using System.IO;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Polly;
using Pragmatic.TemplateApi.Api.Auth;
using Pragmatic.TemplateApi.Core.Model;
using Pragmatic.TemplateApi.Database.Model;
using Pragmatic.TemplateApi.Integration.Tests.Infrastructure.Auth;

namespace Pragmatic.TemplateApi.Integration.Tests.Infrastructure;

public class TestContext
{
    private readonly TestRuntime _testRuntime;

    public TestContext(TestRuntime testRuntime)
    {
        _testRuntime = testRuntime;
    }

    public TodoRecord? NewTodoItem { get; set; }

    public List<TodoRecord>? TodoList { get; set; }

    public string? UploadedBlobName { get; set; }

    /// <summary>
    /// Title of the todo item published to the RabbitMQ queue. Stored so it can be matched in the list.
    /// </summary>
    public string PublishedTodoTitle { get; private set; } = string.Empty;

    /// <summary>
    /// Description of the poison message published to the queue. Stored so it can be matched (or not) in the list.
    /// </summary>
    public string PoisonTodoDescription { get; private set; } = string.Empty;

    public Dictionary<string, TestUser> Users { get; private set; } = new Dictionary<string, TestUser>();

    public HttpResponseMessage? LastResponse { get; set; }

    /// <summary>
    /// The raw HTTP response of the last MCP (JSON-RPC over HTTP) request.
    /// </summary>
    public HttpResponseMessage? McpRawResponse { get; private set; }

    /// <summary>
    /// The parsed JSON-RPC response of the last MCP request.
    /// </summary>
    public JsonNode? McpResponse { get; private set; }

    private static readonly JsonSerializerOptions McpJsonOptions = new(JsonSerializerDefaults.Web);

    public string? HangfireCookieValue { get; set; }

    public AsyncPolicy RetryPolicy { get; } = Policy
        .Handle<HttpRequestException>()
        .WaitAndRetryAsync(10, i => TimeSpan.FromSeconds(1));

    public TestUser? CurrentUser { get; private set; }

    public async Task GetAsync(string path)
    {
        using var client = _testRuntime.GetSubjectApi().CreateClient();
        LastResponse = await RetryPolicy.ExecuteAsync(async () =>
        {
            if (CurrentUser != null)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CurrentUser.UserJwt);

            var result = await client.GetAsync(path);
            return result;
        });
    }

    /// <summary>
    /// Issues a GET against the Consumer host (no authentication — its endpoints are not behind OIDC).
    /// </summary>
    public async Task GetConsumerAsync(string path)
    {
        using var client = _testRuntime.GetSubjectConsumer().CreateClient();
        LastResponse = await RetryPolicy.ExecuteAsync(async () =>
        {
            var result = await client.GetAsync(path);
            return result;
        });
    }

    /// <summary>
    /// Issues a GET against the Worker host (no authentication — its endpoints are not behind OIDC).
    /// </summary>
    public async Task GetWorkerAsync(string path)
    {
        using var client = _testRuntime.GetSubjectWorker().CreateClient();
        LastResponse = await RetryPolicy.ExecuteAsync(async () =>
        {
            var result = await client.GetAsync(path);
            return result;
        });
    }

    /// <summary>
    /// Sends a JSON-RPC 2.0 message to the stateless MCP endpoint (/mcp) using the current user's JWT.
    /// </summary>
    public async Task SendMcpRequestAsync(string method, object? id = null, object? args = null)
    {
        var message = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
        };

        if (id is not null)
            message["id"] = id;

        if (args is not null)
            message["params"] = args;

        using var client = _testRuntime.GetSubjectApi().CreateClient();
        if (CurrentUser != null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CurrentUser.UserJwt);

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var content = new StringContent(JsonSerializer.Serialize(message, McpJsonOptions), Encoding.UTF8, "application/json");

        var previous = McpRawResponse;
        McpRawResponse = await RetryPolicy.ExecuteAsync(async () => await client.PostAsync("mcp", content));
        previous?.Dispose();
    }

    /// <summary>
    /// Parses the last MCP response body into a JSON-RPC message, supporting both
    /// application/json and text/event-stream (SSE) response media types.
    /// </summary>
    public async Task<JsonNode?> ParseMcpResponseAsync()
    {
        var response = McpRawResponse ?? throw new InvalidOperationException(
            "No MCP response is available. Send an MCP request before inspecting the response.");

        var body = await response.Content.ReadAsStringAsync();
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        JsonNode? parsed = null;

        if (mediaType is null || mediaType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            parsed = body.Length > 0 ? JsonNode.Parse(body) : null;
        }
        else
        {
            // SSE framing: each JSON-RPC message arrives as a 'data:' line; keep the last one carrying an 'id'.
            foreach (var line in body.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var payload = trimmed["data:".Length..].Trim();
                if (payload.Length == 0)
                    continue;

                try
                {
                    var candidate = JsonNode.Parse(payload);
                    if (candidate?["id"] is not null)
                        parsed = candidate;
                }
                catch (JsonException)
                {
                    // Non-JSON SSE frames (e.g. keep-alive comments) are ignored.
                }
            }
        }

        McpResponse = parsed;
        return parsed;
    }

    public async Task PostAsJsonAsync<T>(string path, T payload)
    {
        using var client = _testRuntime.GetSubjectApi().CreateClient();
        LastResponse = await RetryPolicy.ExecuteAsync(async () =>
        {
            if (CurrentUser != null)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CurrentUser.UserJwt);

            var result = await client.PostAsJsonAsync(path, payload);
            return result;
        });
    }

    public void AddUser(string userName)
    {
        var user = new TestUser(userName);
        Users.Add(userName, user);
    }

    public void AddUserClaims(string userName, IEnumerable<Claim> claims)
    {
        Users[userName].Claims.AddRange(claims);
    }

    public void SetCurrentUser(string userName)
    {
        var user = Users[userName];
        CurrentUser = user;
        CurrentUser.BuildJwt(_testRuntime.GetSigningCertificate(), _testRuntime.GetJwtIssuer());
    }

    public void ClearCurrentUser()
    {
        CurrentUser = null;
    }

    public async Task PostHangfireLoginAsync()
    {
        var noRedirectOptions = new WebApplicationFactoryClientOptions { AllowAutoRedirect = false };

        using var client = _testRuntime.GetSubjectApi().CreateClient(noRedirectOptions);
        LastResponse = await RetryPolicy.ExecuteAsync(async () =>
        {
            if (CurrentUser != null)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CurrentUser.UserJwt);

            var result = await client.PostAsync("hangfire/login", null);
            return result;
        });
    }

    public async Task GetHangfireDashboardWithCookieAsync(string cookieValue)
    {
        using var client = _testRuntime.GetSubjectApi().CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{HangfireCookieJwtMiddleware.CookieName}={cookieValue}");

        LastResponse = await RetryPolicy.ExecuteAsync(async () =>
        {
            var result = await client.GetAsync("hangfire/dashboard");
            return result;
        });
    }

    public async Task PostHangfireLogoutWithCookieAsync(string cookieValue)
    {
        using var client = _testRuntime.GetSubjectApi().CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{HangfireCookieJwtMiddleware.CookieName}={cookieValue}");

        LastResponse = await RetryPolicy.ExecuteAsync(async () =>
        {
            var result = await client.PostAsync("hangfire/logout", null);
            return result;
        });
    }

    public async Task UploadCsvAsync(string fileName, string content)
    {
        using var client = _testRuntime.GetSubjectApi().CreateClient();
        if (CurrentUser != null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CurrentUser.UserJwt);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        using var contentStream = new StreamContent(stream);
        contentStream.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");

        using var multipartContent = new MultipartFormDataContent
        {
            { contentStream, "file", fileName },
        };

        LastResponse = await RetryPolicy.ExecuteAsync(async () =>
        {
            return await client.PostAsync("todo-list/v1/upload", multipartContent);
        });

        var result = await LastResponse.Content.ReadFromJsonAsync<CsvUploadResult>();
        if (result != null)
        {
            UploadedBlobName = result.BlobName;
        }
    }

    /// <summary>
    /// Publishes a todo item to the RabbitMQ <c>todo-message</c> queue with a unique title.
    /// The title is stored in <see cref="PublishedTodoTitle"/> so it can be matched in the list.
    /// </summary>
    public async Task PublishTodoItemToQueue()
    {
        var title = $"Queue-Imported-Task-{Guid.NewGuid():N}";
        PublishedTodoTitle = title;

        await _testRuntime.PublishTodoItemToQueue(
            title,
            "Imported via the RabbitMQ message queue.",
            DateTime.UtcNow.Date.AddDays(1));
    }

    /// <summary>
    /// Publishes a poison message (null title) to the RabbitMQ queue.
    /// The Consumer's validation rejects it, so it is dead lettered.
    /// </summary>
    public async Task PublishPoisonTodoItemToQueue()
    {
        PoisonTodoDescription = $"Poison message {Guid.NewGuid():N}";

        await _testRuntime.PublishPoisonTodoItemToQueue(
            PoisonTodoDescription,
            DateTime.UtcNow.Date.AddDays(1));
    }

    public Task<IEnumerable<string>> FetchDeadLetterQueue()
        => _testRuntime.FetchDeadLetterQueue();

    public void PurgeFromDLQ(string entity)
        => _testRuntime.PurgeFromDLQ(entity);
}

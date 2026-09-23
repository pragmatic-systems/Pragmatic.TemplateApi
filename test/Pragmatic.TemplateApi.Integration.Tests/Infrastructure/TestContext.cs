using System.IO;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc.Testing;
using Polly;
using Pragmatic.TemplateApi.Api.Auth;
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

    public Dictionary<string, TestUser> Users { get; private set; } = new Dictionary<string, TestUser>();

    public HttpResponseMessage? LastResponse { get; set; }

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
        CurrentUser.BuildJwt(_testRuntime.SigningCertificate, _testRuntime.JwtIssuer);
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
}

public record CsvUploadResult(string BlobName, string Message);

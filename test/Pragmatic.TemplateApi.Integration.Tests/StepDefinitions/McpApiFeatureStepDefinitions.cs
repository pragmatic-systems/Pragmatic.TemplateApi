using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Pragmatic.TemplateApi.Database.Model;
using Pragmatic.TemplateApi.Integration.Tests.Infrastructure;
using Reqnroll;

namespace Pragmatic.TemplateApi.Integration.Tests.StepDefinitions;

[Binding]
public sealed class McpApiFeatureStepDefinitions
{
    private static readonly JsonSerializerOptions TodoJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TestContext _testContext;
    private readonly IReqnrollOutputHelper _output;

    public McpApiFeatureStepDefinitions(TestContext testContext, IReqnrollOutputHelper output)
    {
        _testContext = testContext;
        _output = output;
    }

    [When("We send the MCP initialize request")]
    public async Task WeSendTheMcpInitializeRequest()
    {
        await _testContext.SendMcpRequestAsync("initialize", id: 1, args: new
        {
            protocolVersion = "2025-06-18",
            capabilities = new { },
            clientInfo = new { name = "integration-tests", version = "1.0.0" },
        });

        await _testContext.ParseMcpResponseAsync();
    }

    [Then("The MCP response should be an initialize result")]
    public void TheMcpResponseShouldBeAnInitializeResult()
    {
        var response = _testContext.LastResponse;
        response.ShouldNotBeNull();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "because an authenticated MCP initialize request should succeed");

        var result = _testContext.McpResponse?["result"];
        result.ShouldNotBeNull("because the MCP initialize request should return a JSON-RPC result");
    }

    [Then("The MCP server info should be '(.*)'")]
    public void TheMcpServerInfoShouldBe(string expectedName)
    {
        var name = _testContext.McpResponse?["result"]?["serverInfo"]?["name"]?.GetValue<string>();
        name.ShouldBe(expectedName);
    }

    [When("We send the MCP initialized notification")]
    public async Task WeSendTheMcpInitializedNotification()
    {
        await _testContext.SendMcpRequestAsync("notifications/initialized");
    }

    [Then("The MCP notification should be accepted")]
    public void TheMcpNotificationShouldBeAccepted()
    {
        var response = _testContext.LastResponse;
        response.ShouldNotBeNull();
        ((int)response.StatusCode / 100).ShouldBe(2, "because the initialized notification should be accepted");
    }

    [When("We list the MCP tools")]
    public async Task WeListTheMcpTools()
    {
        await _testContext.SendMcpRequestAsync("tools/list", id: 2, args: new { });
        await _testContext.ParseMcpResponseAsync();
    }

    [Then("The MCP tools should include '(.*)'")]
    public void TheMcpToolsShouldInclude(string toolName)
    {
        var tools = _testContext.McpResponse?["result"]?["tools"] as JsonArray;
        tools.ShouldNotBeNull("because the MCP tools/list response should return a tools array");

        var names = tools
            .Select(t => t?["name"]?.GetValue<string>())
            .Where(n => n != null)
            .ToList();

        names.ShouldContain(toolName);
    }

    [When("We call the MCP tool '(.*)' without arguments")]
    public async Task WeCallTheMcpTool(string toolName)
    {
        await _testContext.SendMcpRequestAsync("tools/call", id: 4, args: new
        {
            name = toolName,
            arguments = new { },
        });

        await _testContext.ParseMcpResponseAsync();
    }

    [When("We call the MCP tool '(.*)' with title '(.*)' and due date '(.*)'")]
    public async Task WeCallTheMcpTool(string toolName, string title, string dueDate)
    {
        await _testContext.SendMcpRequestAsync("tools/call", id: 3, args: new
        {
            name = toolName,
            arguments = new
            {
                title,
                description = $"{title} (added via MCP)",
                dueDate = DateTime.Parse(dueDate),
            },
        });

        await _testContext.ParseMcpResponseAsync();
    }

    [Then("The MCP result should contain a todo item titled '(.*)'")]
    public void TheMcpResultShouldContainATodoItemTitled(string title)
    {
        var json = ReadMcpPayload();
        var todo = JsonSerializer.Deserialize<TodoRecord>(json, TodoJsonOptions);

        todo.ShouldNotBeNull("because the MCP add_todo result should return a todo record");
        todo.Title.ShouldBe(title);
    }

    [Then("The MCP result should be a list containing a todo item titled '(.*)'")]
    public void TheMcpResultShouldBeAListContainingATodoItemTitled(string title)
    {
        var json = ReadMcpPayload();
        var list = JsonSerializer.Deserialize<List<TodoRecord>>(json, TodoJsonOptions);
        var todo = list?.SingleOrDefault(i => i.Title == title);

        todo.ShouldNotBeNull("because the MCP add_todo result should return a todo record");
        todo.Title.ShouldBe(title);
    }

    [Then("The MCP result should not contain a todo item titled '(.*)'")]
    public void TheMcpResultShouldNotContainATodoItemTitled(string title)
    {
        var content = _testContext.McpResponse?["result"]?["content"] as JsonArray;
        content.ShouldNotBeNull("because the MCP list_open_todos response should return content");

        var titles = content
            .Where(c => c?["type"]?.GetValue<string>() == "text")
            .Select(c => c?["text"]?.GetValue<string>())
            .Where(text => text != null)
            .Cast<string>()
            .Select(text => JsonSerializer.Deserialize<List<JsonElement>>(text))
            .Where(list => list != null)
            .SelectMany(list => list!)
            .Select(item => item.GetProperty("title").GetString())
            .ToList();

        titles.Any(t => t == title)
            .ShouldBeFalse($"the todo list should not contain '{title}' (titles: {string.Join(", ", titles.Where(t => t != null).Cast<string>())})");
    }

    [Then("The MCP response should be 401 Unauthorized")]
    public void TheMcpResponseShouldBeUnauthorized()
    {
        var response = _testContext.LastResponse;
        response.ShouldNotBeNull();
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "because anonymous MCP requests should be challenged");
    }

    [Then("The MCP call should be rejected")]
    public void TheMcpCallShouldBeRejected()
    {
        var response = _testContext.LastResponse;
        response.ShouldNotBeNull();

        var node = _testContext.McpResponse;
        var rejected = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || node?["error"] is not null
            || node?["result"]?["isError"]?.GetValue<bool>() == true;

        var actual = JsonSerializer.Serialize(node) ?? $"HTTP {(int)response.StatusCode}";
        _output.WriteLine($"MCP add_todo response: HTTP {(int)response.StatusCode}, body: {actual}");
        rejected.ShouldBeTrue($"Expected the MCP add_todo call to be rejected for a read-only user, but got: {actual}");
    }

    private string ReadMcpPayload()
    {
        var content = _testContext.McpResponse?["result"]?["content"] as JsonArray;
        content.ShouldNotBeNull("because the MCP tools/call response should return a content array");

        var text = content
            .FirstOrDefault(c => c?["type"]?.GetValue<string>() == "text")?["text"]?.GetValue<string>();

        text.ShouldNotBeNullOrEmpty("because the MCP tool result should contain a text payload");

        return text;
    }
}

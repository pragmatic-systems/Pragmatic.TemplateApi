using System.Text.Json.Serialization;

namespace Pragmatic.TemplateApi.Integration.Tests.Infrastructure;

/// <summary>
/// JSON-RPC 2.0 request envelope for the stateless MCP endpoint (/mcp).
/// </summary>
public sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    public string Method { get; init; } = string.Empty;

    // Omitted for notifications, as JSON-RPC 2.0 forbids a null id.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Id { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; init; }
}

using System.Text.Json.Serialization;

namespace Pragmatic.TemplateApi.Integration.Tests.Infrastructure;

/// <summary>
/// JSON-RPC 2.0 request envelope for the MCP endpoint (/mcp).
/// </summary>
public sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    public string Method { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Id { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; init; }
}

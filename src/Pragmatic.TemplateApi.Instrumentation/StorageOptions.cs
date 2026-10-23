namespace Pragmatic.TemplateApi.Instrumentation;

/// <summary>
/// S3-compatible object storage settings: local RustFS in dev, any S3 endpoint in production.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Endpoint { get; init; } = string.Empty;

    public string AccessKeyId { get; init; } = string.Empty;

    public string SecretAccessKey { get; init; } = string.Empty;

    public string Region { get; init; } = "us-east-1";

    public string Bucket { get; init; } = "uploads";
}

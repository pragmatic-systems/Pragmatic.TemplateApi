namespace Pragmatic.TemplateApi.Consumer;

public sealed class RabbitOptions
{
    public const string SectionName = "RabbitMQ";

    public string? ConnectionString { get; init; }

    public int PrefetchCount { get; init; } = 10;
}

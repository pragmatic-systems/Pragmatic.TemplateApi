namespace Pragmatic.TemplateApi.Consumer;

public sealed class RabbitOptions
{
    public const string SectionName = "RabbitMQ";

    public string? ConnectionString { get; init; }

    public string QueueName { get; init; } = "todo-message";

    public int PrefetchCount { get; init; } = 10;
}

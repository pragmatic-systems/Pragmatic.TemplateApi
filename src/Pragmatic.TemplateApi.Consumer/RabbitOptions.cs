namespace Pragmatic.TemplateApi.Consumer;

public sealed class RabbitOptions
{
    public const string SectionName = "RabbitMQ";

    public string? ConnectionString { get; init; }

    public string QueueName { get; init; } = "todo-message";

    public string ExchangeName { get; init; } = "todo-message";

    public string DeadLetterExchangeName { get; init; } = "todo-message-dlx";

    public string DeadLetterQueueName { get; init; } = "todo-message-dl";

    public string DeadLetterRoutingKey { get; init; } = "todo-message-dl";

    public int PrefetchCount { get; init; } = 10;
}

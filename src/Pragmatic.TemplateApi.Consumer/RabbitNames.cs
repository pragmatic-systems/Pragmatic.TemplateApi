namespace Pragmatic.TemplateApi.Consumer;

/// <summary>
/// Names of the RabbitMQ topology the Consumer depends on. The topology itself is provisioned
/// from the repo's rabbitmq/definitions.json (single source of truth) — keep these in sync with it.
/// </summary>
public static class RabbitNames
{
    public const string Exchange = "todo-message";
    public const string Queue = "todo-message";
    public const string DeadLetterQueue = "todo-message-dl";
}

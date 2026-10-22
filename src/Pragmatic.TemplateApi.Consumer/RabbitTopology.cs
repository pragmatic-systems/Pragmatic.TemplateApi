using RabbitMQ.Client;

namespace Pragmatic.TemplateApi.Consumer;

/// <summary>
/// Single source of truth for the RabbitMQ topology the Consumer depends on.
/// All declarations are idempotent, so this can safely be called at startup and by integration tests.
/// </summary>
public static class RabbitTopology
{
    public static void EnsureTopology(IModel channel, RabbitOptions options)
    {
        // Main queue, dead-lettered to the DLX on nack without requeue.
        channel.ExchangeDeclare(
            exchange: options.ExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null);

        channel.QueueDeclare(
            queue: options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = options.DeadLetterExchangeName,
                ["x-dead-letter-routing-key"] = options.DeadLetterRoutingKey,
            });

        channel.QueueBind(queue: options.QueueName, exchange: options.ExchangeName, routingKey: options.QueueName);

        // Dead letter queue.
        channel.ExchangeDeclare(
            exchange: options.DeadLetterExchangeName,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null);

        channel.QueueDeclare(
            queue: options.DeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        channel.QueueBind(queue: options.DeadLetterQueueName, exchange: options.DeadLetterExchangeName, routingKey: options.DeadLetterRoutingKey);
    }
}

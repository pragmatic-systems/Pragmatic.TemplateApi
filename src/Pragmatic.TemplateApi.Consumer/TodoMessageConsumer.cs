using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Pragmatic.TemplateApi.Consumer;

public sealed class TodoMessageConsumer : BackgroundService
{
    private const int MaxLoggedTextLength = 2048;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitOptions _options;
    private readonly ILogger<TodoMessageConsumer> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public TodoMessageConsumer(
        IOptions<RabbitOptions> options,
        ILogger<TodoMessageConsumer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = _options.ConnectionString
            ?? throw new InvalidOperationException($"Missing configuration value '{RabbitOptions.SectionName}:ConnectionString'.");

        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString),
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.BasicQos(prefetchSize: 0, prefetchCount: (ushort)_options.PrefetchCount, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessageReceived;
        _channel.BasicConsume(queue: _options.QueueName, autoAck: false, consumer: consumer);

        _logger.LogInformation(
            "Started consuming from queue '{Queue}' (prefetch: {PrefetchCount})",
            _options.QueueName,
            _options.PrefetchCount);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when the host is stopping.
        }
    }

    private async Task OnMessageReceived(object sender, BasicDeliverEventArgs eventArgs)
    {
        var body = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        var message = TryParseMessage(body);

        _logger.LogInformation(
            "Received message (deliveryTag: {DeliveryTag}, routingKey: {RoutingKey}, redelivered: {Redelivered}, text: {Text})",
            eventArgs.DeliveryTag,
            eventArgs.RoutingKey,
            eventArgs.Redelivered,
            message is null
                ? $"unparseable: {Truncate(body)}"
                : Truncate(message.Text));

        try
        {
            if (message is null)
            {
                throw new FormatException("Message body is not a valid TodoMessage object.");
            }

            await ProcessMessageAsync(message, eventArgs.Redelivered);
            _channel!.BasicAck(deliveryTag: eventArgs.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message (deliveryTag: {DeliveryTag}), nacking without requeue", eventArgs.DeliveryTag);
            _channel!.BasicNack(deliveryTag: eventArgs.DeliveryTag, multiple: false, requeue: false);
        }
    }

    private static TodoMessage? TryParseMessage(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<TodoMessage>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Task ProcessMessageAsync(TodoMessage message, bool redelivered)
    {
        // TODO: Replace with real processing of the consumed message.
        _logger.LogDebug("Processed message with text '{Text}' ({Length} characters)", message.Text, message.Text.Length);
        return Task.CompletedTask;
    }

    private static string Truncate(string value)
        => value.Length <= MaxLoggedTextLength ? value : value[..MaxLoggedTextLength] + "…";

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            _channel.Close();
        }

        if (_connection is not null)
        {
            _connection.Close();
        }

        return base.StopAsync(cancellationToken);
    }
}

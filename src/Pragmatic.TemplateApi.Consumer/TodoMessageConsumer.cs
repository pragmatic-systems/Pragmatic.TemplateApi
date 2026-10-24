using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pragmatic.CQRS;
using Pragmatic.TemplateApi.Core.Handlers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Pragmatic.TemplateApi.Consumer;

public sealed class TodoMessageConsumer : BackgroundService
{
    private const int MaxLoggedTextLength = 2048;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitOptions _options;
    private readonly ILogger<TodoMessageConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private IConnection? _connection;
    private IChannel? _channel;

    public TodoMessageConsumer(
        IOptions<RabbitOptions> options,
        ILogger<TodoMessageConsumer> logger,
        IServiceScopeFactory scopeFactory)
    {
        _options = options.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = _options.ConnectionString
            ?? throw new InvalidOperationException($"Missing configuration value '{RabbitOptions.SectionName}:ConnectionString'.");

        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString),
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken: stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: (ushort)_options.PrefetchCount, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageReceived;
        await _channel.BasicConsumeAsync(queue: _options.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

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
        var cancellationToken = eventArgs.CancellationToken;
        var body = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        var message = TryParseMessage(body);

        _logger.LogInformation(
            "Received message (deliveryTag: {DeliveryTag}, routingKey: {RoutingKey}, redelivered: {Redelivered}, text: {Text})",
            eventArgs.DeliveryTag,
            eventArgs.RoutingKey,
            eventArgs.Redelivered,
            message is null
                ? $"unparseable: {Truncate(body)}"
                : Truncate(message.Title));

        try
        {
            if (message is null)
            {
                throw new FormatException("Message body is not a valid TodoMessage object.");
            }

            await ProcessMessageAsync(message, eventArgs.Redelivered, cancellationToken);
            await _channel!.BasicAckAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message (deliveryTag: {DeliveryTag}), nacking without requeue", eventArgs.DeliveryTag);

            try
            {
                await _channel!.BasicNackAsync(deliveryTag: eventArgs.DeliveryTag, multiple: false, requeue: false, cancellationToken: CancellationToken.None);
            }
            catch (Exception nackEx)
            {
                _logger.LogError(nackEx, "Failed to nack message (deliveryTag: {DeliveryTag}); it may be redelivered and reprocessed", eventArgs.DeliveryTag);
            }
        }
    }

    private static InsertTodo? TryParseMessage(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<InsertTodo>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task ProcessMessageAsync(InsertTodo message, bool redelivered, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        // Queue messages don't get the API's model binding validation, so validate explicitly.
        // A failure here throws and the message is dead-lettered instead of inserted.
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<InsertTodo>>();
        var validation = await validator.ValidateAsync(message, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        _logger.LogDebug("Processed message with {Title} / {Description})", message.Title, message.Description);
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(message, cancellationToken);
    }

    private static string Truncate(string? value)
    {
        value = value ?? string.Empty;
        return value.Length <= MaxLoggedTextLength ? value : value[..MaxLoggedTextLength] + "…";
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync(cancellationToken);
        }

        if (_connection is not null)
        {
            try
            {
                await _connection.CloseAsync(cancellationToken);
            }
            catch (Exception)
            {
                // Swallow — the connection may already be in a closed/disposed state
            }
        }

        await base.StopAsync(cancellationToken);
    }
}

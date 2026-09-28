namespace Pragmatic.TemplateApi.Consumer;

/// <summary>
/// Simple message object consumed from the RabbitMQ queue.
/// </summary>
public sealed record TodoMessage(string Text);

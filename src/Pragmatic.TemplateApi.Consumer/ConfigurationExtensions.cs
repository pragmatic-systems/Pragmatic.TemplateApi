using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Pragmatic.TemplateApi.Core.Handlers;
using Pragmatic.TemplateApi.Core.Validators;
using RabbitMQ.Client;

namespace Pragmatic.TemplateApi.Consumer;

public static class ConfigurationExtensions
{
    public static IServiceCollection AddRabbitConsumer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitOptions>(configuration.GetSection(RabbitOptions.SectionName));

        services.AddSingleton<TodoMessageConsumer>();

        services.AddHostedService(sp => sp.GetRequiredService<TodoMessageConsumer>());

        services
            .AddHealthChecks()
            .AddRabbitMqHealthcheck();

        return services;
    }

    public static IHealthChecksBuilder AddRabbitMqHealthcheck(this IHealthChecksBuilder builder)
    {
        return builder.AddRabbitMQ(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitOptions>>().Value;
            var connectionString = options.ConnectionString
                ?? throw new InvalidOperationException($"Missing configuration value '{RabbitOptions.SectionName}:ConnectionString'.");

            var factory = new ConnectionFactory
            {
                Uri = new Uri(connectionString),
                AutomaticRecoveryEnabled = true
            };

            return factory.CreateConnectionAsync();
        }, "Message Broker");
    }
}

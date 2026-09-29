using HealthChecks.RabbitMQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Pragmatic.TemplateApi.Consumer;

public static class ServiceCollectionExtensions
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
        // NOTE: Due to integration test lifecycle, connectionString is overriden in the config after Build, but before AppStart.
        // Because of this we can't inject the raw config values directly into the RabbitMq Healthcheck.
        builder.Add(new HealthCheckRegistration(
            name: "Message Broker",
            factory: sp =>
            {
                var options = sp.GetRequiredService<IOptions<RabbitOptions>>().Value;
                var connectionString = options.ConnectionString
                    ?? throw new InvalidOperationException($"Missing configuration value '{RabbitOptions.SectionName}:ConnectionString'.");

                return new RabbitMQHealthCheck(new RabbitMQHealthCheckOptions
                {
                    ConnectionUri = new Uri(connectionString)
                });
            },
            failureStatus: null,
            tags: null));

        return builder;
    }
}

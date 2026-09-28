using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Pragmatic.TemplateApi.Consumer;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitConsumer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitOptions>(configuration.GetSection(RabbitOptions.SectionName));

        var connectionString = configuration.GetSection($"{RabbitOptions.SectionName}:ConnectionString").Value
            ?? throw new ArgumentException($"Missing configuration value '{RabbitOptions.SectionName}:ConnectionString'.");

        services.AddSingleton<TodoMessageConsumer>();
        services.AddHostedService(sp => sp.GetRequiredService<TodoMessageConsumer>());

        services
            .AddHealthChecks()
            .AddRabbitMQ(connectionString, name: "Message Broker");

        return services;
    }
}

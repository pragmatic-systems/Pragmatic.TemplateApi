using Azure.Storage.Blobs;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pragmatic.CQRS;
using Pragmatic.TemplateApi.Core.Validators;
using Pragmatic.TemplateApi.Database;
using Pragmatic.TemplateApi.Instrumentation.HostedServices;
using Prometheus;
using Serilog;

namespace Pragmatic.TemplateApi.Instrumentation;

public static class ConfigurationExtensions
{
    public static IServiceCollection WithPostgres(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>((s, options) =>
        {
            var config = s.GetRequiredService<IConfiguration>();
            var conn = config.GetConnectionString("PostgresDb");

            options
                .UseNpgsql(conn)
                .UseSnakeCaseNamingConvention()
                .EnableDetailedErrors();
        });

        services.AddHostedService<PostgresInitService>();

        return services;
    }

    public static IServiceCollection WithMediatr(this IServiceCollection services)
    {
        services.AddCqrs(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(
                typeof(InsertTodoValidator).Assembly);
        });
        return services;
    }

    public static IServiceCollection WithSerilog(this IServiceCollection services, IConfiguration configuration, string appName)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .WriteTo.Console()
            .Enrich.WithProperty("App", appName)
            .CreateLogger();

        services.AddSingleton(Log.Logger);

        services.AddLogging(lb =>
        {
            lb.ClearProviders();
            lb.AddSerilog(Log.Logger);
        });

        return services;
    }

    public static WebApplication MapInstrumentationEndpoints(this WebApplication app)
    {
        app.UseHttpsRedirectionExcluding("/_system");

        app.MapMetrics("_system/metrics");
        app.MapHealthChecks("/_system/ping", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/_system/health", new HealthCheckOptions
        {
            ResponseWriter = async (c, r) =>
            {
                var response = new
                {
                    Health = r.Status.ToString(),
                    Checks = r.Entries.Select(x =>
                        new
                        {
                            Health = x.Value.Status.ToString(),
                            Name = x.Key,
                        }),
                    Duration = r.TotalDuration,
                };
                await c.Response.WriteAsJsonAsync(response);
            },
        });

        return app;
    }

    public static IServiceCollection WithHangfire(this IServiceCollection services, IConfiguration configuration)
    {
        // NOTE: There is no lazy load here for PostgreSQL - if the DB does not exist at this point it will fall over.
        services.AddHangfire(hfConfig =>
        {
            var connection = configuration.GetConnectionString("PostgresDb");

            ArgumentException.ThrowIfNullOrEmpty(connection);

            Migrator.EnsureDb(connection);

            hfConfig
                .UsePostgreSqlStorage(connection);
        });

        return services;
    }

    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddHealthChecks()
            .AddNpgSql(s => configuration.GetConnectionString("PostgresDb") ?? throw new ArgumentException("ConnectionString for PostgresDb not found"), name: "Database Provider")
            .AddUrlGroup(
                s =>
                {
                    var issuer = configuration
                        .GetRequiredSection("OpenIdConnect:Issuer")
                        .Value;

                    return new Uri($"{issuer}/.well-known/openid-configuration");
                },
                "OIDC Provider");

        return services;
    }

    public static IServiceCollection WithAzureBlobStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(provider =>
            new BlobServiceClient(configuration.GetSection("Storage:ConnectionString").Value));

        services.AddSingleton(provider =>
        {
            var client = provider.GetRequiredService<BlobServiceClient>();
            var containerName = configuration.GetSection("Storage:ContainerName").Value ?? "uploads";
            return client.GetBlobContainerClient(containerName);
        });

        services.AddHostedService<BlobContainerInitService>();

        return services;
    }

    public static IApplicationBuilder UseHttpsRedirectionExcluding(this IApplicationBuilder builder, string excluding)
    {
        builder.UseWhen(
            context => !context.Request.Path.StartsWithSegments(excluding),
            builder => builder.UseHttpsRedirection());

        return builder;
    }
}

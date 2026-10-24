using Amazon.Runtime;
using Amazon.S3;
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pragmatic.CQRS;
using Pragmatic.TemplateApi.Core.Handlers;
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

    public static IServiceCollection WithValidators(this IServiceCollection services)
    {
        services.AddSingleton(typeof(IValidator<InsertTodo>), typeof(InsertTodoValidator));
        return services;
    }

    public static IServiceCollection WithHangfire(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHangfire(hfConfig =>
        {
            var connection = configuration.GetConnectionString("PostgresDb");
            ArgumentException.ThrowIfNullOrEmpty(connection);

            hfConfig.UsePostgreSqlStorage(bootstrapper =>
                bootstrapper.UseNpgsqlConnection(connection));
        });

        return services;
    }

    public static IServiceCollection AddPostgresHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddHealthChecks()
            .AddNpgSql(s => configuration.GetConnectionString("PostgresDb") ?? throw new ArgumentException("ConnectionString for PostgresDb not found"), name: "Database Provider");

        return services;
    }

    public static IServiceCollection AddOIDCHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddHealthChecks()
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

    public static IServiceCollection WithS3Storage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<StorageOptions>>().Value;

            ArgumentException.ThrowIfNullOrEmpty(options.Endpoint);

            var config = new AmazonS3Config
            {
                ServiceURL = options.Endpoint,
                AuthenticationRegion = options.Region,
                ForcePathStyle = true,
            };

            return new AmazonS3Client(new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey), config);
        });

        services.AddHostedService<BucketInitService>();

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

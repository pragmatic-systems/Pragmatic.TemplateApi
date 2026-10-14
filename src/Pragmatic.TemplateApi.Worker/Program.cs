using Pragmatic.TemplateApi.Instrumentation;
using Prometheus;
using Serilog;

namespace Pragmatic.TemplateApi.Worker;

#pragma warning disable S1118
public class Program
#pragma warning restore S1118
{
    public static void Main(string[] args)
    {
#pragma warning disable S2139
        try
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.WithSerilog(builder.Configuration, "Pragmatic.TemplateApi Worker");
            builder.Services.WithPostgres(builder.Configuration);
            builder.Services.WithAzureBlobStorage(builder.Configuration);
            builder.Services.WithHangfire(builder.Configuration);
            builder.Services.WithHangfireServer(builder.Configuration);
            builder.Services.AddAppHealthChecks(builder.Configuration);

            var app = builder.Build();

            app.MapInstrumentationEndpoints();
            app.UseMetricServer(); // https://github.com/prometheus-net/prometheus-net
            app.UseRouting();
            app.UseHttpMetrics();

            Log.Logger.Information("Starting Worker");

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Logger.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
#pragma warning restore S2139
    }
}

using System;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Pragmatic.TemplateApi.Api.Auth;
using Pragmatic.TemplateApi.Instrumentation;
using Prometheus;
using Serilog;

namespace Pragmatic.TemplateApi.Api;

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

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.WithSwaggerGen();
            builder.Services.WithIngressConfig();
            builder.Services.WithSerilog(builder.Configuration, "Pragmatic.TemplateApi API");
            builder.Services.WithPostgres(builder.Configuration);
            builder.Services.WithS3Storage(builder.Configuration);
            builder.Services.WithValidators();
            builder.Services.WithMediatr();
            builder.Services.WithOpenIdConnect(builder.Configuration);
            builder.Services.WithAuthorizationPolicy();
            builder.Services.WithHangfire(builder.Configuration);
            builder.Services.AddControllers();
            builder.Services.AddAppHealthChecks(builder.Configuration);

            var app = builder.Build();

            app.MapInstrumentationEndpoints();
            app.ConfigureSwagger();
            app.UseMetricServer(); // https://github.com/prometheus-net/prometheus-net
            app.UseRouting();
            app.UseHttpMetrics();
            app.UseHangfireCookieJwt(); // Extract JWT from cookie and inject into Auth header BEFORE the JWT Bearer authentication handler runs.
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseHangfireDashboard();
            app.MapControllers();

            Log.Logger.Information("Starting Application");

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error Starting Application");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
#pragma warning restore S2139
    }
}

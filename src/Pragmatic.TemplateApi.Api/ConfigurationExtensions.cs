using System;
using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Pragmatic.TemplateApi.Api.Auth;
using Pragmatic.TemplateApi.Instrumentation;

namespace Pragmatic.TemplateApi.Api;

public static class ConfigurationExtensions
{
    public static IServiceCollection WithSwaggerGen(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter a JWT token to authenticate.",
            };

            options.AddSecurityDefinition("Bearer", securityScheme);

            var securityRequirement = new OpenApiSecurityRequirement();
            securityRequirement.Add(
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                },
                Array.Empty<string>());

            options.AddSecurityRequirement(securityRequirement);
        });

        return services;
    }

    public static IServiceCollection WithIngressConfig(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // NOTE: Evaluate alternative rate limiting policies appropriate for your application.
            // AddTokenBucketLimiter / AddSlidingWindowLimiter, etc.
            options.AddFixedWindowLimiter(policyName: "Basic", options =>
            {
                options.PermitLimit = 10;          // Allow 10 requests
                options.Window = TimeSpan.FromSeconds(10); // per 10-second window
                options.QueueLimit = 5;            // Queue up to 5 requests
                options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
        });

        services.Configure<KestrelServerOptions>(options =>
        {
            // 1mb request size
            options.Limits.MaxRequestBodySize = 1024 * 1024;
        });

        return services;
    }

    public static IServiceCollection WithOpenIdConnect(this IServiceCollection services, IConfiguration configuration)
    {
        var authSection = configuration.GetSection("OpenIdConnect");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // By setting the issuer as the Authority - the JWT bearer will load the OIDC Config from this location.
                options.Authority = authSection.GetValue<string>("Issuer");

                // NOTE: This uses any pre-loaded IConfigurationManager<OpenIdConnectConfiguration> which can be supplied by test runners.
                // If none is supplied, then it remains null and will be auto-initialized based off Authority/Issuer.
                options.ConfigurationManager = services
                    .BuildServiceProvider()
                    .GetService<IConfigurationManager<OpenIdConnectConfiguration>>();

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidAudience = authSection.GetValue<string>("Audience"),

                    // NOTE: Custom audience validator to support AWS Cognito where client_id is the aud.
                    // Remove if you are not using Cognito.
                    AudienceValidator = TokenValidators.ValidateAudienceOrClientId,
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    },
                    OnForbidden = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    },
                };
            });

        // Transform AWS Cognito scope claims into role claims for policy-based authorization.
        services.AddSingleton<IClaimsTransformation, CognitoScopeToRoleClaimsTransformer>();

        return services;
    }

    public static IServiceCollection WithAuthorizationPolicy(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(Roles.TodoListRead, policy => policy.RequireClaim(ClaimTypes.Role, Roles.TodoListRead))
            .AddPolicy(Roles.TodoListWrite, policy => policy.RequireClaim(ClaimTypes.Role, Roles.TodoListWrite));

        return services;
    }

    public static WebApplication ConfigureSwagger(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        return app;
    }

    public static WebApplication UseHangfireDashboard(this WebApplication app)
    {
        app.UseHangfireDashboard("/hangfire/dashboard", new DashboardOptions
        {
            Authorization = new[]
            {
                new HangfireAuthorizationFilter(),
            },
            DisplayStorageConnectionString = false,
            AppPath = null,
        });

        return app;
    }
}

using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Pragmatic.TemplateApi.Integration.Tests.Infrastructure.Auth;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;
using WireMock.Net.Testcontainers;

namespace Pragmatic.TemplateApi.Integration.Tests.Infrastructure;

public class TestRuntime : IAsyncDisposable
{
    public WireMockContainer? WireMockContainer { get; private set; }

    public PostgreSqlContainer? PostgresContainer { get; private set; }

    public AzuriteContainer? AzuriteContainer { get; private set; }

    public WebApplicationFactory<Api.Program>? SubjectApi { get; private set; }

    public WebApplicationFactory<Worker.Program>? SubjectWorker { get; private set; }

    /// <summary>
    /// Certificate used for signing the JWT used by the API.
    /// </summary>
    public PemCertificate? SigningCertificate { get; private set; }

    public string? JwtIssuer { get; private set; }

    public WebApplicationFactory<Api.Program> GetSubjectApi()
        => SubjectApi ?? throw new InvalidOperationException(
            "TestRuntime has not been initialized. Call InitializeAsync() before using the subject API.");

    public async ValueTask DisposeAsync()
    {
        if (WireMockContainer != null)
            await WireMockContainer.DisposeAsync();

        if (AzuriteContainer != null)
            await AzuriteContainer.DisposeAsync();

        if (SubjectApi != null)
            await SubjectApi.DisposeAsync();

        if (SubjectWorker != null)
            await SubjectWorker.DisposeAsync();
    }

    public async Task InitializeAsync()
    {
        // Configure Postgres
        var postgresContainer = new PostgreSqlBuilder("postgres:15.1")
            .WithAutoRemove(true)
            .Build();
        PostgresContainer = postgresContainer;

        // Configure Wiremock
        var wireMockContainer = new WireMockContainerBuilder()
            .WithImage("sheyenrath/wiremock.net-alpine:2.14.0")
            .WithAutoRemove(true)
            .Build();
        WireMockContainer = wireMockContainer;

        // Configure Azurite (Blob)
        var azuriteContainer = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.37.0")
            .WithAutoRemove(true)
            .Build();
        AzuriteContainer = azuriteContainer;

        await postgresContainer.StartAsync();
        await wireMockContainer.StartAsync();
        await azuriteContainer.StartAsync();

        // Create SSL Certificate
        SigningCertificate = PemCertificate.Create();

        // Get JWT Issuer
        var jwtIssuer = wireMockContainer.GetPublicUrl().TrimEnd('/');
        JwtIssuer = jwtIssuer;

        SubjectApi = ConfigureSubjectApi(postgresContainer, azuriteContainer);
        SubjectWorker = ConfigureSubjectWorker(postgresContainer, azuriteContainer);

        SubjectWorker.CreateClient();

        var wiremockAdmin = new WiremockConfigurationClient(WireMockContainer.CreateWireMockAdminClient());
        await wiremockAdmin.ConfigureOIDCWellKnown(jwtIssuer);
    }

    private WebApplicationFactory<Api.Program> ConfigureSubjectApi(PostgreSqlContainer postgresContainer, AzuriteContainer azuriteContainer)
    {
        return new WebApplicationFactory<Api.Program>()
            .WithWebHostBuilder(builder =>
            {
                // Override this so we don't inherit any default config.
                builder.UseEnvironment("IntegrationTest");

                // Configure overrides for application
                builder.ConfigureAppConfiguration((c, b) =>
                {
                    var config = new MemoryConfigurationSource();

                    // Override config settings for connection strings / service urls here.
                    config.InitialData = new Dictionary<string, string?>
                    {
                        // Connection Strings
                        { "ConnectionStrings:PostgresDb", postgresContainer.GetConnectionString() },

                        // OIDC
                        { "OpenIdConnect:Audience", TestConstants.Audience },
                        { "OpenIdConnect:Issuer", JwtIssuer },

                        // Azure Blob Storage
                        { "Storage:ConnectionString", azuriteContainer.GetConnectionString() },
                        { "Storage:ContainerName", "uploads" },
                    };

                    b.Add(config);
                });

                builder.ConfigureTestServices(services =>
                {
                    var config = MockOpenIdConfigurationManagerBuilder.Create(
                        JwtIssuer,
                        TestConstants.OpenIdConfigUrl,
                        SigningCertificate);

                    // We are overriding the OIDC Config provider here.
                    // This supports injecting self signed JWTs.
                    // Without this - we have to configure Wiremock certificates to support HTTPS.
                    services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(config);
                });

                builder.UseDefaultServiceProvider(o =>
                {
                    // Force validation of DependencyInjection.
                    o.ValidateOnBuild = true;
                });
            });
    }

    private static WebApplicationFactory<Worker.Program> ConfigureSubjectWorker(PostgreSqlContainer postgresContainer, AzuriteContainer azuriteContainer)
    {
        return new WebApplicationFactory<Worker.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("IntegrationTest");

                builder.ConfigureAppConfiguration((c, b) =>
                {
                    var config = new MemoryConfigurationSource();

                    config.InitialData = new Dictionary<string, string?>
                    {
                        { "ConnectionStrings:PostgresDb", postgresContainer.GetConnectionString() },
                        { "Storage:ConnectionString", azuriteContainer.GetConnectionString() },
                        { "Storage:ContainerName", "uploads" },
                    };

                    b.Add(config);
                });

                builder.UseDefaultServiceProvider(o =>
                {
                    o.ValidateOnBuild = true;
                });
            });
    }
}

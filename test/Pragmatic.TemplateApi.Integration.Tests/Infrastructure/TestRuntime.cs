using System.Text;
using System.Text.Json;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Pragmatic.TemplateApi.Core.Handlers;
using Pragmatic.TemplateApi.Integration.Tests.Infrastructure.Auth;
using RabbitMQ.Client;
using Testcontainers.Azurite;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using WireMock.Net.Testcontainers;

namespace Pragmatic.TemplateApi.Integration.Tests.Infrastructure;

public class TestRuntime : IAsyncDisposable
{
    public WireMockContainer? WireMockContainer { get; private set; }

    public PostgreSqlContainer? PostgresContainer { get; private set; }

    public AzuriteContainer? AzuriteContainer { get; private set; }

    public RabbitMqContainer? RabbitMqContainer { get; private set; }

    public WebApplicationFactory<Api.Program>? SubjectApi { get; private set; }

    public WebApplicationFactory<Worker.Program>? SubjectWorker { get; private set; }

    public WebApplicationFactory<Consumer.Program>? SubjectConsumer { get; private set; }

    /// <summary>
    /// Certificate used for signing the JWT used by the API.
    /// </summary>
    public PemCertificate? SigningCertificate { get; private set; }

    public string? JwtIssuer { get; private set; }

    public WebApplicationFactory<Api.Program> GetSubjectApi()
        => SubjectApi ?? throw new InvalidOperationException(
            "TestRuntime has not been initialized. Call InitializeAsync() before using the subject API.");

    public string GetJwtIssuer()
        => JwtIssuer ?? throw new InvalidOperationException(
            "TestRuntime has not been initialized. Call InitializeAsync() before getting JwtIssuer.");

    public PemCertificate GetSigningCertificate()
        => SigningCertificate ?? throw new InvalidOperationException(
            "TestRuntime has not been initialized. Call InitializeAsync() before getting SigningCertificate.");

    public async ValueTask DisposeAsync()
    {
        if (WireMockContainer != null)
            await WireMockContainer.DisposeAsync();

        if (PostgresContainer != null)
            await PostgresContainer.DisposeAsync();

        if (AzuriteContainer != null)
            await AzuriteContainer.DisposeAsync();

        if (RabbitMqContainer != null)
            await RabbitMqContainer.DisposeAsync();

        if (SubjectApi != null)
            await SubjectApi.DisposeAsync();

        if (SubjectWorker != null)
            await SubjectWorker.DisposeAsync();

        if (SubjectConsumer != null)
            await SubjectConsumer.DisposeAsync();

        GC.SuppressFinalize(this);
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
        // --skipApiVersionCheck: Azurite can't verify the newest Storage SDK API versions, so allow them through.
        var azuriteContainer = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.37.0")
            .WithAutoRemove(true)
            .WithCommand("--skipApiVersionCheck")
            .Build();
        AzuriteContainer = azuriteContainer;

        // Configure RabbitMQ
        var rabbitMqContainer = new RabbitMqBuilder("rabbitmq:4.1-management")
            .WithUsername("local")
            .WithPassword("local")
            .WithAutoRemove(true)
            .Build();
        RabbitMqContainer = rabbitMqContainer;

        await postgresContainer.StartAsync();
        await wireMockContainer.StartAsync();
        await azuriteContainer.StartAsync();
        await rabbitMqContainer.StartAsync();

        await EnsureRabbitTopology(rabbitMqContainer);

        // Create SSL Certificate
        SigningCertificate = PemCertificate.Create();

        // Get JWT Issuer
        var jwtIssuer = wireMockContainer.GetPublicUrl().TrimEnd('/');
        JwtIssuer = jwtIssuer;

        SubjectApi = ConfigureSubjectApi(postgresContainer, azuriteContainer);
        SubjectWorker = ConfigureSubjectWorker(postgresContainer, azuriteContainer);
        SubjectConsumer = ConfigureSubjectConsumer(postgresContainer, rabbitMqContainer);

        SubjectWorker.CreateClient();
        SubjectConsumer.CreateClient();

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
                        GetJwtIssuer(),
                        TestConstants.OpenIdConfigUrl,
                        GetSigningCertificate());

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

    /// <summary>
    /// Declares the topology the Consumer expects, mirroring the local RabbitMQ definitions.
    /// </summary>
    private static async Task EnsureRabbitTopology(RabbitMqContainer rabbitMqContainer)
    {
        var factory = new ConnectionFactory { Uri = new Uri(rabbitMqContainer.GetConnectionString()) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // Single source of truth for the topology, so the test broker can't drift from the app.
        await Consumer.RabbitTopology.EnsureTopologyAsync(channel, new Consumer.RabbitOptions());
    }

    // Mirrors the Consumer's RabbitOptions defaults, which are the single source of truth for the topology names.
    private static readonly Consumer.RabbitOptions RabbitOptionsDefaults = new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<(IConnection Connection, IChannel Channel)> CreateRabbitChannel()
    {
        var rabbitMqContainer = RabbitMqContainer
            ?? throw new InvalidOperationException(
                "TestRuntime has not been initialized. Call InitializeAsync() before using RabbitMQ.");

        var factory = new ConnectionFactory { Uri = new Uri(rabbitMqContainer.GetConnectionString()) };
        var connection = await factory.CreateConnectionAsync();
        return (connection, await connection.CreateChannelAsync());
    }

    private static ValueTask PublishMessage(IChannel channel, string exchange, string routingKey, InsertTodo message)
        => channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            body: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOptions)));

    /// <summary>
    /// Publishes an <see cref="InsertTodo"/> message to the <c>todo-message</c> queue,
    /// acting as an external producer so the Consumer can pick it up and insert it.
    /// </summary>
    public async Task PublishTodoItemToQueue(string title, string description, DateTime dueDate)
    {
        var (connection, channel) = await CreateRabbitChannel();
        await using (connection)
        await using (channel)
        {
            await PublishMessage(channel, RabbitOptionsDefaults.ExchangeName, RabbitOptionsDefaults.QueueName, new InsertTodo(title, description, dueDate));
        }
    }

    /// <summary>
    /// Publishes a message with a null title, which the Consumer's validation rejects,
    /// so it deterministically fails and is dead lettered.
    /// </summary>
    public async Task PublishPoisonTodoItemToQueue(string description, DateTime dueDate)
    {
        var (connection, channel) = await CreateRabbitChannel();
        await using (connection)
        await using (channel)
        {
            await PublishMessage(channel, RabbitOptionsDefaults.ExchangeName, RabbitOptionsDefaults.QueueName, new InsertTodo(null, description, dueDate));
        }
    }

    private readonly object _deadLetterLock = new();
    private readonly List<string> _deadLetterBuffer = new List<string>();

    /// <summary>
    /// Drains the dead letter queue, adds to the memory buffer and returns a snapshot of its contents.
    /// </summary>
    public async Task<IEnumerable<string>> FetchDeadLetterQueue()
    {
        var (connection, channel) = await CreateRabbitChannel();
        await using (connection)
        await using (channel)
        {
            while (await channel.BasicGetAsync(queue: RabbitOptionsDefaults.DeadLetterQueueName, autoAck: true) is { } result)
            {
                var payload = Encoding.UTF8.GetString(result.Body.ToArray());
                lock (_deadLetterLock)
                {
                    _deadLetterBuffer.Add(payload);
                }
            }
        }

        lock (_deadLetterLock)
        {
            return _deadLetterBuffer.ToArray();
        }
    }

    /// <summary>
    /// Remove an item from the dead letter queue once it has been asserted against.
    /// </summary>
    public void PurgeFromDLQ(string entity)
    {
        lock (_deadLetterLock)
        {
            _deadLetterBuffer.Remove(entity);
        }
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

    private static WebApplicationFactory<Consumer.Program> ConfigureSubjectConsumer(PostgreSqlContainer postgresContainer, RabbitMqContainer rabbitMqContainer)
    {
        return new WebApplicationFactory<Consumer.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("IntegrationTest");

                builder.ConfigureAppConfiguration((c, b) =>
                {
                    var config = new MemoryConfigurationSource();

                    config.InitialData = new Dictionary<string, string?>
                    {
                        { "ConnectionStrings:PostgresDb", postgresContainer.GetConnectionString() },
                        { "RabbitMQ:ConnectionString", rabbitMqContainer.GetConnectionString() },
                        { "RabbitMQ:QueueName", "todo-message" },

                        // Tests invoke the re-drive explicitly via DlqReDriveService, so the scheduled loop must stay off.
                        { "RabbitMQ:ReDriveEnabled", "false" },
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

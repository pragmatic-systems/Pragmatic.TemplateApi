using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Npgsql;
using Polly;

namespace Pragmatic.TemplateApi.Provisioning;

public static class HangfireProvisioner
{
    private static readonly Policy RetryPolicy = Policy
            .Handle<NpgsqlException>()
            .WaitAndRetry(
                10,
                i => TimeSpan.FromSeconds(2),
                (e, t) => Console.WriteLine("Retrying... Waiting for database"));

    public static void EnsureTables(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentException("connectionString is required.");

        // Constructing the storage runs Hangfire's idempotent DDL (CREATE ... IF NOT EXISTS),
        var options = new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = true };

        RetryPolicy.Execute(() =>
            _ = new PostgreSqlStorage(new NpgsqlConnectionFactory(connectionString, options, _ => { })));
    }
}

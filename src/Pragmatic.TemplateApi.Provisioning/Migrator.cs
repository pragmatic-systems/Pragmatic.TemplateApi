using System.Reflection;
using DbUp;
using Npgsql;
using Polly;

namespace Pragmatic.TemplateApi.Provisioning;

public static class Migrator
{
    private static readonly Policy RetryPolicy = Policy
            .Handle<NpgsqlException>()
            .WaitAndRetry(
                10,
                i => TimeSpan.FromSeconds(2),
                (e, t) => Console.WriteLine("Retrying... Waiting for database"));

    public static void EnsureDb(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentException("connectionString is required.");

        RetryPolicy.Execute(() =>
            EnsureDatabase.For.PostgresqlDatabase(connectionString));
    }

    public static void Migrate(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentException("connectionString is required.");

        RetryPolicy.Execute(() =>
            ExecuteMigration(connectionString));
    }

    private static void ExecuteMigration(string connectionString)
    {
        var upgrader =
            DeployChanges.To
                .PostgresqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                .LogToConsole()
                .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
            throw result.Error;
    }
}

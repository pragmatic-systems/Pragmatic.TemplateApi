namespace Pragmatic.TemplateApi.Provisioning;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            ProvisionDb();
            ProvisionHangfire();
            await ProvisionStorageAsync();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Success!");
            Console.ResetColor();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex);
            Console.ResetColor();
            return -1;
        }
    }

    private static string RequireDbConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("DbConnectionString");
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentException("DbConnectionString environment variable is required.");

        return connectionString;
    }

    private static void ProvisionDb()
    {
        var connectionString = RequireDbConnectionString();

        Migrator.EnsureDb(connectionString);
        Migrator.Migrate(connectionString);

        Console.WriteLine("Database schema ensured and migrated.");
    }

    private static void ProvisionHangfire()
    {
        HangfireProvisioner.EnsureTables(RequireDbConnectionString());
        Console.WriteLine("Hangfire schema ensured.");
    }

    private static async Task ProvisionStorageAsync()
    {
        var endpoint = Environment.GetEnvironmentVariable("StorageEndpoint");
        if (string.IsNullOrEmpty(endpoint))
            throw new ArgumentException("StorageEndpoint environment variable is required.");

        await StorageProvisioner.EnsureBucketAsync(
            endpoint: endpoint,
            accessKeyId: Environment.GetEnvironmentVariable("StorageAccessKeyId") ?? throw new ArgumentException("StorageAccessKeyId environment variable is required."),
            secretAccessKey: Environment.GetEnvironmentVariable("StorageSecretAccessKey") ?? throw new ArgumentException("StorageSecretAccessKey environment variable is required."),
            region: Environment.GetEnvironmentVariable("StorageRegion") ?? "us-east-1",
            bucket: Environment.GetEnvironmentVariable("StorageBucket") ?? "uploads");
    }
}

using Reqnroll;

namespace Pragmatic.TemplateApi.Integration.Tests.Infrastructure;

[Binding]
public class RuntimeSetup
{
    public static readonly TestRuntime TestRuntime = new TestRuntime();

    [BeforeTestRun]
    public static async Task ConfigureRuntime()
    {
        await TestRuntime.InitializeAsync();
    }

    [AfterTestRun]
    public static async Task DisposeRuntime()
    {
        await TestRuntime.DisposeAsync();
    }
}

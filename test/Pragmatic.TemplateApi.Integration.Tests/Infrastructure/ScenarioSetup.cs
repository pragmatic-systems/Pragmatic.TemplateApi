using Reqnroll;
using Reqnroll.BoDi;

namespace Pragmatic.TemplateApi.Integration.Tests.Infrastructure;

[Binding]
public class ScenarioSetup
{
    private readonly IObjectContainer objectContainer;

    public ScenarioSetup(IObjectContainer objectContainer)
    {
        this.objectContainer = objectContainer;
    }

    public TestRuntime? TestRuntime { get; private set; }

    public TestContext? TestContext { get; private set; }

    [BeforeScenario]
    public void ConfigureInjection()
    {
        TestRuntime = RuntimeSetup.TestRuntime;
        TestContext = new TestContext(TestRuntime);

        objectContainer.RegisterInstanceAs(TestRuntime);
        objectContainer.RegisterInstanceAs(TestContext);
    }
}

public static class TestConstants
{
    public static string OpenIdConfigUrl { get; } = "https://i.do.not.exist/.well-known/openid-configuration";

    public static string Issuer { get; } = $"ApiTest:Issuer";

    public static string Audience { get; } = $"ApiTest:Audience";
}

using Pragmatic.TemplateApi.Integration.Tests.Infrastructure;
using Reqnroll;

namespace Pragmatic.TemplateApi.Integration.Tests.StepDefinitions;

[Binding]
public sealed class HealthcheckStepDefinitions
{
    // For additional details on SpecFlow step definitions see https://go.specflow.org/doc-stepdef
    private readonly TestContext _testContext;

    public HealthcheckStepDefinitions(TestContext testContext)
    {
        _testContext = testContext;
    }

    [When("We call the healthcheck endpoint")]
    public async Task WeCallTheHealthcheckEndpoint()
    {
        await _testContext.GetAsync("_system/health");
    }

    [When("We call the ping endpoint")]
    public async Task WeCallThePingEndpoint()
    {
        await _testContext.GetAsync("_system/ping");
    }

    [When("We call the metrics endpoint")]
    public async Task WeCallTheMetricsEndpoint()
    {
        await _testContext.GetAsync("_system/metrics");
    }

    [When("We call the consumer healthcheck endpoint")]
    public async Task WeCallTheConsumerHealthcheckEndpoint()
    {
        await _testContext.GetConsumerAsync("_system/health");
    }

    [When("We call the worker healthcheck endpoint")]
    public async Task WeCallTheWorkerHealthcheckEndpoint()
    {
        await _testContext.GetWorkerAsync("_system/health");
    }
}

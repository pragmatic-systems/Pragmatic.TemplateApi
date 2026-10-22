using Polly;
using Pragmatic.TemplateApi.Integration.Tests.Infrastructure;
using Reqnroll;

namespace Pragmatic.TemplateApi.Integration.Tests.StepDefinitions;

[Binding]
public sealed class DeadLetterStepDefinitions
{
    private readonly TestContext _testContext;

    public DeadLetterStepDefinitions(
        TestContext testContext)
    {
        _testContext = testContext;
    }

    [When("We publish a poison todo item to the queue")]
    public void WePublishAPoisonTodoItemToTheQueue()
    {
        _testContext.PublishPoisonTodoItemToQueue();
    }

    [Then("The dead letter queue should contain the poison message")]
    public async Task TheDeadLetterQueueShouldContainThePoisonMessage()
    {
        await Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                Enumerable.Range(0, 15)
                    .Select(i => TimeSpan.FromSeconds(1)))
            .ExecuteAsync(async () =>
            {
                var payload = _testContext.FetchDeadLetterQueue()
                    .FirstOrDefault(p => p.Contains(_testContext.PoisonTodoDescription))
                    ?? throw new InvalidOperationException(
                        $"Dead letter queue does not contain a message with description '{_testContext.PoisonTodoDescription}'.");

                _testContext.PurgeFromDLQ(payload);
            });
    }

    [Then("The result does not contain the poison todo item")]
    public void TheResultDoesNotContainThePoisonTodoItem()
    {
        var match = _testContext.TodoList?
            .SingleOrDefault(i => i.Description == _testContext.PoisonTodoDescription);

        ((object?)match).ShouldBeNull();
    }
}

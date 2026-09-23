using System.Net;
using Pragmatic.TemplateApi.Api.Auth;
using Pragmatic.TemplateApi.Integration.Tests.Infrastructure;
using Reqnroll;

namespace Pragmatic.TemplateApi.Integration.Tests.StepDefinitions;

[Binding]
public sealed class HangfireDashboardStepDefinitions
{
    private readonly TestContext _testContext;

    public HangfireDashboardStepDefinitions(TestContext testContext)
    {
        _testContext = testContext;
    }

    [When("We call hangfire login with bearer token")]
    public async Task WeCallHangfireLoginWithBearerToken()
    {
        await _testContext.PostHangfireLoginAsync();
    }

    [Then("The response should redirect to hangfire dashboard")]
    public void TheResponseShouldRedirectToHangfireDashboard()
    {
        _testContext.LastResponse.ShouldNotBeNull();
        _testContext.LastResponse.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = _testContext.LastResponse.Headers.Location;
        location.ShouldNotBeNull();
        location.ToString().ShouldContain("/hangfire/dashboard");
    }

    [Then("The hangfire cookie should be present")]
    public void TheHangfireCookieShouldBePresent()
    {
        _testContext.LastResponse.ShouldNotBeNull();
        var setCookieHeaders = _testContext.LastResponse.Headers.GetValues("Set-Cookie");
        setCookieHeaders.ShouldNotBeNull();
        var setCookieValues = setCookieHeaders.ToList();
        setCookieValues.ShouldNotBeEmpty();

        var hangfireCookieLine = setCookieValues.FirstOrDefault(c => c.StartsWith(HangfireCookieJwtMiddleware.CookieName));
        hangfireCookieLine.ShouldNotBeNull();

        // Extract the cookie value (everything after the '=' up to ';')
        var cookieValue = hangfireCookieLine.Split('=')[1].Split(';')[0];
        _testContext.HangfireCookieValue = cookieValue;
    }

    [When("We call hangfire dashboard with cookie")]
    public async Task WeCallHangfireDashboardWithCookie()
    {
        await _testContext.GetHangfireDashboardWithCookieAsync(_testContext.HangfireCookieValue!);
    }

    [When("We call hangfire logout")]
    public async Task WeCallHangfireLogout()
    {
        await _testContext.PostHangfireLogoutWithCookieAsync(_testContext.HangfireCookieValue!);
    }

    [Then("The hangfire cookie should be removed")]
    public void TheHangfireCookieShouldBeRemoved()
    {
        _testContext.LastResponse.ShouldNotBeNull();
        var setCookieHeaders = _testContext.LastResponse.Headers.GetValues("Set-Cookie");
        var setCookieValues = setCookieHeaders?.ToList() ?? new List<string>();

        var hangfireCookieLine = setCookieValues.FirstOrDefault(c => c.StartsWith(HangfireCookieJwtMiddleware.CookieName));
        hangfireCookieLine.ShouldNotBeNull();

        // When a cookie is deleted, it should have an expired date or max-age=0
        var lowerLine = hangfireCookieLine.ToLowerInvariant();
        var hasExpired = lowerLine.Contains("expires=");
        var hasMaxAgeZero = lowerLine.Contains("max-age=0");
        (hasExpired || hasMaxAgeZero).ShouldBeTrue($"Cookie deletion indicator not found in: {hangfireCookieLine}");
    }
}

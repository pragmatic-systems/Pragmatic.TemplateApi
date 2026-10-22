using System.Security.Claims;
using Pragmatic.TemplateApi.Api.Auth;
using Shouldly;

namespace Pragmatic.TemplateApi.UnitTests;

public class CognitoScopeToRoleClaimsTransformerTests
{
    private readonly CognitoScopeToRoleClaimsTransformer _transformer = new();

    [Fact]
    public async Task UnauthenticatedPrincipal_IsReturnedUnchanged()
    {
        var unauthenticatedIdentity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(unauthenticatedIdentity);

        var result = await _transformer.TransformAsync(principal);

        // No new role claims should be added
        var roleClaims = result.FindAll(ClaimTypes.Role).ToList();
        roleClaims.ShouldBeEmpty();
    }

    [Fact]
    public async Task MultipleScopeClaims_ProducesAllRoleClaims()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("scope", "todolist-permissions/TodoList:Read"),
                new Claim("scope", "todolist-permissions/TodoList:Write"),
            },
            "Cookies",
            "name",
            "role");
        var principal = new ClaimsPrincipal(identity);

        var result = await _transformer.TransformAsync(principal);

        var roleClaims = result.FindAll(ClaimTypes.Role).ToList();
        roleClaims.Count.ShouldBe(2);
        roleClaims.ShouldContain(c => c.Value == "TodoList:Read");
        roleClaims.ShouldContain(c => c.Value == "TodoList:Write");
    }
}

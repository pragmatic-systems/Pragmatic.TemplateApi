using System.Security.Claims;
using Pragmatic.TemplateApi.Integration.Tests.Infrastructure.Auth;

namespace Pragmatic.TemplateApi.Integration.Tests.Infrastructure;

public class TestUser
{
    public TestUser(string userName)
    {
        UserName = userName;
    }

    public string UserName { get; private set; }

    public List<Claim> Claims { get; private set; } = new List<Claim>();

    public string? UserJwt { get; private set; }

    public void BuildJwt(PemCertificate certificate, string issuer)
    {
        var audience = TestConstants.Audience;
        var signingCertificate = certificate.ToX509Certificate2();

        var accessTokenParameters = new AccessToken(audience, issuer, signingCertificate, Claims);
        UserJwt = accessTokenParameters.ToJwt();
    }
}

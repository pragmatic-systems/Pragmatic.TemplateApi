using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Pragmatic.TemplateApi.Integration.Tests.Infrastructure.Auth;

public static class MockOpenIdConfigurationManagerBuilder
{
    public static IConfigurationManager<OpenIdConnectConfiguration> Create(
        string issuer,
        string openIdConfigUrl,
        PemCertificate signingCertificate)
    {
        var config = OpenIdConnectDiscoveryDocumentConfiguration.ForIssuer(issuer);

        var openIdHttpClient = new HttpClient(
            new MockOpenIdProviderMessageHandler(config, signingCertificate, openIdConfigUrl));

        return new ConfigurationManager<OpenIdConnectConfiguration>(
            openIdConfigUrl,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(openIdHttpClient));
    }
}

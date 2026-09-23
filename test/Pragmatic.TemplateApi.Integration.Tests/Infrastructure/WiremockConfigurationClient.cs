using System.Text.Json;
using Pragmatic.TemplateApi.Integration.Tests.Infrastructure.Auth;
using WireMock.Admin.Mappings;
using WireMock.Client;

namespace Pragmatic.TemplateApi.Integration.Tests.Infrastructure;

/// <summary>
/// Wrapper for configuring Wiremock endpoints.
/// </summary>
public class WiremockConfigurationClient
{
    private IWireMockAdminApi _wireMockAdminApi;

    public WiremockConfigurationClient(IWireMockAdminApi wireMockAdminApi) =>
        _wireMockAdminApi = wireMockAdminApi;

    /// <summary>
    /// Configures the OIDC Well Known endpoint in wiremock so it returns 200 OK.
    /// This ensures availability for the healthcheck.
    /// </summary>
    /// <returns></returns>
    public async Task<WiremockConfigurationClient> ConfigureOIDCWellKnown(string issuer)
    {
        var discoveryDocument = OpenIdConnectDiscoveryDocumentConfiguration.ForIssuer(issuer);

        var mapping = new MappingModelBuilder()
            .WithRequest(request =>
            {
                request.WithPath("/.well-known/openid-configuration");
            })
            .WithResponse(response =>
            {
                response.WithStatusCode(200);
                response.WithBody(JsonSerializer.Serialize(discoveryDocument));
            })
            .Build();

        await _wireMockAdminApi
            .PostMappingAsync(mapping);

        return this;
    }
}

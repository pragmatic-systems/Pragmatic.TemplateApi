using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Pragmatic.TemplateApi.IntegrationTests.Infrastructure.Auth;

public sealed class MockOpenIdProviderMessageHandler : HttpMessageHandler
{
    private readonly OpenIdConnectDiscoveryDocumentConfiguration _openIdConnectDiscoveryDocumentConfiguration;
    private readonly PemCertificate _tokenSigningCertificate;
    private readonly string _openIdConfigUrl;

    public MockOpenIdProviderMessageHandler(
        OpenIdConnectDiscoveryDocumentConfiguration openIdConnectDiscoveryDocumentConfiguration,
        PemCertificate tokenSigningCertificate,
        string openIdConfigUrl)
    {
        _openIdConnectDiscoveryDocumentConfiguration = openIdConnectDiscoveryDocumentConfiguration ?? throw new ArgumentNullException(nameof(openIdConnectDiscoveryDocumentConfiguration));
        _tokenSigningCertificate = tokenSigningCertificate ?? throw new ArgumentNullException(nameof(tokenSigningCertificate));
        _openIdConfigUrl = openIdConfigUrl;
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return SendAsync(request, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.RequestUri);

        if (request.RequestUri.AbsoluteUri.Contains(_openIdConfigUrl))
            return await GetOpenIdConfigurationHttpResponseMessage();

        if (request.RequestUri.AbsoluteUri.Equals(_openIdConnectDiscoveryDocumentConfiguration.JwksUri))
            return await GetJwksHttpResonseMessage();

        throw new NotSupportedException("I only support mocking jwks.json and openid-configuration");
    }

    private Task<HttpResponseMessage> GetOpenIdConfigurationHttpResponseMessage()
    {
        var httpResponseMessage = new HttpResponseMessage();

        httpResponseMessage.StatusCode = HttpStatusCode.OK;
        httpResponseMessage.Content = JsonContent.Create(_openIdConnectDiscoveryDocumentConfiguration, MediaTypeHeaderValue.Parse("application/json"));
        return Task.FromResult(httpResponseMessage);
    }

    private Task<HttpResponseMessage> GetJwksHttpResonseMessage()
    {
        var httpResponseMessage = new HttpResponseMessage();

        var jwksCertificate = _tokenSigningCertificate.ToJwksCertificate();
        httpResponseMessage.Content = JsonContent.Create(jwksCertificate, MediaTypeHeaderValue.Parse("application/json"));

        httpResponseMessage.StatusCode = HttpStatusCode.OK;
        return Task.FromResult(httpResponseMessage);
    }
}

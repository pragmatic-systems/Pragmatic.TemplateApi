using System.Text.Json.Serialization;

namespace Pragmatic.TemplateApi.Integration.Tests.Infrastructure.Auth;

public record OpenIdConnectDiscoveryDocumentConfiguration(
    [property: JsonPropertyName("issuer")] string Issuer,
    [property: JsonPropertyName("authorization_endpoint")] string AuthorizationEndpoint,
    [property: JsonPropertyName("token_endpoint")] string TokenEndpoint,
    [property: JsonPropertyName("device_authorization_endpoint")] string DeviceAuthorizationEndpoint,
    [property: JsonPropertyName("userinfo_endpoint")] string UserinfoEndpoint,
    [property: JsonPropertyName("mfa_challenge_endpoint")] string MfaChallengeEndpoint,
    [property: JsonPropertyName("jwks_uri")] string JwksUri,
    [property: JsonPropertyName("registration_endpoint")] string RegistrationEndpoint,
    [property: JsonPropertyName("revocation_endpoint")] string RevocationEndpoint,
    [property: JsonPropertyName("scopes_supported")] string[] ScopesSupported,
    [property: JsonPropertyName("response_types_supported")] string[] ResponseTypesSupported,
    [property: JsonPropertyName("code_challenge_methods_supported")] string[] CodeChallengeMethodsSupported,
    [property: JsonPropertyName("response_modes_supported")] string[] ResponseModesSupported,
    [property: JsonPropertyName("subject_types_supported")] string[] SubjectTypesSupported,
    [property: JsonPropertyName("id_token_signing_alg_values_supported")] string[] IdTokenSigningAlgValuesSupported,
    [property: JsonPropertyName("token_endpoint_auth_methods_supported")] string[] TokenEndpointAuthMethodsSupported,
    [property: JsonPropertyName("claims_supported")] string[] ClaimsSupported,
    [property: JsonPropertyName("request_uri_parameter_supported")] bool RequestUriParameterSupported)
{
    public static OpenIdConnectDiscoveryDocumentConfiguration ForIssuer(string issuer)
    {
#pragma warning disable SA1118 // Parameter should not span multiple lines
#pragma warning disable CA1861 // Avoid constant arrays as arguments
#pragma warning disable SA1009 // Closing parenthesis should be spaced correctly
#pragma warning disable SA1111 // Closing parenthesis should be on line of last parameter
        var discoveryDocument = new OpenIdConnectDiscoveryDocumentConfiguration(
            Issuer: issuer,
            AuthorizationEndpoint: "http://i.do.not.exist/authorize",
            TokenEndpoint: "http://i.do.not.exist/oauth/token",
            DeviceAuthorizationEndpoint: "http://i.do.not.exist/oauth/device/code",
            UserinfoEndpoint: "https://i.do.not.exist/userinfo",
            MfaChallengeEndpoint: "https://i.do.not.exist/mfa/challenge",
            JwksUri: "https://i.do.not.exist/.well-known/jwks.json",
            RegistrationEndpoint: "https://i.do.not.exist/oidc/register",
            RevocationEndpoint: "https://i.do.not.exist/oauth/revoke",
            ScopesSupported: new[]
            {
                "openid",
                "profile",
                "offline_access",
                "weatherforecast:read",
            },
            ResponseTypesSupported: new[]
            {
                "code",
                "token",
                "id_token",
                "code token",
                "code id_token",
                "token id_token",
                "code token id_token",
            },
            CodeChallengeMethodsSupported: new[]
            {
                "S256",
                "plain",
            },
            ResponseModesSupported: new[]
            {
                "query",
                "fragment",
                "form_post",
            },
            SubjectTypesSupported: new[]
            {
                "public",
            },
            IdTokenSigningAlgValuesSupported: new[]
            {
                "HS256",
                "RS256",
            },
            TokenEndpointAuthMethodsSupported: new[]
            {
                "client_secret_basic",
                "client_secret_post",
            },
            ClaimsSupported: new[]
            {
                "aud",
                "exp",
                "iat",
                "iss",
                "sub",
                "nbf",
                "scope",
                "country",
            },
            RequestUriParameterSupported: false
        );
#pragma warning restore SA1111 // Closing parenthesis should be on line of last parameter
#pragma warning restore SA1009 // Closing parenthesis should be spaced correctly
#pragma warning restore CA1861 // Avoid constant arrays as arguments
#pragma warning restore SA1118 // Parameter should not span multiple lines

        return discoveryDocument;
    }
}

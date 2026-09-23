using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace Pragmatic.TemplateApi.Integration.Tests.Infrastructure.Auth;

public record AccessToken
{
    public AccessToken(string audience, string issuer, X509Certificate2 certificate, params Claim[] claims)
    {
        Audience = audience;
        Issuer = issuer;
        SigningCertificate = certificate;
        Claims = new List<Claim>(claims);
    }

    public AccessToken(string audience, string issuer, X509Certificate2 certificate, IEnumerable<Claim> claims)
        : this(audience, issuer, certificate, claims.ToArray())
    {
    }

    public X509Certificate2 SigningCertificate { get; set; }
    public string Audience { get; set; }
    public string Issuer { get; set; }
    public List<Claim> Claims { get; set; }

    public void SetClaim(string claimType, string claimValue)
    {
        var claim = Claims?.FirstOrDefault(x => x.Type == claimType);
        if (claim != null)
            Claims?.Remove(claim);

        Claims ??= new List<Claim>();
        Claims.Add(new Claim(claimType, claimValue));
    }

    public void AppendClaim(string claimType, string claimValue)
    {
        var claim = Claims?.FirstOrDefault(x => x.Type == claimType && x.Value == claimValue);
        if (claim != null)
            Claims?.Remove(claim);

        Claims ??= new List<Claim>();
        Claims.Add(new Claim(claimType, claimValue));
    }

    public string ToJwt()
    {
        var signingCredentials = new SigningCredentials(new X509SecurityKey(SigningCertificate), SecurityAlgorithms.RsaSha256);

        var notBefore = DateTime.UtcNow;
        var expires = DateTime.UtcNow.AddHours(1);

        var identity = new ClaimsIdentity(Claims);

        var securityTokenDescriptor = new SecurityTokenDescriptor
        {
            Audience = Audience,
            Issuer = Issuer,
            NotBefore = notBefore,
            Expires = expires,
            SigningCredentials = signingCredentials,
            Subject = identity,
        };

        var securityTokenHandler = new JwtSecurityTokenHandler();
        var securityToken = securityTokenHandler.CreateToken(securityTokenDescriptor);
        var encodedAccessToken = securityTokenHandler.WriteToken(securityToken);

        return encodedAccessToken;
    }
}

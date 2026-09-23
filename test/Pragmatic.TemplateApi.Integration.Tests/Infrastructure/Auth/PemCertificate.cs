using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace Pragmatic.TemplateApi.Integration.Tests.Infrastructure.Auth;

public sealed record PemCertificate(string Certificate, string PrivateKey, string PublicKey)
{
    public X509Certificate2 ToX509Certificate2()
    {
        return X509Certificate2.CreateFromPem(Certificate, PrivateKey);
    }

    public JsonWebKeySet ToJwksCertificate()
    {
        var certificate = X509Certificate2.CreateFromPem(Certificate);

        var keyParameters = certificate.PublicKey.GetRSAPublicKey()?.ExportParameters(false);
        if (!keyParameters.HasValue)
            ArgumentNullException.ThrowIfNull(keyParameters);

        var e = Base64UrlEncoder.Encode(keyParameters.Value.Exponent);
        var n = Base64UrlEncoder.Encode(keyParameters.Value.Modulus);
        var dict = new Dictionary<string, string>()
        {
            { "e", e },
            { "kty", "RSA" },
            { "n", n },
        };
        byte[] hashBytes =
            SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(dict)));
        JsonWebKey jsonWebKey = new JsonWebKey()
        {
            Kid = Base64UrlEncoder.Encode(hashBytes),
            Kty = "RSA",
            E = e,
            N = n,
        };
        JsonWebKeySet jsonWebKeySet = new JsonWebKeySet();
        jsonWebKeySet.Keys.Add(jsonWebKey);

        return jsonWebKeySet;
    }

    public static PemCertificate Create()
    {
        using (RSA rsa = RSA.Create())
        {
            // Generate a 2048 bit RSA key pair
            rsa.KeySize = 2048;

            // Create a new self signed certificate
            CertificateRequest request = new CertificateRequest(
                "cn=i.do.not.exist",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Set the validity period of the certificate
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new Oid("1.3.6.1.5.5.7.3.1"),
                }, false));

            X509Certificate2 cert = request.CreateSelfSigned(
                new DateTimeOffset(DateTime.UtcNow.AddDays(-1)),
                new DateTimeOffset(DateTime.UtcNow.AddDays(3650)));

            // Export the certificate to a PEM file
            byte[] certificateBytes = cert.RawData;
            char[] certificatePem = PemEncoding.Write("CERTIFICATE", certificateBytes);

            AsymmetricAlgorithm? key = cert.GetRSAPrivateKey();
            if (key == null)
                ArgumentNullException.ThrowIfNull(key);

            byte[] pubKeyBytes = key.ExportSubjectPublicKeyInfo();
            byte[] privKeyBytes = key.ExportPkcs8PrivateKey();
            char[] pubKeyPem = PemEncoding.Write("PUBLIC KEY", pubKeyBytes);
            char[] privKeyPem = PemEncoding.Write("PRIVATE KEY", privKeyBytes);

            var pemCertificate = new PemCertificate(
                Certificate: new string(certificatePem),
                PublicKey: new string(pubKeyPem),
                PrivateKey: new string(privKeyPem));

            return pemCertificate;
        }
    }
}

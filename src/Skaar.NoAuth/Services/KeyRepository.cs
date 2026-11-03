using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Services;

/// <summary>
/// Signs tokens with the self-signed certificate embedded in the assembly.
/// </summary>
internal sealed class KeyRepository : IKeyRepository, IDisposable
{
    private const string Algorithm = SecurityAlgorithms.RsaSha256;
    private const string CertificateResource = "Cert.selfsign.pfx";
    private readonly X509Certificate2 certificate;
    private readonly X509SecurityKey key;

    public KeyRepository()
    {
        using var stream = typeof(KeyRepository).Assembly.GetManifestResourceStream(CertificateResource)
                           ?? throw new InvalidOperationException($"Cannot find resource {CertificateResource}");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        certificate = X509CertificateLoader.LoadPkcs12(memory.ToArray(), string.Empty);
        key = new X509SecurityKey(certificate);
        SigningCredentials = new SigningCredentials(key, Algorithm);
    }

    public SigningCredentials SigningCredentials { get; }

    public IEnumerable<JsonWebKeyModel> PublicKeys
    {
        get
        {
            using var rsa = certificate.GetRSAPublicKey()
                            ?? throw new InvalidOperationException("The certificate has no RSA public key");
            var parameters = rsa.ExportParameters(false);
            return
            [
                new JsonWebKeyModel(
                    Kty: "RSA",
                    Use: "sig",
                    Kid: key.KeyId,
                    X5t: Base64UrlEncoder.Encode(certificate.GetCertHash()),
                    E: Base64UrlEncoder.Encode(parameters.Exponent),
                    N: Base64UrlEncoder.Encode(parameters.Modulus),
                    Alg: Algorithm,
                    X5c: [Convert.ToBase64String(certificate.RawData)])
            ];
        }
    }

    public void Dispose() => certificate.Dispose();
}

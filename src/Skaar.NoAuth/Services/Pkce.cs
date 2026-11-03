using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Skaar.NoAuth.Services;

/// <summary>
/// Proof Key for Code Exchange (RFC 7636).
/// </summary>
internal static class Pkce
{
    public const string S256 = "S256";
    public const string Plain = "plain";

    public static bool IsSupportedMethod(string method) => method is S256 or Plain;

    /// <summary>43 to 128 characters of [A-Z] / [a-z] / [0-9] / "-" / "." / "_" / "~".</summary>
    public static bool IsWellFormed(string value) =>
        value.Length is >= 43 and <= 128 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~');

    public static bool Verify(string codeVerifier, string codeChallenge, string method) =>
        method switch
        {
            S256 => CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)))),
                Encoding.ASCII.GetBytes(codeChallenge)),
            Plain => codeVerifier == codeChallenge,
            _ => false
        };
}

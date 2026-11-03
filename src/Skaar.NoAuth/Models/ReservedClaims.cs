using System.Collections.Frozen;

namespace Skaar.NoAuth.Models;

/// <summary>
/// Claims set by NoAuth itself. They cannot be overridden by login forms or client profiles.
/// </summary>
public static class ReservedClaims
{
    private static readonly FrozenSet<string> Types = new[]
    {
        "iss", "sub", "aud", "exp", "nbf", "iat", "jti", "nonce", "auth_time", "at_hash", "azp",
        "client_id", "scope", "uid", "name", "given_name", "family_name"
    }.ToFrozenSet();

    public static bool IsReserved(string type) => Types.Contains(type);
}

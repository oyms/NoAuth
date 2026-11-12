using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Services;

/// <summary>
/// Audiences and claims of Microsoft Entra ID v2.0 tokens, used when <see cref="Config.Entra.Enabled"/>.
/// </summary>
internal sealed class EntraClaims(Configuration config)
{
    private const string Graph = "https://graph.microsoft.com";
    private const string DefaultScope = ".default";
    private static readonly string[] OidcScopes = ["openid", "profile", "email", "offline_access"];

    public bool Enabled => config.Entra.Enabled;

    /// <summary>
    /// The audiences of the requested scopes. The resource is the scope without the last segment
    /// (<c>api://my-api/access_as_user</c> gives <c>api://my-api</c>), and <c>api://{guid}</c> gives the guid,
    /// as in v2.0 tokens. Scopes without a resource (<c>User.Read</c>) are Microsoft Graph scopes.
    /// </summary>
    public IReadOnlyCollection<string> Audiences(IEnumerable<string> scopes) =>
        scopes.Select(Resource).OfType<string>().Distinct().ToList();

    /// <summary>Claims of access tokens. <c>scp</c> replaces the <c>scope</c> claim.</summary>
    public IEnumerable<Claim> AccessTokenClaims(AccessToken token)
    {
        yield return new Claim("ver", "2.0");
        yield return new Claim("tid", config.Entra.TenantId);
        yield return new Claim("azp", token.ClientId);
        if (token.User is null)
        {
            yield return new Claim("oid", ToGuid(token.ClientId));
            yield return new Claim("idtyp", "app");
            yield break;
        }

        yield return new Claim("oid", ObjectId(token.User));
        yield return new Claim("preferred_username", PreferredUsername(token.User));
        var scopes = token.Scopes.Where(s => Resource(s) is not null).Select(ScopeName).Where(s => s != DefaultScope);
        var scp = string.Join(' ', scopes.Distinct());
        if (scp.Length > 0) yield return new Claim("scp", scp);
    }

    public IEnumerable<Claim> IdTokenClaims(UserProfile user)
    {
        yield return new Claim("ver", "2.0");
        yield return new Claim("tid", config.Entra.TenantId);
        yield return new Claim("oid", ObjectId(user));
        yield return new Claim("preferred_username", PreferredUsername(user));
    }

    /// <summary>
    /// The <c>client_info</c> of token responses, used by MSAL to identify the account.
    /// </summary>
    public string ClientInfo(UserProfile user) =>
        Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string>
        {
            ["uid"] = ObjectId(user),
            ["utid"] = config.Entra.TenantId
        }));

    /// <summary>The <c>oid</c> claim of the user, or a guid derived from the uid.</summary>
    private static string ObjectId(UserProfile user) => Claim(user, "oid") ?? ToGuid(user.Uid);

    private string PreferredUsername(UserProfile user) =>
        Claim(user, "preferred_username") ?? Claim(user, "email") ??
        $"{UsernamePart(user.GivenName)}.{UsernamePart(user.FamilyName)}@{config.Entra.Domain}";

    private static string? Claim(UserProfile user, string type) =>
        user.ExtraClaims.FirstOrDefault(c => c.Type == type)?.Value;

    private static string? Resource(string scope)
    {
        if (OidcScopes.Contains(scope)) return null;
        var separator = scope.LastIndexOf('/');
        if (separator < 0) return Graph;
        var resource = scope[..separator];
        return resource.StartsWith("api://", StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParse(resource["api://".Length..], out var appId)
            ? appId.ToString()
            : resource;
    }

    private static string ScopeName(string scope) => scope[(scope.LastIndexOf('/') + 1)..];

    /// <summary>A guid as is, or a stable guid derived from any other value.</summary>
    private static string ToGuid(string value) =>
        Guid.TryParse(value, out var guid)
            ? guid.ToString()
            : new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16)).ToString();

    private static string UsernamePart(string name)
    {
        var normalized = name.ToLowerInvariant()
            .Replace("æ", "ae").Replace("ø", "o").Replace("å", "a")
            .Normalize(NormalizationForm.FormD);
        return new string(normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => char.IsWhiteSpace(c) ? '.' : c)
            .Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-')
            .ToArray());
    }
}

using System.Text.RegularExpressions;
using Skaar.NoAuth.Config;

namespace Skaar.NoAuth.Services;

/// <summary>
/// The paths of Microsoft Entra ID v2.0, like <c>/{tenant}/oauth2/v2.0/token</c>. Any tenant is accepted
/// (a tenant id, a domain, <c>common</c>, <c>organizations</c>), and requests are rewritten to the configured paths.
/// </summary>
internal static partial class EntraPaths
{
    public const string Discovery = "v2.0/.well-known/openid-configuration";
    public const string Authorize = "oauth2/v2.0/authorize";
    public const string Token = "oauth2/v2.0/token";
    public const string Logout = "oauth2/v2.0/logout";
    public const string Keys = "discovery/v2.0/keys";

    /// <summary>Tenants of multi-tenant apps, with <c>{tenantid}</c> in the issuer of the discovery document.</summary>
    private static readonly string[] MultiTenants = ["common", "organizations", "consumers"];

    private const string TenantKey = "noauth-entra-tenant";

    [GeneratedRegex(@"^/(?<tenant>[^/]+)/(?<endpoint>v2\.0/\.well-known/openid-configuration|oauth2/v2\.0/(authorize|token|logout)|discovery/v2\.0/keys)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex PathPattern();

    /// <summary>
    /// Rewrites a request to an Entra path to the configured path of the endpoint, and remembers the tenant.
    /// </summary>
    public static bool TryRewrite(HttpRequest request, Paths paths)
    {
        var match = PathPattern().Match(request.Path.Value ?? string.Empty);
        if (!match.Success) return false;

        var target = match.Groups["endpoint"].Value.ToLowerInvariant() switch
        {
            Discovery => paths.Discovery,
            Authorize => paths.Authorize,
            Token => paths.Token.FirstOrDefault(),
            Logout => paths.EndSession,
            Keys => paths.Jwks,
            _ => null
        };
        if (string.IsNullOrEmpty(target)) return false;

        request.HttpContext.Items[TenantKey] = match.Groups["tenant"].Value;
        request.Path = target;
        return true;
    }

    /// <summary>The tenant of a request to an Entra path.</summary>
    public static string? Tenant(HttpRequest request) => request.HttpContext.Items[TenantKey] as string;

    public static string Path(string tenant, string endpoint) => $"/{tenant}/{endpoint}";

    /// <summary>The issuer in the discovery document of a tenant.</summary>
    public static string Issuer(Configuration config, string tenant) =>
        MultiTenants.Contains(tenant, StringComparer.OrdinalIgnoreCase)
            ? Entra.Issuer(config.BaseUrl, "{tenantid}")
            : config.Issuer;
}

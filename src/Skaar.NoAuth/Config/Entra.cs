namespace Skaar.NoAuth.Config;

/// <summary>
/// Makes NoAuth look like Microsoft Entra ID (Azure AD) v2.0: tenant paths like
/// <c>/{tenant}/oauth2/v2.0/token</c>, the issuer <c>{BaseUrl}/{TenantId}/v2.0</c>, and Entra claims
/// (<c>oid</c>, <c>tid</c>, <c>scp</c>, <c>preferred_username</c>, ...) in tokens.
/// </summary>
public record Entra
{
    public bool Enabled { get; set; }

    /// <summary>The <c>tid</c> claim, and the tenant in the issuer.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>The domain of generated <c>preferred_username</c> claims, for users without an email.</summary>
    public string Domain { get; set; } = string.Empty;

    public void Validate()
    {
        if (Enabled && string.IsNullOrWhiteSpace(TenantId))
            throw new InvalidOperationException("Entra:TenantId is required when Entra is enabled");
    }

    /// <summary>The issuer for a tenant, like <c>https://login.microsoftonline.com/{tenant}/v2.0</c>.</summary>
    public static string Issuer(Uri baseUrl, string tenant) => $"{baseUrl.AbsoluteUri.TrimEnd('/')}/{tenant}/v2.0";
}

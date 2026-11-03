namespace Skaar.NoAuth.Config;

public record Configuration
{
    public const string SectionName = "NoAuth";

    public string Issuer { get; set; } = string.Empty;
    public Uri BaseUrl { get; set; } = new("https://localhost:44367");
    public Paths Paths { get; set; } = new();
    public Oidc Oidc { get; set; } = new();
    public bool UseReferenceTokens { get; set; }

    /// <summary>
    /// Enforce OAuth 2.1 rules that are relaxed by default: PKCE (S256) is required,
    /// authorization codes are single-use and short-lived, and client_id and redirect_uri must match the code.
    /// </summary>
    public bool Strict { get; set; }

    public uint TokenTtlInSeconds { get; set; }

    /// <summary>
    /// Use the scheme and host of each request, instead of <see cref="BaseUrl"/>, for urls in the discovery
    /// document and redirects. Useful when clients reach the server by different names
    /// (e.g. <c>localhost</c> from the host and <c>noauth</c> from other containers).
    /// </summary>
    public bool DeriveUrlsFromRequest { get; set; }

    public Uri? Url(string? path) => string.IsNullOrEmpty(path) ? null : new Uri(BaseUrl, path);

    /// <summary>The url of a path, as seen by the client making the request.</summary>
    public Uri? Url(string? path, HttpRequest request) =>
        string.IsNullOrEmpty(path) ? null : new Uri(RequestBaseUrl(request), path);

    private Uri RequestBaseUrl(HttpRequest request) =>
        DeriveUrlsFromRequest && request.Host.HasValue ? new Uri($"{request.Scheme}://{request.Host}") : BaseUrl;
}

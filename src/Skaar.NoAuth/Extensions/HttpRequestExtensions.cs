using System.Diagnostics.CodeAnalysis;

namespace Skaar.NoAuth.Extensions;

internal static class HttpRequestExtensions
{
    private const string BearerPrefix = "Bearer ";

    /// <summary>True when the request path equals the configured path. An empty path never matches.</summary>
    public static bool IsPath(this HttpRequest request, string path) =>
        path.Length > 0 && request.Path.Equals(new PathString(path));

    public static bool TryReadBearerToken(this HttpRequest request, [NotNullWhen(true)] out string? token)
    {
        var authHeader = request.Headers.Authorization.ToString();
        if (authHeader.Length > BearerPrefix.Length &&
            authHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader[BearerPrefix.Length..].Trim();
            return true;
        }

        token = null;
        return false;
    }
}

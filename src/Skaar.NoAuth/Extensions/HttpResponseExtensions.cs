using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Extensions;
using Skaar.NoAuth.Exceptions;
using Skaar.NoAuth.Serialization;

namespace Skaar.NoAuth.Extensions;

internal static class HttpResponseExtensions
{
    public static Task Serialize<T>(this HttpResponse response,
        T payload,
        JsonSerializerOptions? options = null,
        string contentType = "application/json") =>
        response.WriteAsJsonAsync(payload, options ?? JsonOptions.SnakeCase, contentType);

    /// <summary>
    /// Redirects back to the client with the authorization response parameters, and the issuer (RFC 9207).
    /// </summary>
    public static void RedirectToClient(this HttpResponse response, string redirectUri, string issuer,
        string? state, params IEnumerable<(string Key, string Value)> parameters)
    {
        var uri = new UriBuilder(ParseAbsoluteUri(redirectUri, "redirect_uri"));
        var query = new QueryBuilder();
        query.AddFromQuery(uri.Query);
        foreach (var (key, value) in parameters) query.Add(key, value);
        query.AddIfNotNullOrWhiteSpace("state", state);
        query.AddIfNotNullOrWhiteSpace("iss", issuer);
        uri.Query = query.ToString();
        response.Redirect(uri.Uri.AbsoluteUri);
    }

    /// <summary>
    /// Parses an absolute uri. File uris are rejected, also because a path like <c>/callback</c>
    /// is parsed as an absolute file uri on Linux and macOS.
    /// </summary>
    public static Uri ParseAbsoluteUri(string uri, string parameterName) =>
        TryParseAbsoluteUri(uri, out var result)
            ? result
            : throw new StatusCodeException(HttpStatusCode.BadRequest, $"{parameterName} must be an absolute uri");

    public static bool TryParseAbsoluteUri(string uri, [NotNullWhen(true)] out Uri? result) =>
        Uri.TryCreate(uri, UriKind.Absolute, out result) && !result.IsFile;
}

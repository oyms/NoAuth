using Skaar.NoAuth.Config;
using Skaar.NoAuth.Exceptions;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Services;

/// <summary>
/// Reads and validates the parameters of an authorization request.
/// </summary>
internal static class AuthorizationRequestReader
{
    /// <summary>
    /// Reads the client and redirect uri. Errors here cannot be redirected to the client.
    /// </summary>
    public static (string ClientId, string RedirectUri) ReadClient(RequestParameters parameters)
    {
        var clientId = parameters.GetRequired("client_id");
        var redirectUri = parameters.GetRequired("redirect_uri");
        HttpResponseExtensions.ParseAbsoluteUri(redirectUri, "redirect_uri");
        return (clientId, redirectUri);
    }

    /// <summary>
    /// Reads the rest of the request, for a client already read with <see cref="ReadClient"/>.
    /// Throws <see cref="OAuthException"/>, to be redirected to the client.
    /// </summary>
    public static AuthorizationRequest Read(RequestParameters parameters, Configuration config, string clientId,
        string redirectUri)
    {
        var responseType = parameters.GetOptional("response_type");
        if (responseType != "code")
            throw new OAuthException(OAuthException.UnsupportedResponseType,
                $"Unsupported response_type '{responseType}'. Only 'code' is supported.");

        var codeChallenge = parameters.GetOptional("code_challenge");
        var codeChallengeMethod = codeChallenge is null
            ? null
            : parameters.GetOptional("code_challenge_method") ?? Pkce.Plain;
        if (codeChallengeMethod is not null && !Pkce.IsSupportedMethod(codeChallengeMethod))
            throw new OAuthException(OAuthException.InvalidRequest,
                $"Unsupported code_challenge_method '{codeChallengeMethod}'");

        var resources = parameters.GetAll("resource");

        if (config.Strict)
        {
            if (codeChallenge is null || codeChallengeMethod != Pkce.S256)
                throw new OAuthException(OAuthException.InvalidRequest,
                    "PKCE is required: provide code_challenge with code_challenge_method=S256");
            if (!Pkce.IsWellFormed(codeChallenge))
                throw new OAuthException(OAuthException.InvalidRequest, "Malformed code_challenge");
            ValidateResources(resources);
        }

        return new AuthorizationRequest(
            clientId,
            redirectUri,
            parameters.GetOptional("state"),
            parameters.GetOptional("nonce"),
            parameters.GetOptional("scope").ParseScopeString(),
            resources,
            codeChallenge,
            codeChallengeMethod);
    }

    /// <summary>Resource indicators must be absolute uris without a fragment (RFC 8707).</summary>
    public static void ValidateResources(IEnumerable<string> resources)
    {
        foreach (var resource in resources)
        {
            if (!HttpResponseExtensions.TryParseAbsoluteUri(resource, out var uri) || uri.Fragment.Length > 0)
                throw new OAuthException(OAuthException.InvalidTarget,
                    $"The resource '{resource}' must be an absolute uri without a fragment");
        }
    }
}

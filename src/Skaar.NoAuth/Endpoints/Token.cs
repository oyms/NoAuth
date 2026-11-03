using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Exceptions;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Models;
using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Endpoints;

/// <summary>
/// The token endpoint. Supports the client_credentials, authorization_code and refresh_token grants.
/// </summary>
internal sealed class Token(Configuration config, ITokenRepository tokens, IWriter @out) : IEndpoint
{
    private static readonly TimeSpan StrictCodeLifetime = TimeSpan.FromMinutes(10);

    public bool IsMatch(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) && config.Paths.Token.Any(request.IsPath);

    public async Task Process(HttpContext context)
    {
        var parameters = await RequestParameters.Read(context);
        var grantType = parameters.GetOptional("grant_type") ?? "client_credentials";
        var tokenSet = grantType switch
        {
            "client_credentials" => new TokenSet(GetTokenFromClientCredentials(parameters, context.Request)),
            "authorization_code" => RedeemAuthorizationCode(parameters, context.Request),
            "refresh_token" => Refresh(parameters, context.Request),
            _ => throw new OAuthException(OAuthException.UnsupportedGrantType,
                $"Grant type {grantType} is not supported")
        };

        context.Response.Headers.CacheControl = "no-store";
        await context.Response.Serialize(new
        {
            AccessToken = tokenSet.AccessToken.ToString(),
            TokenType = "Bearer",
            ExpiresIn = (long)Math.Floor((tokenSet.AccessToken.Expires - DateTimeOffset.Now).TotalSeconds),
            Scope = tokenSet.AccessToken.ScopeString,
            IdToken = tokenSet.IdToken?.ToString(),
            tokenSet.AccessToken.RefreshToken
        });
    }

    private TokenSet RedeemAuthorizationCode(RequestParameters parameters, HttpRequest request)
    {
        var requestedCode = parameters.GetRequired("code");
        var grant = (AuthorizationCode.TryParse(requestedCode, out var code)
                        ? tokens.FindAuthorizationGrant(code)
                        : null)
                    ?? throw new OAuthException(OAuthException.InvalidGrant, $"Unknown code {requestedCode}");
        var authorization = grant.Request;

        var codeVerifier = parameters.GetOptional("code_verifier");
        if (authorization.CodeChallenge is not null)
        {
            if (codeVerifier is null)
                throw new OAuthException(OAuthException.InvalidGrant, "Missing code_verifier");
            if (!Pkce.Verify(codeVerifier, authorization.CodeChallenge, authorization.CodeChallengeMethod!))
                throw new OAuthException(OAuthException.InvalidGrant, "The code_verifier does not match the code_challenge");
        }

        var resources = parameters.GetAll("resource");

        if (config.Strict)
        {
            if (DateTimeOffset.Now - grant.IssuedAt > StrictCodeLifetime)
                throw new OAuthException(OAuthException.InvalidGrant, "The code has expired");
            if (authorization.CodeChallenge is null && codeVerifier is not null)
                throw new OAuthException(OAuthException.InvalidGrant, "A code_verifier was sent, but no code_challenge");
            var clientId = ReadClientCredentials(parameters, request)?.ClientId;
            if (clientId != authorization.ClientId)
                throw new OAuthException(OAuthException.InvalidGrant, "The code was issued to another client");
            if (parameters.GetOptional("redirect_uri") != authorization.RedirectUri)
                throw new OAuthException(OAuthException.InvalidGrant, "The redirect_uri does not match the authorization request");
            AuthorizationRequestReader.ValidateResources(resources);
        }

        if (!tokens.TryRedeem(grant) && config.Strict)
            throw new OAuthException(OAuthException.InvalidGrant, "The code has already been used");

        return tokens.IssueTokens(grant, resources);
    }

    private TokenSet Refresh(RequestParameters parameters, HttpRequest request)
    {
        var refreshToken = parameters.GetRequired("refresh_token");
        var existing = tokens.FindByRefreshToken(refreshToken)
                       ?? throw new OAuthException(OAuthException.InvalidGrant, "Unknown refresh_token");
        if (config.Strict && ReadClientCredentials(parameters, request)?.ClientId != existing.ClientId)
            throw new OAuthException(OAuthException.InvalidGrant, "The refresh_token was issued to another client");

        return tokens.Refresh(refreshToken)
               ?? throw new OAuthException(OAuthException.InvalidGrant, "Unknown refresh_token");
    }

    private AccessToken GetTokenFromClientCredentials(RequestParameters parameters, HttpRequest request)
    {
        var (clientId, clientSecret) = ReadClientCredentials(parameters, request)
                                       ?? throw new OAuthException(OAuthException.InvalidClient,
                                           "Missing client_id", HttpStatusCode.Unauthorized);
        var resources = parameters.GetAll("resource");
        if (config.Strict) AuthorizationRequestReader.ValidateResources(resources);
        if (parameters.GetOptional("audience") is { } audience) resources.Add(audience);

        return tokens.GetAccessToken(new TokenRequestParameters(
            parameters.GetOptional("scope").ParseScopeString(),
            clientId,
            clientSecret,
            resources.Distinct().ToList()));
    }

    private (string ClientId, string? ClientSecret)? ReadClientCredentials(RequestParameters parameters,
        HttpRequest request)
    {
        if (parameters.GetOptional("client_id") is { } clientId)
            return (clientId, parameters.GetOptional("client_secret"));

        foreach (var headerValue in request.Headers.Authorization)
        {
            if (!AuthenticationHeaderValue.TryParse(headerValue, out var parsed) ||
                !parsed.Scheme.Equals("basic", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(parsed.Parameter))
                continue;

            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(parsed.Parameter));
                var parts = decoded.Split(':', 2, StringSplitOptions.TrimEntries)
                    .Select(Uri.UnescapeDataString)
                    .ToArray();
                if (parts[0].Length > 0) return (parts[0], parts.ElementAtOrDefault(1));
            }
            catch (FormatException e)
            {
                @out.WriteWarning($"Error parsing Authorization header: {e.Message}");
            }
        }

        return null;
    }
}

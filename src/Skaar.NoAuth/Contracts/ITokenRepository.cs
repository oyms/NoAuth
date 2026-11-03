using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Contracts;

public interface ITokenRepository
{
    IEnumerable<AccessToken> AccessTokens { get; }
    IEnumerable<IdToken> IdTokens { get; }
    IEnumerable<AuthorizationGrant> AuthorizationGrants { get; }

    /// <summary>
    /// Returns an existing, active client token matching the request, or issues a new one.
    /// A new token is always issued when the request has a ttl.
    /// </summary>
    AccessToken GetAccessToken(TokenRequestParameters request);

    /// <summary>The access token, if it exists and has not expired.</summary>
    AccessToken? FindActiveAccessToken(string token);

    AccessToken? FindByRefreshToken(string refreshToken);

    /// <summary>Issues new tokens (and a new refresh token) for a refresh token. Null if not found.</summary>
    TokenSet? Refresh(string refreshToken);

    void ClearAccessTokens(string clientId);

    /// <summary>Revokes an access token or a refresh token (RFC 7009).</summary>
    bool Revoke(string token);

    AuthorizationCode CreateAuthorizationCode(AuthorizationRequest request, UserProfile user);
    AuthorizationGrant? FindAuthorizationGrant(AuthorizationCode code);

    /// <summary>Marks the code as redeemed. False if it was already redeemed (also by a concurrent request).</summary>
    bool TryRedeem(AuthorizationGrant grant);

    /// <summary>Issues tokens for an authorization code.</summary>
    TokenSet IssueTokens(AuthorizationGrant grant, IReadOnlyCollection<string> resources);

    /// <summary>Removes id tokens for the subject of the id token hint. Returns the subject, if any.</summary>
    string? RemoveIdTokens(string idTokenHint);

    void FlushAll();
}

using System.Collections.Concurrent;
using Microsoft.IdentityModel.JsonWebTokens;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Services;

/// <summary>
/// Keeps track of issued tokens and codes in memory. Tokens are created by <see cref="TokenIssuer"/>.
/// Tokens and codes are removed <see cref="Retention"/> after they expire (or are issued, for codes).
/// </summary>
internal sealed class TokenRepository(TokenIssuer issuer, TimeProvider time, IWriter @out) : ITokenRepository
{
    /// <summary>
    /// How long expired tokens are kept. Refresh tokens can be used until then.
    /// </summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<string, AccessToken> accessTokens = new();
    private readonly ConcurrentDictionary<string, IdToken> idTokens = new();
    private readonly ConcurrentDictionary<AuthorizationCode, AuthorizationGrant> grants = new();

    public IEnumerable<AccessToken> AccessTokens => accessTokens.Values;
    public IEnumerable<IdToken> IdTokens => idTokens.Values;
    public IEnumerable<AuthorizationGrant> AuthorizationGrants => grants.Values;

    public AccessToken GetAccessToken(TokenRequestParameters request)
    {
        Prune();
        var scopes = issuer.ResolveScopes(request.ClientId, request.Scopes);
        var existing = request.Ttl is not null ? null : accessTokens.Values.FirstOrDefault(t =>
            t.User is null &&
            t.ClientId == request.ClientId &&
            t.Audiences.ToHashSet().SetEquals(request.Audiences) &&
            t.Scopes.ToHashSet().SetEquals(scopes));

        if (existing is not null)
        {
            if (existing.IsActiveAt(time.GetUtcNow()))
            {
                @out.WriteWarning($"Existing token found for {existing.ClientId}.");
                return existing;
            }

            accessTokens.TryRemove(existing.Token, out _);
            @out.WriteInfo($"Token expired for {existing.ClientId}.");
        }

        var token = Store(issuer.CreateAccessToken(request.ClientId, request.Audiences, scopes, null,
            request.Ttl ?? issuer.DefaultTtl));
        @out.WriteInfo($"Issuing a new token for {request.ClientId}.");
        return token;
    }

    public AccessToken? FindActiveAccessToken(string token) =>
        accessTokens.GetValueOrDefault(token) is { } found && found.IsActiveAt(time.GetUtcNow()) ? found : null;

    public AccessToken? FindByRefreshToken(string refreshToken) =>
        accessTokens.Values.FirstOrDefault(t => t.RefreshToken == refreshToken);

    public TokenSet? Refresh(string refreshToken)
    {
        var existing = FindByRefreshToken(refreshToken);
        if (existing is null || !accessTokens.TryRemove(existing.Token, out _)) return null;

        var token = Store(issuer.CreateAccessToken(existing.ClientId, existing.Audiences, existing.Scopes,
            existing.User, issuer.DefaultTtl));
        var idToken = existing.User is null
            ? null
            : Store(issuer.CreateIdToken(existing.ClientId, existing.User, null, token, null));
        @out.WriteInfo($"Issuing a refreshed token for {token.ClientId}.");
        return new TokenSet(token, idToken);
    }

    public void ClearAccessTokens(string clientId)
    {
        foreach (var token in accessTokens.Values.Where(t => t.ClientId == clientId))
            accessTokens.TryRemove(token.Token, out _);
    }

    public bool Revoke(string token)
    {
        var existing = accessTokens.GetValueOrDefault(token) ?? FindByRefreshToken(token);
        if (existing is null || !accessTokens.TryRemove(existing.Token, out _)) return false;
        @out.WriteInfo($"Revoked token for {existing.ClientId}.");
        return true;
    }

    public AuthorizationCode CreateAuthorizationCode(AuthorizationRequest request, UserProfile user)
    {
        Prune();
        var code = AuthorizationCode.NewCode();
        grants[code] = new AuthorizationGrant(code, request, user) { IssuedAt = time.GetUtcNow() };
        @out.WriteInfo($"Issuing authorization code {code} for {request.ClientId} and user {user.Uid}.");
        return code;
    }

    public AuthorizationGrant? FindAuthorizationGrant(AuthorizationCode code) =>
        code.IsValid ? grants.GetValueOrDefault(code) : null;

    public bool TryRedeem(AuthorizationGrant grant) =>
        !grant.Redeemed && grants.TryUpdate(grant.Code, grant with { Redeemed = true }, grant);

    public TokenSet IssueTokens(AuthorizationGrant grant, IReadOnlyCollection<string> resources)
    {
        var request = grant.Request;
        var accessToken = Store(issuer.CreateAccessToken(
            request.ClientId,
            resources.Count > 0 ? resources : request.Resources,
            issuer.ResolveScopes(request.ClientId, request.Scopes),
            grant.User,
            issuer.DefaultTtl));
        var idToken = Store(issuer.CreateIdToken(request.ClientId, grant.User, request.Nonce, accessToken, grant.Code));
        @out.WriteInfo($"Issuing tokens for code {grant.Code} to {request.ClientId}.");
        return new TokenSet(accessToken, idToken);
    }

    public string? RemoveIdTokens(string idTokenHint)
    {
        string subject;
        try
        {
            subject = new JsonWebToken(idTokenHint).Subject;
        }
        catch (ArgumentException)
        {
            @out.WriteWarning("The id_token_hint is not a valid jwt.");
            return null;
        }

        foreach (var (key, token) in idTokens)
            if (token.UserProfile.Uid.Equals(subject)) idTokens.TryRemove(key, out _);
        return subject;
    }

    public void FlushAll()
    {
        accessTokens.Clear();
        idTokens.Clear();
        grants.Clear();
    }

    private AccessToken Store(AccessToken token) => accessTokens[token.Token] = token;

    private IdToken Store(IdToken token) => idTokens[token.Jwt] = token;

    /// <summary>Removes tokens and codes older than <see cref="Retention"/>.</summary>
    private void Prune()
    {
        var limit = time.GetUtcNow() - Retention;
        foreach (var (key, token) in accessTokens)
            if (token.Expires < limit) accessTokens.TryRemove(key, out _);
        foreach (var (key, token) in idTokens)
            if (token.Expires < limit) idTokens.TryRemove(key, out _);
        foreach (var (code, grant) in grants)
            if (grant.IssuedAt < limit) grants.TryRemove(code, out _);
    }
}

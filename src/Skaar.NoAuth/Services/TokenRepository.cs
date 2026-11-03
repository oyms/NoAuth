using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Services;

/// <summary>
/// Issues and keeps track of tokens in memory.
/// Tokens and codes are removed <see cref="Retention"/> after they expire (or are issued, for codes).
/// </summary>
internal sealed class TokenRepository(
    IJwtBuilderFactory jwtBuilder,
    Configuration config,
    IProfileStore profiles,
    IWriter @out) : ITokenRepository
{
    private const string ScopeClaim = "scope";

    /// <summary>
    /// How long expired tokens are kept. Refresh tokens can be used until then.
    /// </summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    /// <summary>The <c>typ</c> of JWT access tokens (RFC 9068).</summary>
    private const string AccessTokenType = "at+jwt";

    private readonly ConcurrentDictionary<string, AccessToken> accessTokens = new();
    private readonly ConcurrentDictionary<string, IdToken> idTokens = new();
    private readonly ConcurrentDictionary<AuthorizationCode, AuthorizationGrant> grants = new();

    public IEnumerable<AccessToken> AccessTokens => accessTokens.Values;
    public IEnumerable<IdToken> IdTokens => idTokens.Values;
    public IEnumerable<AuthorizationGrant> AuthorizationGrants => grants.Values;

    private TimeSpan DefaultTtl => TimeSpan.FromSeconds(config.TokenTtlInSeconds);

    public AccessToken GetAccessToken(TokenRequestParameters request)
    {
        Prune();
        var scopes = ResolveScopes(request.ClientId, request.Scopes);
        var existing = request.Ttl is not null ? null : accessTokens.Values.FirstOrDefault(t =>
            t.User is null &&
            t.ClientId == request.ClientId &&
            t.Audiences.ToHashSet().SetEquals(request.Audiences) &&
            t.Scopes.ToHashSet().SetEquals(scopes));

        if (existing is not null)
        {
            if (existing.IsActive)
            {
                @out.WriteWarning($"Existing token found for {existing.ClientId}.");
                return existing;
            }

            accessTokens.TryRemove(existing.Token, out _);
            @out.WriteInfo($"Token expired for {existing.ClientId}.");
        }

        var token = CreateAccessToken(request.ClientId, request.Audiences, scopes, null, request.Ttl ?? DefaultTtl);
        @out.WriteInfo($"Issuing a new token for {request.ClientId}.");
        return token;
    }

    public AccessToken? FindAccessToken(string token) => accessTokens.GetValueOrDefault(token);

    public AccessToken? FindByRefreshToken(string refreshToken) =>
        accessTokens.Values.FirstOrDefault(t => t.RefreshToken == refreshToken);

    public TokenSet? Refresh(string refreshToken)
    {
        var existing = FindByRefreshToken(refreshToken);
        if (existing is null || !accessTokens.TryRemove(existing.Token, out _)) return null;

        var token = CreateAccessToken(existing.ClientId, existing.Audiences, existing.Scopes, existing.User,
            DefaultTtl);
        var idToken = existing.User is null ? null : CreateIdToken(existing.ClientId, existing.User, null, token, null);
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
        grants[code] = new AuthorizationGrant(code, request, user);
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
        var accessToken = CreateAccessToken(
            request.ClientId,
            resources.Count > 0 ? resources : request.Resources,
            ResolveScopes(request.ClientId, request.Scopes),
            grant.User,
            DefaultTtl);
        var idToken = CreateIdToken(request.ClientId, grant.User, request.Nonce, accessToken, grant.Code);
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

    /// <summary>Removes tokens and codes older than <see cref="Retention"/>.</summary>
    private void Prune()
    {
        var limit = DateTimeOffset.Now - Retention;
        foreach (var (key, token) in accessTokens)
            if (token.Expires < limit) accessTokens.TryRemove(key, out _);
        foreach (var (key, token) in idTokens)
            if (token.Expires < limit) idTokens.TryRemove(key, out _);
        foreach (var (code, grant) in grants)
            if (grant.IssuedAt < limit) grants.TryRemove(code, out _);
    }

    /// <summary>The requested scopes, and the scopes predefined for the client.</summary>
    private IReadOnlyCollection<string> ResolveScopes(string clientId, IEnumerable<string> requested) =>
        requested.Union(PredefinedClaims(clientId).TryGetValue(ScopeClaim, out var scopes) ? scopes : []).ToList();

    private IDictionary<string, IEnumerable<string>> PredefinedClaims(string clientId) =>
        profiles.TryGetClient(clientId, out var client) ? client.Claims : new Dictionary<string, IEnumerable<string>>();

    private AccessToken CreateAccessToken(string clientId, IReadOnlyCollection<string> audiences,
        IReadOnlyCollection<string> scopes, UserProfile? user, TimeSpan ttl)
    {
        var token = new AccessToken(clientId, string.Empty)
        {
            Scopes = scopes,
            Audiences = audiences,
            User = user,
            Expires = DateTimeOffset.Now + ttl
        };
        token = token with { Token = CreateAccessTokenJwt(token) };
        accessTokens[token.Token] = token;
        return token;
    }

    /// <summary>A JWT access token as profiled in RFC 9068, or a reference token.</summary>
    private string CreateAccessTokenJwt(AccessToken token)
    {
        if (config.UseReferenceTokens) return Guid.NewGuid().ToString();
        var builder = jwtBuilder.NewBuilder()
            .Type(AccessTokenType)
            .Aud(token.Audiences.Count > 0 ? token.Audiences : [token.ClientId])
            .Sub(token.Subject)
            .Claim(new Claim("client_id", token.ClientId))
            .Exp(token.Expires);
        foreach (var scope in token.Scopes) builder.Scope(scope);
        foreach (var claim in token.User?.AllClaims() ?? []) builder.Claim(claim);
        foreach (var (type, values) in PredefinedClaims(token.ClientId).Where(c => !ReservedClaims.IsReserved(c.Key)))
        foreach (var value in values)
            builder.Claim(new Claim(type, value));
        return builder.Build();
    }

    private IdToken CreateIdToken(string clientId, UserProfile user, string? nonce, AccessToken accessToken,
        AuthorizationCode? code)
    {
        var expires = DateTimeOffset.Now + DefaultTtl;
        var builder = jwtBuilder.NewBuilder()
            .Aud(clientId)
            .Sub(user.Uid)
            .Exp(expires);
        foreach (var claim in user.AllClaims()) builder.Claim(claim);
        if (nonce is not null) builder.Claim(new Claim("nonce", nonce));

        var idToken = new IdToken(user, clientId, nonce, builder.Build())
        {
            Expires = expires,
            AccessToken = accessToken.Token,
            Code = code
        };
        idTokens[idToken.Jwt] = idToken;
        return idToken;
    }
}

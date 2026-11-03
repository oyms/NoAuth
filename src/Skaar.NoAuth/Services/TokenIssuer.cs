using System.Security.Claims;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Services;

/// <summary>
/// Creates access tokens and id tokens, including the claims predefined for the client.
/// Issued tokens are kept by <see cref="TokenRepository"/>.
/// </summary>
internal sealed class TokenIssuer(
    IJwtBuilderFactory jwtBuilder,
    Configuration config,
    IProfileStore profiles,
    TimeProvider time)
{
    private const string ScopeClaim = "scope";

    /// <summary>The <c>typ</c> of JWT access tokens (RFC 9068).</summary>
    private const string AccessTokenType = "at+jwt";

    public TimeSpan DefaultTtl => TimeSpan.FromSeconds(config.TokenTtlInSeconds);

    /// <summary>The requested scopes, and the scopes predefined for the client.</summary>
    public IReadOnlyCollection<string> ResolveScopes(string clientId, IEnumerable<string> requested) =>
        requested.Union(PredefinedClaims(clientId).TryGetValue(ScopeClaim, out var scopes) ? scopes : []).ToList();

    public AccessToken CreateAccessToken(string clientId, IReadOnlyCollection<string> audiences,
        IReadOnlyCollection<string> scopes, UserProfile? user, TimeSpan ttl)
    {
        var now = time.GetUtcNow();
        var token = new AccessToken(clientId, string.Empty)
        {
            Scopes = scopes,
            Audiences = audiences,
            User = user,
            IssuedAt = now,
            Expires = now + ttl
        };
        return token with { Token = CreateAccessTokenJwt(token) };
    }

    public IdToken CreateIdToken(string clientId, UserProfile user, string? nonce, AccessToken accessToken,
        AuthorizationCode? code)
    {
        var now = time.GetUtcNow();
        var expires = now + DefaultTtl;
        var builder = jwtBuilder.NewBuilder()
            .Aud(clientId)
            .Sub(user.Uid)
            .Exp(expires);
        foreach (var claim in user.AllClaims()) builder.Claim(claim);
        if (nonce is not null) builder.Claim(new Claim("nonce", nonce));

        return new IdToken(user, clientId, nonce, builder.Build())
        {
            Iat = now,
            Expires = expires,
            AccessToken = accessToken.Token,
            Code = code
        };
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

    private IDictionary<string, IEnumerable<string>> PredefinedClaims(string clientId) =>
        profiles.TryGetClient(clientId, out var client) ? client.Claims : new Dictionary<string, IEnumerable<string>>();
}

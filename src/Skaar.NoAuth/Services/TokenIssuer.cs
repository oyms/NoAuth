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
    EntraClaims entra,
    TimeProvider time)
{
    private const string ScopeClaim = "scope";

    /// <summary>The <c>typ</c> of JWT access tokens (RFC 9068).</summary>
    private const string AccessTokenType = "at+jwt";

    public TimeSpan DefaultTtl => TimeSpan.FromSeconds(config.TokenTtlInSeconds);

    /// <summary>The requested scopes, and the scopes predefined for the client.</summary>
    public IReadOnlyCollection<string> ResolveScopes(string clientId, IEnumerable<string> requested) =>
        requested.Union(PredefinedClaims(clientId).TryGetValue(ScopeClaim, out var scopes) ? scopes : []).ToList();

    /// <summary>The requested audiences. With Entra, audiences are otherwise given by the scopes.</summary>
    public IReadOnlyCollection<string> ResolveAudiences(IReadOnlyCollection<string> requested,
        IEnumerable<string> scopes) =>
        requested.Count == 0 && entra.Enabled ? entra.Audiences(scopes) : requested;

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
        if (entra.Enabled) AddDefaults(builder, entra.IdTokenClaims(user), user.AllClaims());

        return new IdToken(user, clientId, nonce, builder.Build())
        {
            Iat = now,
            Expires = expires,
            AccessToken = accessToken.Token,
            Code = code
        };
    }

    /// <summary>
    /// A JWT access token as profiled in RFC 9068, an Entra v2.0 access token, or a reference token.
    /// </summary>
    private string CreateAccessTokenJwt(AccessToken token)
    {
        if (config.UseReferenceTokens) return Guid.NewGuid().ToString();
        var builder = jwtBuilder.NewBuilder()
            .Aud(token.Audiences.Count > 0 ? token.Audiences : [token.ClientId])
            .Sub(token.Subject)
            .Exp(token.Expires);
        if (!entra.Enabled)
        {
            builder.Type(AccessTokenType).Claim(new Claim("client_id", token.ClientId));
            foreach (var scope in token.Scopes) builder.Scope(scope);
        }

        var claims = (token.User?.AllClaims() ?? [])
            .Concat(PredefinedClaims(token.ClientId)
                .Where(c => !ReservedClaims.IsReserved(c.Key))
                .SelectMany(c => c.Value.Select(value => new Claim(c.Key, value))))
            .ToList();
        foreach (var claim in claims) builder.Claim(claim);
        if (entra.Enabled) AddDefaults(builder, entra.AccessTokenClaims(token), claims);
        return builder.Build();
    }

    /// <summary>Adds claims of types not already given by the user or client profile.</summary>
    private static void AddDefaults(IJwtBuilder builder, IEnumerable<Claim> defaults, IEnumerable<Claim> given)
    {
        var givenTypes = given.Select(c => c.Type).ToHashSet();
        foreach (var claim in defaults.Where(c => !givenTypes.Contains(c.Type) || ReservedClaims.IsReserved(c.Type)))
            builder.Claim(claim);
    }

    private IDictionary<string, IEnumerable<string>> PredefinedClaims(string clientId) =>
        profiles.TryGetClient(clientId, out var client) ? client.Claims : new Dictionary<string, IEnumerable<string>>();
}

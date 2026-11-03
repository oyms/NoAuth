namespace Skaar.NoAuth.Models;

public record IdToken(UserProfile UserProfile, string ClientId, string? Nonce, string Jwt)
{
    public DateTimeOffset Expires { get; init; }
    public DateTimeOffset Iat { get; init; }

    /// <summary>The access token issued together with this id token.</summary>
    public string? AccessToken { get; init; }

    /// <summary>The code this token was issued for. Null when issued by a refresh.</summary>
    public AuthorizationCode? Code { get; init; }

    public override string ToString() => Jwt;
}

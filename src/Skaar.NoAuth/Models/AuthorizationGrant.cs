namespace Skaar.NoAuth.Models;

/// <summary>
/// An issued authorization code, waiting to be exchanged for tokens.
/// </summary>
public record AuthorizationGrant(AuthorizationCode Code, AuthorizationRequest Request, UserProfile User)
{
    public DateTimeOffset IssuedAt { get; init; }
    public bool Redeemed { get; init; }
}

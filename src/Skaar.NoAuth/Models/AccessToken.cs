namespace Skaar.NoAuth.Models;

public record AccessToken(string ClientId, string Token)
{
    public string RefreshToken { get; init; } = Guid.NewGuid().ToString();
    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset Expires { get; init; }
    public IReadOnlyCollection<string> Scopes { get; init; } = [];
    public IReadOnlyCollection<string> Audiences { get; init; } = [];

    /// <summary>The logged in user, for tokens issued with the authorization code flow.</summary>
    public UserProfile? User { get; init; }

    public bool IsActive => Expires > DateTimeOffset.Now;
    public string ScopeString => string.Join(' ', Scopes);

    /// <summary>The token subject: the user for user tokens, otherwise the client (RFC 9068).</summary>
    public string Subject => User?.Uid ?? ClientId;

    public override string ToString() => Token;
}

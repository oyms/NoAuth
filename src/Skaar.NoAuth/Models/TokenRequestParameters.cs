namespace Skaar.NoAuth.Models;

public record TokenRequestParameters(
    IReadOnlyCollection<string> Scopes,
    string ClientId,
    string? ClientSecret,
    IReadOnlyCollection<string> Audiences,
    TimeSpan? Ttl = null);

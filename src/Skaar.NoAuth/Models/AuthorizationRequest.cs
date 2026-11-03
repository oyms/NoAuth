namespace Skaar.NoAuth.Models;

/// <summary>
/// The parameters of a request to the authorization endpoint.
/// </summary>
public record AuthorizationRequest(
    string ClientId,
    string RedirectUri,
    string? State,
    string? Nonce,
    IReadOnlyCollection<string> Scopes,
    IReadOnlyCollection<string> Resources,
    string? CodeChallenge,
    string? CodeChallengeMethod);

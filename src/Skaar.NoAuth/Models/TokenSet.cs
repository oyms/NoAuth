namespace Skaar.NoAuth.Models;

/// <summary>
/// The tokens returned from the token endpoint.
/// </summary>
public record TokenSet(AccessToken AccessToken, IdToken? IdToken = null);

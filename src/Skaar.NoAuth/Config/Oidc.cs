using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Config;

public record Oidc
{
    /// <summary>
    /// With <see cref="OidcStrategy.GenerateRandom"/>: remember the generated user in a session cookie,
    /// so later logins in the same browser get the same user.
    /// </summary>
    public bool RememberUser { get; set; }
    public OidcStrategy Strategy { get; set; }
}

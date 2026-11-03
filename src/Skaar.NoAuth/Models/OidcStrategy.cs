namespace Skaar.NoAuth.Models;

public enum OidcStrategy
{
    /// <summary>Let the user pick or fill in a profile in a login form.</summary>
    WebForm = 0,

    /// <summary>Log in with a random generated user, without user interaction.</summary>
    GenerateRandom = 1
}

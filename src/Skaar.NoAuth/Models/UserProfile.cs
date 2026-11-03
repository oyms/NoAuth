using System.Security.Claims;

namespace Skaar.NoAuth.Models;

/// <summary>
/// A logged in user. Generated users get a valid fødselsnummer as <see cref="Uid"/>, but any value is accepted.
/// </summary>
public record UserProfile(string Uid, string GivenName, string FamilyName)
{
    public IEnumerable<ClaimModel> ExtraClaims { get; init; } = [];

    public string Name => $"{GivenName} {FamilyName}";

    public IEnumerable<Claim> AllClaims()
    {
        yield return new Claim("uid", Uid);
        yield return new Claim("given_name", GivenName);
        yield return new Claim("family_name", FamilyName);
        yield return new Claim("name", Name);
        foreach (var extraClaim in ExtraClaims) yield return extraClaim;
    }
}

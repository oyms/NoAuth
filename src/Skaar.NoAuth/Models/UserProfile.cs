using System.Security.Claims;

namespace Skaar.NoAuth.Models;

public record UserProfile(Fodselsnummer Uid, string GivenName, string FamilyName)
{
    public IEnumerable<ClaimModel> ExtraClaims { get; init; } = [];

    public string Name => $"{GivenName} {FamilyName}";

    public IEnumerable<Claim> AllClaims()
    {
        yield return new Claim("uid", Uid.ToString());
        yield return new Claim("given_name", GivenName);
        yield return new Claim("family_name", FamilyName);
        yield return new Claim("name", Name);
        foreach (var extraClaim in ExtraClaims) yield return extraClaim;
    }
}

using System.Security.Claims;

namespace Skaar.NoAuth.Models;

public record ClaimModel(string Type, string Value)
{
    public static implicit operator Claim(ClaimModel model) => new(model.Type, model.Value);
    public static explicit operator ClaimModel(Claim claim) => new(claim.Type, claim.Value);
}

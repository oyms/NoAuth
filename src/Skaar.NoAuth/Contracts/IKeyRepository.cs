using Microsoft.IdentityModel.Tokens;
using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Contracts;

public interface IKeyRepository
{
    SigningCredentials SigningCredentials { get; }
    IEnumerable<JsonWebKeyModel> PublicKeys { get; }
}

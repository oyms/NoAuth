using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Contracts;

public interface IUserDataFactory
{
    UserProfile GenerateRandomProfile();
}

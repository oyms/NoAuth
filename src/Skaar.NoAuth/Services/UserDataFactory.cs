using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Models;
using Skaar.NoAuth.Services.DataGeneration;

namespace Skaar.NoAuth.Services;

internal sealed class UserDataFactory(PersonDataGenerator dataGenerator) : IUserDataFactory
{
    public UserProfile GenerateRandomProfile()
    {
        var fodselsnummer = dataGenerator.GenerateRandomFodselsnummer();
        var (firstName, familyName) = dataGenerator.GenerateName(fodselsnummer);
        return new UserProfile(fodselsnummer.ToString(), firstName, familyName);
    }
}

using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Services.DataGeneration;

public class PersonDataGenerator
{
    public Fodselsnummer GenerateRandomFodselsnummer(DateTime lowerBound, DateTime upperBound) =>
        Fodselsnummer.GenerateRandom(Random.Shared, lowerBound, upperBound);

    /// <summary>Generates an id for an adult aged 18 to 70 years.</summary>
    public Fodselsnummer GenerateRandomFodselsnummer()
    {
        var now = DateTime.Now;
        return GenerateRandomFodselsnummer(now.AddYears(-70), now.AddYears(-18));
    }

    public (string FirstName, string FamilyName) GenerateName(Fodselsnummer fodselsnummer)
    {
        var firstName = PickRandom(fodselsnummer.IsFemale ? Constants.Names.First.Female : Constants.Names.First.Male);
        return (firstName, PickRandom(Constants.Names.Last));
    }

    private static T PickRandom<T>(IReadOnlyList<T> items) => items[Random.Shared.Next(items.Count)];
}

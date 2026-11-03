using Skaar.Contracts;
using Skaar.Utils;

namespace Skaar.NoAuth.Services.DataGeneration;

public class PersonDataGenerator(TimeProvider time)
{
    private readonly IdNumberFactory idNumbers = new();

    public Fodselsnummer GenerateRandomFodselsnummer(DateOnly lowerBound, DateOnly upperBound)
    {
        var birthDate = lowerBound.AddDays(Random.Shared.Next(upperBound.DayNumber - lowerBound.DayNumber));
        var gender = Random.Shared.Next(2) == 0 ? Gender.Female : Gender.Male;
        return idNumbers.CreateRandomFodselsnummer(birthDate, gender);
    }

    /// <summary>Generates an id for an adult aged 18 to 70 years.</summary>
    public Fodselsnummer GenerateRandomFodselsnummer()
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        return GenerateRandomFodselsnummer(today.AddYears(-70), today.AddYears(-18));
    }

    public (string FirstName, string FamilyName) GenerateName(Fodselsnummer fodselsnummer)
    {
        var firstName = PickRandom(fodselsnummer.Gender == Gender.Female
            ? Constants.Names.First.Female
            : Constants.Names.First.Male);
        return (firstName, PickRandom(Constants.Names.Last));
    }

    private static T PickRandom<T>(IReadOnlyList<T> items) => items[Random.Shared.Next(items.Count)];
}

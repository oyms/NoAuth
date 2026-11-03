using Skaar.Contracts;
using Skaar.NoAuth.Services.DataGeneration;

namespace Skaar.NoAuth.Tests;

public class PersonDataGeneratorTests
{
    private readonly PersonDataGenerator generator = new(TimeProvider.System);

    [Fact]
    public void Generated_numbers_are_valid_and_within_the_bounds()
    {
        var lower = new DateOnly(1990, 1, 1);
        var upper = new DateOnly(2010, 1, 1);

        for (var i = 0; i < 1000; i++)
        {
            var number = generator.GenerateRandomFodselsnummer(lower, upper);

            number.IsValid.ShouldBeTrue(number.ToString());
            number.BirthDate.ShouldBeInRange(lower, upper);
        }
    }

    [Fact]
    public void Generated_users_are_adults()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        var number = generator.GenerateRandomFodselsnummer();

        number.BirthDate.ShouldBeInRange(today.AddYears(-70), today.AddYears(-18));
    }

    [Theory]
    [InlineData("12106346508", Gender.Male)]
    [InlineData("02029404855", Gender.Female)]
    public void The_first_name_matches_the_gender(string value, Gender gender)
    {
        var names = gender == Gender.Female ? Constants.Names.First.Female : Constants.Names.First.Male;

        var (firstName, _) = generator.GenerateName(Fodselsnummer.Parse(value, null));

        names.ShouldContain(firstName);
    }
}

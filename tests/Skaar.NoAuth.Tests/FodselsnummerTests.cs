using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Tests;

public class FodselsnummerTests
{
    [Fact]
    public void Generated_numbers_have_valid_control_digits()
    {
        var lower = new DateTime(1990, 1, 1);
        var upper = new DateTime(2010, 1, 1);

        for (var i = 0; i < 1000; i++)
        {
            var number = Fodselsnummer.GenerateRandom(Random.Shared, lower, upper).ToString();

            number.Length.ShouldBe(11);
            number.ShouldAllBe(c => char.IsAsciiDigit(c));
            ControlDigit(number, [3, 7, 6, 1, 8, 9, 4, 5, 2]).ShouldBe(number[9] - '0', number);
            ControlDigit(number, [5, 4, 3, 2, 7, 6, 5, 4, 3, 2]).ShouldBe(number[10] - '0', number);
        }
    }

    [Theory]
    [InlineData("12106346508", false)]
    [InlineData("02029404855", true)]
    [InlineData("1234567", false)]
    public void The_ninth_digit_gives_the_gender(string number, bool isFemale)
    {
        Fodselsnummer.TryParse(number, out var fodselsnummer).ShouldBeTrue();

        fodselsnummer.IsFemale.ShouldBe(isFemale);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Empty_values_are_not_parsed(string? value) =>
        Fodselsnummer.TryParse(value, out _).ShouldBeFalse();

    private static int ControlDigit(string number, int[] weights)
    {
        var sum = weights.Select((weight, i) => weight * (number[i] - '0')).Sum();
        return sum % 11 == 0 ? 0 : 11 - sum % 11;
    }
}

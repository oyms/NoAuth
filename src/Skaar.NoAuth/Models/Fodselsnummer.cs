using System.Text.Json.Serialization;
using Skaar.NoAuth.Serialization;

namespace Skaar.NoAuth.Models;

/// <summary>
/// A Norwegian national identity number (fødselsnummer), used as user id.
/// Any non-empty string is accepted as a value, but generated values are valid numbers.
/// </summary>
[JsonConverter(typeof(FodselsnummerJsonConverter))]
public readonly struct Fodselsnummer : IEquatable<Fodselsnummer>, IEquatable<string>
{
    private static readonly int[] FirstControlWeights = [3, 7, 6, 1, 8, 9, 4, 5, 2];
    private static readonly int[] SecondControlWeights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    private readonly string value;

    private Fodselsnummer(string value) => this.value = value;

    public bool IsFemale => value is { Length: > 8 } && char.IsDigit(value[8]) && (value[8] - '0') % 2 == 0;

    public bool Equals(Fodselsnummer other) => value == other.value;

    public bool Equals(string? other) => string.Equals(value, other);

    public override bool Equals(object? obj) => obj is Fodselsnummer other && Equals(other);

    public override int GetHashCode() => value?.GetHashCode() ?? 0;

    public static bool operator ==(Fodselsnummer left, Fodselsnummer right) => left.Equals(right);

    public static bool operator !=(Fodselsnummer left, Fodselsnummer right) => !left.Equals(right);

    public override string ToString() => value ?? string.Empty;

    public static bool TryParse(string? fodselsnummer, out Fodselsnummer result)
    {
        if (string.IsNullOrWhiteSpace(fodselsnummer))
        {
            result = default;
            return false;
        }

        result = new Fodselsnummer(fodselsnummer.Trim());
        return true;
    }

    public static Fodselsnummer GenerateRandom(Random rnd, DateTime lowerBound, DateTime upperBound)
    {
        while (true)
        {
            var birthdate = lowerBound + TimeSpan.FromDays(rnd.Next((upperBound - lowerBound).Days));
            var digits = $"{birthdate:ddMMyy}{rnd.Next(0, 1000):000}".Select(c => c - '0').ToList();
            if (!TryCalculateControlDigit(digits, FirstControlWeights, out var first)) continue;
            digits.Add(first);
            if (!TryCalculateControlDigit(digits, SecondControlWeights, out var second)) continue;
            digits.Add(second);
            return new Fodselsnummer(string.Concat(digits));
        }
    }

    private static bool TryCalculateControlDigit(IReadOnlyList<int> digits, int[] weights, out int control)
    {
        var sum = weights.Select((weight, i) => weight * digits[i]).Sum();
        control = (11 - sum % 11) % 11;
        return control != 10;
    }
}

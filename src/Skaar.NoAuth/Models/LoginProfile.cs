using System.Text.Json.Serialization;

namespace Skaar.NoAuth.Models;

/// <summary>
/// A template for user profiles to log in with.
/// </summary>
public record LoginProfile
{
    public required string ProfileName { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; }

    [JsonPropertyName("given_name")] public string? GivenName { get; init; }

    [JsonPropertyName("family_name")] public string? FamilyName { get; init; }

    public string? Uid { get; init; }
    public IDictionary<string, IEnumerable<string>> Claims { get; init; } = new Dictionary<string, IEnumerable<string>>();
}

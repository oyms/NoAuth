namespace Skaar.NoAuth.Extensions;

internal static class StringExtensions
{
    public static string WithFallback(this string? candidate, string fallback) =>
        string.IsNullOrWhiteSpace(candidate) ? fallback : candidate;

    public static IReadOnlyCollection<string> ParseScopeString(this string? scope) =>
        string.IsNullOrWhiteSpace(scope) ? [] : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}

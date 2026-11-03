using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;

namespace Skaar.NoAuth.Extensions;

internal static class QueryBuilderExtensions
{
    public static void AddIfNotNullOrWhiteSpace(this QueryBuilder builder, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) builder.Add(key, value);
    }

    public static void AddFromQuery(this QueryBuilder builder, string query)
    {
        foreach (var (key, values) in QueryHelpers.ParseQuery(query))
            builder.Add(key, values.Where(v => v is not null).Select(v => v!));
    }
}

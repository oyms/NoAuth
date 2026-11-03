using System.Text.Json;
using System.Text.Json.Serialization;

namespace Skaar.NoAuth.Serialization;

internal static class JsonOptions
{
    /// <summary>For OAuth/OIDC protocol responses.</summary>
    public static readonly JsonSerializerOptions SnakeCase = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>For the api and configuration files.</summary>
    public static readonly JsonSerializerOptions CamelCase = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

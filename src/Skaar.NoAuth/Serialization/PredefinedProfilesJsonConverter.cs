using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Serialization;

/// <summary>
/// Clients are serialized as an object keyed by client_id.
/// </summary>
internal class PredefinedProfilesJsonConverter : JsonConverter<PredefinedProfiles>
{
    public override PredefinedProfiles Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var json = JsonNode.Parse(ref reader)?.AsObject() ?? throw new JsonException("Expected an object");
        var logins = json["logins"].Deserialize<List<LoginProfile>>(options) ?? [];
        var clients = json["clients"].Deserialize<Dictionary<string, IDictionary<string, IEnumerable<string>>>>(options);
        return new PredefinedProfiles(
            logins,
            clients?.Select(c => new ClientProfile(c.Key, c.Value)).ToList() ?? []);
    }

    public override void Write(Utf8JsonWriter writer, PredefinedProfiles value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("logins");
        JsonSerializer.Serialize(writer, value.Logins, options);
        writer.WritePropertyName("clients");
        JsonSerializer.Serialize(writer, value.Clients.ToDictionary(c => c.Id, c => c.Claims), options);
        writer.WriteEndObject();
    }
}

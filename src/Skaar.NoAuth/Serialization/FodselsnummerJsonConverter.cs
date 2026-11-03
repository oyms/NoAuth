using System.Text.Json;
using System.Text.Json.Serialization;
using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Serialization;

internal class FodselsnummerJsonConverter : JsonConverter<Fodselsnummer>
{
    public override Fodselsnummer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Fodselsnummer.TryParse(reader.GetString(), out var result);
        return result;
    }

    public override void Write(Utf8JsonWriter writer, Fodselsnummer value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

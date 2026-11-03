using System.Text.Json.Serialization;

namespace Skaar.NoAuth.Models;

/// <summary>
/// A public key as published in the jwks document.
/// </summary>
public record JsonWebKeyModel(
    [property: JsonPropertyName("kty")] string Kty,
    [property: JsonPropertyName("use")] string Use,
    [property: JsonPropertyName("kid")] string Kid,
    [property: JsonPropertyName("x5t")] string X5t,
    [property: JsonPropertyName("e")] string E,
    [property: JsonPropertyName("n")] string N,
    [property: JsonPropertyName("alg")] string Alg,
    [property: JsonPropertyName("x5c")] IReadOnlyList<string> X5c);

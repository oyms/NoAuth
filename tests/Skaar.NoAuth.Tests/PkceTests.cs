using System.Security.Cryptography;
using System.Text;
using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Tests;

public class PkceTests
{
    [Fact]
    public void A_matching_S256_verifier_is_accepted()
    {
        var pkce = PkcePair.New();

        Pkce.Verify(pkce.Verifier, pkce.Challenge, Pkce.S256).ShouldBeTrue();
        Pkce.Verify(PkcePair.New().Verifier, pkce.Challenge, Pkce.S256).ShouldBeFalse();
    }

    [Fact]
    public void S256_is_base64url_encoded_sha256_without_padding()
    {
        const string verifier = "dBjftJeZ4CVP-mJ92IYn1kvL3aeBb1dL1hd1E1hk-ZQ";
        var expected = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Pkce.Verify(verifier, expected, Pkce.S256).ShouldBeTrue();
    }

    [Fact]
    public void Plain_compares_the_values()
    {
        Pkce.Verify("value", "value", Pkce.Plain).ShouldBeTrue();
        Pkce.Verify("value", "other", Pkce.Plain).ShouldBeFalse();
        Pkce.Verify("value", "value", "unknown").ShouldBeFalse();
    }

    [Theory]
    [InlineData(42, false)]
    [InlineData(43, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Verifiers_are_43_to_128_characters(int length, bool wellFormed) =>
        Pkce.IsWellFormed(new string('a', length)).ShouldBe(wellFormed);

    [Fact]
    public void Only_unreserved_characters_are_allowed() =>
        Pkce.IsWellFormed(new string('a', 42) + "+").ShouldBeFalse();
}

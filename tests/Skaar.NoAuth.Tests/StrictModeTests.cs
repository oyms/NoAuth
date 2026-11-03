using System.Net;

namespace Skaar.NoAuth.Tests;

public class StrictModeTests
{
    private static Task<NoAuthHost> StartStrict() => NoAuthHost.Start("--NoAuth:Strict=true");

    [Theory]
    [InlineData(null, null, "invalid_request")]
    [InlineData("plain", "plain-challenge-that-is-long-enough-to-be-well-formed-xx", "invalid_request")]
    public async Task Pkce_with_S256_is_required(string? method, string? challenge, string error)
    {
        await using var host = await StartStrict();
        (string, string)[] parameters = challenge is null
            ? []
            : [("code_challenge", challenge), ("code_challenge_method", method!)];

        var response = await host.Get(NoAuthHost.AuthorizeUri(parameters: parameters));

        NoAuthHost.Query(response.Headers.Location!)["error"].ShouldBe(error);
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("/relative")]
    [InlineData("https://api.local/#fragment")]
    public async Task Resources_must_be_absolute_uris_without_fragment(string resource)
    {
        await using var host = await StartStrict();

        var response = await host.Get(NoAuthHost.AuthorizeUri(PkcePair.New(), ("resource", resource)));

        NoAuthHost.Query(response.Headers.Location!)["error"].ShouldBe("invalid_target");
    }

    [Fact]
    public async Task Codes_are_single_use()
    {
        await using var host = await StartStrict();
        var pkce = PkcePair.New();
        var redirect = await host.LogIn(pkce);

        var first = await host.ExchangeCode(redirect["code"], pkce);
        var second = await host.ExchangeCode(redirect["code"], pkce);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await NoAuthHost.Json(second)).Str("error_description").ShouldBe("The code has already been used");
    }

    [Fact]
    public async Task Concurrent_requests_cannot_redeem_a_code_twice()
    {
        await using var host = await StartStrict();
        var pkce = PkcePair.New();
        var redirect = await host.LogIn(pkce);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => host.ExchangeCode(redirect["code"], pkce)));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_tokens_are_bound_to_the_client()
    {
        await using var host = await StartStrict();
        var pkce = PkcePair.New();
        var redirect = await host.LogIn(pkce);
        var tokens = await NoAuthHost.Json(await host.ExchangeCode(redirect["code"], pkce));

        var otherClient = await host.PostJson("/connect/token", ("grant_type", "refresh_token"),
            ("refresh_token", tokens.Str("refresh_token")), ("client_id", "other"));
        var sameClient = await host.Post("/connect/token", ("grant_type", "refresh_token"),
            ("refresh_token", tokens.Str("refresh_token")), ("client_id", "web"));

        otherClient.Str("error_description").ShouldBe("The refresh_token was issued to another client");
        sameClient.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("client_id", "other", "The code was issued to another client")]
    [InlineData("redirect_uri", "https://other.local/callback", "The redirect_uri does not match the authorization request")]
    public async Task The_token_request_must_match_the_authorization_request(string parameter, string value,
        string error)
    {
        await using var host = await StartStrict();
        var pkce = PkcePair.New();
        var redirect = await host.LogIn(pkce);
        (string, string)[] form = [("grant_type", "authorization_code"), ("code", redirect["code"]),
            ("code_verifier", pkce.Verifier), ("client_id", "web"), ("redirect_uri", NoAuthHost.RedirectUri)];

        var response = await host.PostJson("/connect/token",
            form.Select(f => f.Item1 == parameter ? (f.Item1, value) : f).ToArray());

        response.Str("error").ShouldBe("invalid_grant");
        response.Str("error_description").ShouldBe(error);
    }
}

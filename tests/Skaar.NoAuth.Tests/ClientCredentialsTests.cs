using System.Net;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Skaar.NoAuth.Tests;

public class ClientCredentialsTests
{
    [Fact]
    public async Task Issues_a_jwt_access_token_as_profiled_in_rfc_9068()
    {
        await using var host = await NoAuthHost.Start();

        var response = await host.ClientCredentials(
            ("client_id", "my_client"), ("scope", "read write"), ("resource", "https://api.local"));
        var jwt = new JsonWebToken(response.Str("access_token"));

        response.Str("token_type").ShouldBe("Bearer");
        jwt.Typ.ShouldBe("at+jwt");
        jwt.Issuer.ShouldBe(NoAuthHost.Issuer);
        jwt.Subject.ShouldBe("my_client");
        jwt.GetClaim("client_id").Value.ShouldBe("my_client");
        jwt.GetClaim("scope").Value.ShouldBe("read write");
        jwt.Audiences.ShouldBe(["https://api.local"]);
        jwt.Id.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task The_signature_validates_with_the_published_keys()
    {
        await using var host = await NoAuthHost.Start();
        var response = await host.ClientCredentials(("client_id", "my_client"));
        var jwksResponse = await host.Get("/.well-known/openid-configuration/jwks");
        var jwks = new JsonWebKeySet(await jwksResponse.Content.ReadAsStringAsync(NoAuthHost.Ct));

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(response.Str("access_token"),
            new TokenValidationParameters
            {
                ValidIssuer = NoAuthHost.Issuer,
                ValidAudience = "my_client",
                ValidTypes = ["at+jwt"],
                IssuerSigningKeys = jwks.GetSigningKeys()
            });

        result.IsValid.ShouldBeTrue(result.Exception?.Message);
    }

    [Fact]
    public async Task Accepts_client_credentials_in_a_basic_auth_header()
    {
        await using var host = await NoAuthHost.Start();

        var response = await host.PostWithBasicAuth("/connect/token", "basic_client", "secret",
            ("grant_type", "client_credentials"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        new JsonWebToken((await NoAuthHost.Json(response)).Str("access_token")).Subject.ShouldBe("basic_client");
    }

    [Fact]
    public async Task A_missing_client_is_an_invalid_client_error()
    {
        await using var host = await NoAuthHost.Start();

        var response = await host.Post("/connect/token", ("grant_type", "client_credentials"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await NoAuthHost.Json(response)).Str("error").ShouldBe("invalid_client");
    }

    [Fact]
    public async Task An_unsupported_grant_type_is_an_error()
    {
        await using var host = await NoAuthHost.Start();

        var response = await host.PostJson("/connect/token", ("grant_type", "password"));

        response.Str("error").ShouldBe("unsupported_grant_type");
    }

    [Fact]
    public async Task The_same_request_returns_the_existing_token()
    {
        await using var host = await NoAuthHost.Start();

        var first = await host.ClientCredentials(("client_id", "c"), ("scope", "a"));
        var second = await host.ClientCredentials(("client_id", "c"), ("scope", "a"));
        var otherScope = await host.ClientCredentials(("client_id", "c"), ("scope", "b"));

        second.Str("access_token").ShouldBe(first.Str("access_token"));
        otherScope.Str("access_token").ShouldNotBe(first.Str("access_token"));
    }

    [Fact]
    public async Task Predefined_client_claims_are_added()
    {
        using var profiles = new ProfilesFile("""
            { "logins": [], "clients": { "known_client": { "scope": [ "extra" ], "tenant": [ "acme" ] } } }
            """);
        await using var host = await NoAuthHost.Start(profiles.Path);

        var response = await host.ClientCredentials(("client_id", "known_client"), ("scope", "read"));
        var jwt = new JsonWebToken(response.Str("access_token"));

        response.Str("scope").ShouldBe("read extra");
        jwt.GetClaim("tenant").Value.ShouldBe("acme");
    }

    [Fact]
    public async Task Introspection_describes_active_tokens()
    {
        await using var host = await NoAuthHost.Start();
        var token = (await host.ClientCredentials(("client_id", "c"), ("scope", "a b"))).Str("access_token");

        var active = await host.PostJson("/connect/introspect", ("token", token));
        var unknown = await host.PostJson("/connect/introspect", ("token", "unknown"));

        active.GetProperty("active").GetBoolean().ShouldBeTrue();
        active.Str("client_id").ShouldBe("c");
        active.Str("scope").ShouldBe("a b");
        unknown.GetProperty("active").GetBoolean().ShouldBeFalse();
        unknown.EnumerateObject().Count().ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_tokens_are_rotated()
    {
        await using var host = await NoAuthHost.Start();
        var first = await host.ClientCredentials(("client_id", "c"));

        var refreshed = await host.PostJson("/connect/token",
            ("grant_type", "refresh_token"), ("refresh_token", first.Str("refresh_token")));
        var reused = await host.PostJson("/connect/token",
            ("grant_type", "refresh_token"), ("refresh_token", first.Str("refresh_token")));
        var oldToken = await host.PostJson("/connect/introspect", ("token", first.Str("access_token")));

        refreshed.Str("access_token").ShouldNotBe(first.Str("access_token"));
        refreshed.Str("refresh_token").ShouldNotBe(first.Str("refresh_token"));
        reused.Str("error").ShouldBe("invalid_grant");
        oldToken.GetProperty("active").GetBoolean().ShouldBeFalse();
    }

    [Theory]
    [InlineData("access_token")]
    [InlineData("refresh_token")]
    public async Task Revoked_tokens_are_inactive(string tokenToRevoke)
    {
        await using var host = await NoAuthHost.Start();
        var token = await host.ClientCredentials(("client_id", "c"));

        var revoke = await host.Post("/connect/revoke", ("token", token.Str(tokenToRevoke)));
        var introspection = await host.PostJson("/connect/introspect", ("token", token.Str("access_token")));

        revoke.StatusCode.ShouldBe(HttpStatusCode.OK);
        introspection.GetProperty("active").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task A_ttl_always_issues_a_new_token()
    {
        await using var host = await NoAuthHost.Start();

        var cached = await TokenFromApi(host, "/api/token?client_id=c");
        var withTtl = await TokenFromApi(host, "/api/token?client_id=c&ttl_minutes=1");

        withTtl.EncodedToken.ShouldNotBe(cached.EncodedToken);
        withTtl.ValidTo.ShouldBe(DateTime.UtcNow.AddMinutes(1), TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_ttl_of_zero_or_less_issues_an_expired_token(int minutes)
    {
        await using var host = await NoAuthHost.Start();

        var token = await TokenFromApi(host, $"/api/token?client_id=c&ttl_minutes={minutes}");
        var introspection = await host.PostJson("/connect/introspect", ("token", token.EncodedToken));

        token.ValidTo.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
        introspection.GetProperty("active").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Expired_tokens_are_removed_after_the_retention_period()
    {
        await using var host = await NoAuthHost.Start();
        var minutes = -(int)Services.TokenRepository.Retention.TotalMinutes - 60;
        await TokenFromApi(host, $"/api/token?client_id=old&ttl_minutes={minutes}");
        await TokenFromApi(host, "/api/token?client_id=recent&ttl_minutes=-5");

        await TokenFromApi(host, "/api/token?client_id=new");
        var clients = (await host.GetJson("/api/tokens")).GetProperty("accessTokens").EnumerateArray()
            .Select(t => t.Str("clientId"));

        clients.ShouldBe(["recent", "new"], ignoreOrder: true);
    }

    [Fact]
    public async Task Reserved_claims_in_client_profiles_are_ignored()
    {
        using var profiles = new ProfilesFile("""
            { "logins": [], "clients": { "c": { "sub": [ "x" ], "aud": [ "y" ], "tenant": [ "acme" ] } } }
            """);
        await using var host = await NoAuthHost.Start(profiles.Path);

        var jwt = new JsonWebToken((await host.ClientCredentials(("client_id", "c"))).Str("access_token"));

        jwt.Subject.ShouldBe("c");
        jwt.Audiences.ShouldBe(["c"]);
        jwt.GetClaim("tenant").Value.ShouldBe("acme");
    }

    [Fact]
    public async Task The_token_api_returns_a_token_as_text()
    {
        await using var host = await NoAuthHost.Start();

        var response = await host.Get("/api/token?client_id=api_client&scope=a&resource=https://api.local");
        var jwt = new JsonWebToken(await response.Content.ReadAsStringAsync(NoAuthHost.Ct));

        jwt.Subject.ShouldBe("api_client");
        jwt.Audiences.ShouldBe(["https://api.local"]);
    }

    private static async Task<JsonWebToken> TokenFromApi(NoAuthHost host, string uri)
    {
        var response = await host.Get(uri);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return new JsonWebToken(await response.Content.ReadAsStringAsync(NoAuthHost.Ct));
    }
}

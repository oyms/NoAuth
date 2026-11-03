using System.Net;
using Microsoft.IdentityModel.JsonWebTokens;
using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Tests;

/// <summary>Rules that depend on time, tested by advancing the clock of the server.</summary>
public class ExpiryTests
{
    private const int TtlInSeconds = 600;
    private static readonly string TtlArg = $"--NoAuth:TokenTtlInSeconds={TtlInSeconds}";

    [Fact]
    public async Task Tokens_are_issued_by_the_clock_of_the_server()
    {
        await using var host = await NoAuthHost.Start(TtlArg);
        host.Time.Advance(TimeSpan.FromDays(3));
        var now = host.Time.GetUtcNow();

        var response = await host.ClientCredentials(("client_id", "c"));

        var token = new JsonWebToken(response.Str("access_token"));
        token.IssuedAt.ShouldBe(now.UtcDateTime, TimeSpan.FromSeconds(1));
        token.ValidTo.ShouldBe(now.UtcDateTime.AddSeconds(TtlInSeconds), TimeSpan.FromSeconds(1));
        response.GetProperty("expires_in").GetInt64().ShouldBe(TtlInSeconds);
    }

    [Fact]
    public async Task Access_tokens_expire_after_the_ttl()
    {
        await using var host = await NoAuthHost.Start(TtlArg);
        var accessToken = (await host.ClientCredentials(("client_id", "c"))).Str("access_token");

        host.Time.Advance(TimeSpan.FromSeconds(TtlInSeconds - 1));
        var beforeExpiry = await host.PostJson("/connect/introspect", ("token", accessToken));
        host.Time.Advance(TimeSpan.FromSeconds(1));
        var afterExpiry = await host.PostJson("/connect/introspect", ("token", accessToken));

        beforeExpiry.GetProperty("active").GetBoolean().ShouldBeTrue();
        afterExpiry.GetProperty("active").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task A_new_client_token_is_issued_when_the_previous_has_expired()
    {
        await using var host = await NoAuthHost.Start(TtlArg);
        var first = (await host.ClientCredentials(("client_id", "c"))).Str("access_token");

        var cached = (await host.ClientCredentials(("client_id", "c"))).Str("access_token");
        host.Time.Advance(TimeSpan.FromSeconds(TtlInSeconds));
        var renewed = (await host.ClientCredentials(("client_id", "c"))).Str("access_token");

        cached.ShouldBe(first);
        renewed.ShouldNotBe(first);
    }

    [Fact]
    public async Task User_info_is_refused_for_expired_tokens()
    {
        await using var host = await NoAuthHost.Start(TtlArg);
        var redirect = await host.LogIn();
        var tokens = await NoAuthHost.Json(await host.ExchangeCode(redirect["code"]));

        host.Time.Advance(TimeSpan.FromSeconds(TtlInSeconds));
        var response = await host.Get("/connect/userinfo", tokens.Str("access_token"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_tokens_can_be_used_after_the_access_token_has_expired()
    {
        await using var host = await NoAuthHost.Start(TtlArg);
        var redirect = await host.LogIn();
        var tokens = await NoAuthHost.Json(await host.ExchangeCode(redirect["code"]));

        host.Time.Advance(TokenRepository.Retention);
        var refreshed = await host.Post("/connect/token",
            ("grant_type", "refresh_token"), ("refresh_token", tokens.Str("refresh_token")));

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_tokens_are_removed_after_the_retention_period()
    {
        await using var host = await NoAuthHost.Start(TtlArg);
        var redirect = await host.LogIn();
        var tokens = await NoAuthHost.Json(await host.ExchangeCode(redirect["code"]));

        host.Time.Advance(TokenRepository.Retention + TimeSpan.FromSeconds(TtlInSeconds + 1));
        await host.ClientCredentials(("client_id", "c")); // Old tokens are removed when new ones are issued
        var refreshed = await host.Post("/connect/token",
            ("grant_type", "refresh_token"), ("refresh_token", tokens.Str("refresh_token")));

        refreshed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await NoAuthHost.Json(refreshed)).Str("error").ShouldBe("invalid_grant");
    }

    [Theory]
    [InlineData(false, 11, HttpStatusCode.OK)]
    [InlineData(true, 9, HttpStatusCode.OK)]
    [InlineData(true, 11, HttpStatusCode.BadRequest)]
    public async Task In_strict_mode_codes_expire_after_ten_minutes(bool strict, int minutes,
        HttpStatusCode expected)
    {
        await using var host = await NoAuthHost.Start($"--NoAuth:Strict={strict}");
        var pkce = PkcePair.New();
        var redirect = await host.LogIn(pkce);

        host.Time.Advance(TimeSpan.FromMinutes(minutes));
        var response = await host.ExchangeCode(redirect["code"], pkce);

        response.StatusCode.ShouldBe(expected);
    }
}

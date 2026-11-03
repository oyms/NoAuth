using System.Net;

namespace Skaar.NoAuth.Tests;

public class DiscoveryTests
{
    [Theory]
    [InlineData("/.well-known/openid-configuration")]
    [InlineData("/.well-known/oauth-authorization-server")]
    public async Task Metadata_is_served_at_both_well_known_paths(string path)
    {
        await using var host = await NoAuthHost.Start();

        var metadata = await host.GetJson(path);

        metadata.Str("issuer").ShouldBe(NoAuthHost.Issuer);
        metadata.Str("token_endpoint").ShouldBe("https://localhost:44367/connect/token");
        metadata.Str("registration_endpoint").ShouldBe("https://localhost:44367/connect/register");
        metadata.Strings("code_challenge_methods_supported").ShouldBe(["S256", "plain"]);
        metadata.GetProperty("authorization_response_iss_parameter_supported").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Strict_mode_only_supports_S256()
    {
        await using var host = await NoAuthHost.Start("--NoAuth:Strict=true");

        var metadata = await host.GetJson("/.well-known/openid-configuration");

        metadata.Strings("code_challenge_methods_supported").ShouldBe(["S256"]);
    }

    [Fact]
    public async Task Urls_follow_the_request_host_when_configured()
    {
        await using var host = await NoAuthHost.Start("--NoAuth:DeriveUrlsFromRequest=true");
        var request = new HttpRequestMessage(HttpMethod.Get, "/.well-known/openid-configuration");
        request.Headers.Host = "noauth:8443";

        var metadata = await NoAuthHost.Json(await host.Client.SendAsync(request, NoAuthHost.Ct));

        metadata.Str("token_endpoint").ShouldBe("http://noauth:8443/connect/token");
        metadata.Str("issuer").ShouldBe(NoAuthHost.Issuer);
    }

    [Fact]
    public async Task Paths_are_configurable()
    {
        await using var host = await NoAuthHost.Start("--NoAuth:Paths:Token:0=/oauth2/token");

        var metadata = await host.GetJson("/.well-known/openid-configuration");
        var token = await host.Post("/oauth2/token", ("client_id", "c"));

        metadata.Str("token_endpoint").ShouldEndWith("/oauth2/token");
        token.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_empty_path_disables_the_endpoint()
    {
        await using var host = await NoAuthHost.Start("--NoAuth:Paths:Revocation=");

        var metadata = await host.GetJson("/.well-known/openid-configuration");
        var revoke = await host.Post("/connect/revoke", ("token", "t"));

        metadata.TryGetProperty("revocation_endpoint", out _).ShouldBeFalse();
        revoke.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Configuration_after_an_absolute_profiles_path_is_used()
    {
        using var profiles = new ProfilesFile("""{ "logins": [] }""");
        Path.IsPathRooted(profiles.Path).ShouldBeTrue();
        await using var host = await NoAuthHost.Start(profiles.Path, "--NoAuth:Issuer=https://other");

        var metadata = await host.GetJson("/.well-known/openid-configuration");

        metadata.Str("issuer").ShouldBe("https://other");
    }

    [Fact]
    public async Task Invalid_paths_are_rejected_at_startup()
    {
        var e = await Should.ThrowAsync<InvalidOperationException>(() =>
            NoAuthHost.Start("--NoAuth:Paths:Token:0=no-slash"));

        e.Message.ShouldContain("Token=no-slash");
    }

    [Fact]
    public async Task Html_pages_link_to_the_favicon()
    {
        await using var host = await NoAuthHost.Start();

        var page = await (await host.Get("/")).Content.ReadAsStringAsync(NoAuthHost.Ct);
        var icon = await host.Get("/favicon.png");

        page.ShouldContain("<link rel=\"icon\" type=\"image/png\" href=\"/favicon.png\"/>");
        icon.StatusCode.ShouldBe(HttpStatusCode.OK);
        icon.Content.Headers.ContentType?.MediaType.ShouldBe("image/png");
        (await icon.Content.ReadAsByteArrayAsync(NoAuthHost.Ct)).Length.ShouldBeGreaterThan(0);
    }
}

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Skaar.NoAuth.Tests;

public class EntraTests
{
    private const string TenantId = "1d7a9b2e-6c35-4f0a-9e48-3b5c2a7d8f61";
    private const string EntraIssuer = $"https://localhost:44367/{TenantId}/v2.0";
    private const string ApiId = "6a3f0c1e-2b4d-4e5f-8a9b-0c1d2e3f4a5b";
    private const string Enabled = "--NoAuth:Entra:Enabled=true";

    [Theory]
    [InlineData(TenantId)]
    [InlineData("contoso.onmicrosoft.com")]
    public async Task Discovery_is_served_for_a_tenant_with_entra_urls(string tenant)
    {
        await using var host = await NoAuthHost.Start(Enabled);

        var metadata = await host.GetJson($"/{tenant}/v2.0/.well-known/openid-configuration");

        metadata.Str("issuer").ShouldBe(EntraIssuer);
        metadata.Str("authorization_endpoint").ShouldBe($"https://localhost:44367/{tenant}/oauth2/v2.0/authorize");
        metadata.Str("token_endpoint").ShouldBe($"https://localhost:44367/{tenant}/oauth2/v2.0/token");
        metadata.Str("end_session_endpoint").ShouldBe($"https://localhost:44367/{tenant}/oauth2/v2.0/logout");
        metadata.Str("jwks_uri").ShouldBe($"https://localhost:44367/{tenant}/discovery/v2.0/keys");
    }

    [Fact]
    public async Task Multi_tenant_discovery_has_a_tenant_template_in_the_issuer()
    {
        await using var host = await NoAuthHost.Start(Enabled);

        var metadata = await host.GetJson("/common/v2.0/.well-known/openid-configuration");

        metadata.Str("issuer").ShouldBe("https://localhost:44367/{tenantid}/v2.0");
    }

    [Fact]
    public async Task Entra_paths_are_not_served_unless_enabled()
    {
        await using var host = await NoAuthHost.Start();

        var response = await host.Get($"/{TenantId}/v2.0/.well-known/openid-configuration");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Entra_paths_are_case_insensitive()
    {
        await using var host = await NoAuthHost.Start(Enabled);

        var metadata = await host.GetJson($"/{TenantId}/V2.0/.well-known/OpenID-Configuration");

        metadata.Str("issuer").ShouldBe(EntraIssuer);
    }

    [Fact]
    public async Task A_tenant_id_is_required_when_enabled()
    {
        await Should.ThrowAsync<InvalidOperationException>(() =>
            NoAuthHost.Start(Enabled, "--NoAuth:Entra:TenantId= "));
    }

    [Fact]
    public async Task A_refresh_with_scopes_of_another_api_gives_a_token_for_that_api()
    {
        await using var host = await NoAuthHost.Start(Enabled);
        const string otherApi = "0b9e8d7c-6f5a-4b3c-9d2e-1f0a9b8c7d6e";
        var tokens = await LogIn(host, $"openid offline_access api://{ApiId}/access_as_user");

        var refreshed = await host.PostJson($"/{TenantId}/oauth2/v2.0/token",
            ("grant_type", "refresh_token"), ("refresh_token", tokens.Str("refresh_token")), ("client_id", "web"),
            ("scope", $"openid offline_access api://{otherApi}/Data.Read"));
        var accessToken = new JsonWebToken(refreshed.Str("access_token"));

        accessToken.Audiences.ShouldBe([otherApi]);
        accessToken.GetClaim("scp").Value.ShouldBe("Data.Read");
    }

    [Fact]
    public async Task A_refresh_without_scopes_keeps_the_audience()
    {
        await using var host = await NoAuthHost.Start(Enabled);
        var tokens = await LogIn(host, $"openid api://{ApiId}/access_as_user");

        var refreshed = await host.PostJson($"/{TenantId}/oauth2/v2.0/token",
            ("grant_type", "refresh_token"), ("refresh_token", tokens.Str("refresh_token")));

        new JsonWebToken(refreshed.Str("access_token")).Audiences.ShouldBe([ApiId]);
    }

    [Fact]
    public async Task User_tokens_have_entra_claims_and_the_audience_of_the_scope()
    {
        await using var host = await NoAuthHost.Start(Enabled);

        var tokens = await LogIn(host, $"openid profile api://{ApiId}/access_as_user api://{ApiId}/Data.Read");
        var accessToken = new JsonWebToken(tokens.Str("access_token"));
        var idToken = new JsonWebToken(tokens.Str("id_token"));

        accessToken.Typ.ShouldBe("JWT");
        accessToken.Issuer.ShouldBe(EntraIssuer);
        accessToken.Audiences.ShouldBe([ApiId]);
        accessToken.GetClaim("scp").Value.ShouldBe("access_as_user Data.Read");
        accessToken.GetClaim("tid").Value.ShouldBe(TenantId);
        accessToken.GetClaim("ver").Value.ShouldBe("2.0");
        accessToken.GetClaim("azp").Value.ShouldBe("web");
        accessToken.GetClaim("preferred_username").Value.ShouldBe("ola.nordmann@noauth.onmicrosoft.com");
        Guid.TryParse(accessToken.GetClaim("oid").Value, out _).ShouldBeTrue();
        accessToken.TryGetClaim("scope", out _).ShouldBeFalse();
        accessToken.TryGetClaim("client_id", out _).ShouldBeFalse();
        idToken.Issuer.ShouldBe(EntraIssuer);
        idToken.Audiences.ShouldBe(["web"]);
        idToken.GetClaim("oid").Value.ShouldBe(accessToken.GetClaim("oid").Value);
        idToken.GetClaim("tid").Value.ShouldBe(TenantId);
    }

    [Fact]
    public async Task Claims_of_the_user_override_the_generated_entra_claims()
    {
        await using var host = await NoAuthHost.Start(Enabled);
        var oid = Guid.NewGuid().ToString();

        var tokens = await LogIn(host, "openid", ("oid", oid), ("email", "ola@example.com"));
        var accessToken = new JsonWebToken(tokens.Str("access_token"));

        accessToken.GetPayloadValue<string>("oid").ShouldBe(oid);
        accessToken.GetPayloadValue<string>("preferred_username").ShouldBe("ola@example.com");
    }

    [Fact]
    public async Task Scopes_without_a_resource_are_graph_scopes()
    {
        await using var host = await NoAuthHost.Start(Enabled);

        var tokens = await LogIn(host, "openid User.Read");
        var accessToken = new JsonWebToken(tokens.Str("access_token"));

        accessToken.Audiences.ShouldBe(["https://graph.microsoft.com"]);
        accessToken.GetClaim("scp").Value.ShouldBe("User.Read");
    }

    [Fact]
    public async Task Client_info_is_returned_when_requested()
    {
        await using var host = await NoAuthHost.Start(Enabled);

        var tokens = await LogIn(host, "openid", tokenParameters: [("client_info", "1")]);
        var clientInfo = JsonDocument.Parse(Base64UrlEncoder.Decode(tokens.Str("client_info"))).RootElement;

        clientInfo.Str("uid").ShouldBe(new JsonWebToken(tokens.Str("id_token")).GetClaim("oid").Value);
        clientInfo.Str("utid").ShouldBe(TenantId);
    }

    [Fact]
    public async Task App_tokens_get_the_audience_of_the_default_scope_and_roles_of_the_client()
    {
        using var profiles = new JsonFile("""
            { "logins": [], "clients": { "daemon": { "roles": [ "Data.Read" ] } } }
            """);
        await using var host = await NoAuthHost.Start(profiles.Path, Enabled);

        var response = await host.PostJson($"/{TenantId}/oauth2/v2.0/token", ("grant_type", "client_credentials"),
            ("client_id", "daemon"), ("client_secret", "secret"), ("scope", $"api://{ApiId}/.default"));
        var accessToken = new JsonWebToken(response.Str("access_token"));

        accessToken.Audiences.ShouldBe([ApiId]);
        accessToken.GetClaim("roles").Value.ShouldBe("Data.Read");
        accessToken.GetClaim("idtyp").Value.ShouldBe("app");
        accessToken.TryGetClaim("scp", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task User_tokens_are_accepted_by_microsoft_identity_web()
    {
        await using var host = await NoAuthHost.Start(Enabled);
        await using var api = await StartApi(host);
        var tokens = await LogIn(host, $"openid api://{ApiId}/access_as_user");

        var response = await Get(api, "/user", tokens.Str("access_token"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(NoAuthHost.Ct));
        (await response.Content.ReadAsStringAsync(NoAuthHost.Ct))
            .ShouldBe(new JsonWebToken(tokens.Str("access_token")).GetClaim("oid").Value);
    }

    [Fact]
    public async Task Required_scopes_are_checked_by_microsoft_identity_web()
    {
        await using var host = await NoAuthHost.Start(Enabled);
        await using var api = await StartApi(host);
        var tokens = await LogIn(host, $"openid api://{ApiId}/other_scope");

        var response = await Get(api, "/user", tokens.Str("access_token"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task App_tokens_are_accepted_by_microsoft_identity_web()
    {
        using var profiles = new JsonFile("""
            { "logins": [], "clients": { "daemon": { "roles": [ "Data.Read" ] } } }
            """);
        await using var host = await NoAuthHost.Start(profiles.Path, Enabled);
        await using var api = await StartApi(host);
        var token = await host.PostJson($"/{TenantId}/oauth2/v2.0/token", ("grant_type", "client_credentials"),
            ("client_id", "daemon"), ("scope", $"api://{ApiId}/.default"));

        var response = await Get(api, "/app", token.Str("access_token"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(NoAuthHost.Ct));
    }

    /// <summary>Logs in user Ola Nordmann through the Entra paths, and exchanges the code for tokens.</summary>
    private static async Task<JsonElement> LogIn(NoAuthHost host, string scope,
        params (string Type, string Value)[] claims) => await LogIn(host, scope, [], claims);

    private static async Task<JsonElement> LogIn(NoAuthHost host, string scope,
        (string Key, string Value)[] tokenParameters, params (string Type, string Value)[] claims)
    {
        var authorize = await host.Get(NoAuthHost.AuthorizeUri($"/{TenantId}/oauth2/v2.0/authorize", null,
            ("scope", scope)));
        authorize.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var login = await host.Post(authorize.Headers.Location!.ToString(),
        [
            ("uid", "12106346508"), ("given_name", "Ola"), ("family_name", "Nordmann"),
            .. claims.SelectMany(c => new[] { ("claim_type", c.Type), ("claim_value", c.Value) })
        ]);
        var code = NoAuthHost.Query(login.Headers.Location!)["code"];
        var response = await host.Post($"/{TenantId}/oauth2/v2.0/token",
        [
            ("grant_type", "authorization_code"), ("code", code), ("client_id", "web"),
            ("redirect_uri", NoAuthHost.RedirectUri), .. tokenParameters
        ]);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await NoAuthHost.Json(response);
    }

    /// <summary>
    /// A web api protected by Microsoft.Identity.Web, with NoAuth as the instance (as in <c>AzureAd:Instance</c>).
    /// </summary>
    private static async Task<WebApplication> StartApi(NoAuthHost host)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureAd:Instance"] = "https://localhost:44367/",
            ["AzureAd:TenantId"] = TenantId,
            ["AzureAd:ClientId"] = ApiId
        });
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(builder.Configuration);
        // Metadata is read with the backchannel, and by the issuer validator with a named http client.
        builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
            options => options.BackchannelHttpHandler = host.CreateHandler());
        builder.Services.Configure<AadIssuerValidatorOptions>(options => options.HttpClientName = "noauth");
        builder.Services.AddHttpClient("noauth").ConfigurePrimaryHttpMessageHandler(host.CreateHandler);
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/user", (ClaimsPrincipal user) => user.GetObjectId())
            .RequireAuthorization()
            .RequireScope("access_as_user");
        app.MapGet("/app", () => "ok").RequireAuthorization(policy => policy.RequireRole("Data.Read"));
        await app.StartAsync(NoAuthHost.Ct);
        return app;
    }

    private static Task<HttpResponseMessage> Get(WebApplication api, string path, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new("Bearer", accessToken);
        return api.GetTestClient().SendAsync(request, NoAuthHost.Ct);
    }
}

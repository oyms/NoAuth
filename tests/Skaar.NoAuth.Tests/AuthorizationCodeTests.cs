using System.Net;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Skaar.NoAuth.Tests;

public class AuthorizationCodeTests
{
    [Fact]
    public async Task The_authorization_request_is_passed_on_to_the_login_form()
    {
        await using var host = await NoAuthHost.Start();
        var pkce = PkcePair.New();

        var response = await host.Get(NoAuthHost.AuthorizeUri(pkce));
        var login = NoAuthHost.Query(response.Headers.Location!);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith("/connect/login?");
        login["code_challenge"].ShouldBe(pkce.Challenge);
        login["state"].ShouldBe("the-state");
        login.ShouldContainKey("uid");
    }

    [Fact]
    public async Task The_login_page_uses_the_configured_profiles_path()
    {
        await using var host = await NoAuthHost.Start("--NoAuth:Paths:LoginProfile=/profiles");

        var html = await (await host.Get("/connect/login")).Content.ReadAsStringAsync(NoAuthHost.Ct);

        html.ShouldContain("const apiUrl = '/profiles';");
    }

    [Fact]
    public async Task Logging_in_redirects_to_the_client_with_code_state_and_issuer()
    {
        await using var host = await NoAuthHost.Start();

        var redirect = await host.LogIn(PkcePair.New());

        redirect.Keys.ShouldBe(["state", "code", "iss"], ignoreOrder: true);
        redirect["state"].ShouldBe("the-state");
        redirect["iss"].ShouldBe(NoAuthHost.Issuer);
    }

    [Fact]
    public async Task The_code_is_exchanged_for_tokens_for_the_user()
    {
        await using var host = await NoAuthHost.Start();
        var pkce = PkcePair.New();
        var redirect = await host.LogIn(pkce, parameters: ("resource", "https://api.local"));

        var tokens = await NoAuthHost.Json(await host.ExchangeCode(redirect["code"], pkce));
        var idToken = new JsonWebToken(tokens.Str("id_token"));
        var accessToken = new JsonWebToken(tokens.Str("access_token"));

        idToken.Subject.ShouldBe("12106346508");
        idToken.Audiences.ShouldBe(["web"]);
        idToken.GetClaim("nonce").Value.ShouldBe("the-nonce");
        idToken.GetClaim("name").Value.ShouldBe("Ola Nordmann");
        accessToken.Typ.ShouldBe("at+jwt");
        accessToken.Subject.ShouldBe("12106346508");
        accessToken.Audiences.ShouldBe(["https://api.local"]);
        accessToken.GetClaim("role").Value.ShouldBe("user");
        accessToken.GetClaim("scope").Value.ShouldBe("openid profile");
    }

    [Fact]
    public async Task User_info_is_available_with_the_access_token()
    {
        await using var host = await NoAuthHost.Start();
        var redirect = await host.LogIn();
        var tokens = await NoAuthHost.Json(await host.ExchangeCode(redirect["code"]));

        var userInfo = await NoAuthHost.Json(await host.Get("/connect/userinfo", tokens.Str("access_token")));
        var unauthorized = await host.Get("/connect/userinfo", "unknown");

        userInfo.Str("sub").ShouldBe("12106346508");
        userInfo.Strings("role").ShouldBe(["user"]);
        unauthorized.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_wrong_code_verifier_is_rejected()
    {
        await using var host = await NoAuthHost.Start();
        var redirect = await host.LogIn(PkcePair.New());

        var response = await host.ExchangeCode(redirect["code"], PkcePair.New());

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await NoAuthHost.Json(response)).Str("error").ShouldBe("invalid_grant");
    }

    [Fact]
    public async Task A_missing_code_verifier_is_rejected_when_a_challenge_was_sent()
    {
        await using var host = await NoAuthHost.Start();
        var redirect = await host.LogIn(PkcePair.New());

        var response = await host.ExchangeCode(redirect["code"]);

        (await NoAuthHost.Json(response)).Str("error_description").ShouldBe("Missing code_verifier");
    }

    [Fact]
    public async Task By_default_codes_can_be_reused()
    {
        await using var host = await NoAuthHost.Start();
        var redirect = await host.LogIn();

        (await host.ExchangeCode(redirect["code"])).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.ExchangeCode(redirect["code"])).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refreshing_returns_new_tokens_for_the_user()
    {
        await using var host = await NoAuthHost.Start();
        var redirect = await host.LogIn();
        var tokens = await NoAuthHost.Json(await host.ExchangeCode(redirect["code"]));

        var refreshed = await host.PostJson("/connect/token",
            ("grant_type", "refresh_token"), ("refresh_token", tokens.Str("refresh_token")));

        new JsonWebToken(refreshed.Str("access_token")).Subject.ShouldBe("12106346508");
        new JsonWebToken(refreshed.Str("id_token")).Subject.ShouldBe("12106346508");
    }

    [Fact]
    public async Task An_unsupported_response_type_is_redirected_as_an_error()
    {
        await using var host = await NoAuthHost.Start();

        var response = await host.Get(NoAuthHost.AuthorizeUri(parameters: ("response_type", "token")));
        var redirect = NoAuthHost.Query(response.Headers.Location!);

        response.Headers.Location!.ToString().ShouldStartWith(NoAuthHost.RedirectUri);
        redirect["error"].ShouldBe("unsupported_response_type");
        redirect["state"].ShouldBe("the-state");
        redirect["iss"].ShouldBe(NoAuthHost.Issuer);
    }

    [Theory]
    [InlineData("/relative")]
    [InlineData("relative")]
    [InlineData("file:///etc/passwd")]
    public async Task An_invalid_redirect_uri_is_not_redirected_to(string redirectUri)
    {
        await using var host = await NoAuthHost.Start();

        var response = await host.Get(NoAuthHost.AuthorizeUri(parameters: ("redirect_uri", redirectUri)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task By_default_random_users_are_not_remembered()
    {
        await using var host = await NoAuthHost.Start("--NoAuth:Oidc:Strategy=GenerateRandom");

        var response = await host.Get(NoAuthHost.AuthorizeUri());

        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task Reserved_claims_from_the_login_form_are_ignored()
    {
        await using var host = await NoAuthHost.Start();
        var authorize = await host.Get(NoAuthHost.AuthorizeUri());
        var login = await host.Post(authorize.Headers.Location!.ToString(),
            ("uid", "12106346508"), ("claim_type", "sub"), ("claim_value", "someone-else"),
            ("claim_type", "department"), ("claim_value", "sales"));
        var code = NoAuthHost.Query(login.Headers.Location!)["code"];
        var tokens = await NoAuthHost.Json(await host.ExchangeCode(code));

        var userInfo = await NoAuthHost.Json(await host.Get("/connect/userinfo", tokens.Str("access_token")));

        new JsonWebToken(tokens.Str("id_token")).Subject.ShouldBe("12106346508");
        userInfo.Str("sub").ShouldBe("12106346508");
        userInfo.Strings("department").ShouldBe(["sales"]);
    }

    [Theory]
    [InlineData("alice", "alice")]
    [InlineData(" 1234567 ", "1234567")]
    [InlineData("", null)]
    public async Task Any_user_id_is_accepted_and_an_empty_one_is_generated(string uid, string? expected)
    {
        await using var host = await NoAuthHost.Start();
        var redirect = await host.LogIn(uid: uid);

        var tokens = await NoAuthHost.Json(await host.ExchangeCode(redirect["code"]));

        var subject = new JsonWebToken(tokens.Str("id_token")).Subject;
        if (expected is null) Fodselsnummer.Parse(subject, null).IsValid.ShouldBeTrue();
        else subject.ShouldBe(expected);
    }

    [Fact]
    public async Task Without_a_logout_page_the_session_ends_without_a_redirect()
    {
        await using var host = await NoAuthHost.Start("--NoAuth:Paths:LogOutRedirect=");

        var response = await host.Get("/connect/endsession");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(NoAuthHost.Ct)).ShouldBe("Logged out.");
    }

    [Fact]
    public async Task Native_app_redirect_uris_are_accepted()
    {
        await using var host = await NoAuthHost.Start("--NoAuth:Oidc:Strategy=GenerateRandom");

        var response = await host.Get(NoAuthHost.AuthorizeUri(parameters: ("redirect_uri", "com.example.app:/callback")));

        response.Headers.Location!.ToString().ShouldStartWith("com.example.app:/callback?");
    }

    [Fact]
    public async Task Ending_the_session_removes_id_tokens_and_redirects()
    {
        await using var host = await NoAuthHost.Start();
        var redirect = await host.LogIn();
        var tokens = await NoAuthHost.Json(await host.ExchangeCode(redirect["code"]));

        var endSession = await host.Get(
            $"/connect/endsession?id_token_hint={tokens.Str("id_token")}&post_logout_redirect_uri=https://app.local/bye&state=s");
        var fallback = await host.Get("/connect/endsession");
        var dump = await host.GetJson("/api/tokens");

        endSession.Headers.Location!.ToString().ShouldBe("https://app.local/bye?state=s");
        fallback.Headers.Location!.AbsolutePath.ShouldBe("/fallback/session_ended");
        dump.GetProperty("idTokens").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task A_random_user_is_remembered_in_a_session_cookie()
    {
        await using var host = await NoAuthHost.Start(
            "--NoAuth:Oidc:Strategy=GenerateRandom", "--NoAuth:Oidc:RememberUser=true");

        var first = await host.Get(NoAuthHost.AuthorizeUri());
        var cookie = first.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        var second = await host.Get(NoAuthHost.AuthorizeUri(), cookie: cookie);
        await host.Get("/connect/endsession", cookie: cookie);
        var afterLogout = await host.Get(NoAuthHost.AuthorizeUri(), cookie: cookie);

        var users = await Task.WhenAll(new[] { first, second, afterLogout }.Select(async r =>
        {
            var code = NoAuthHost.Query(r.Headers.Location!)["code"];
            var tokens = await NoAuthHost.Json(await host.ExchangeCode(code));
            return new JsonWebToken(tokens.Str("id_token")).Subject;
        }));
        users[1].ShouldBe(users[0]);
        users[2].ShouldNotBe(users[0]);
    }
}

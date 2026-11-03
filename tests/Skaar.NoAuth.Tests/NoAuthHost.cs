using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Tests;

/// <summary>
/// Runs NoAuth in memory. Configuration is given as command line arguments, e.g. <c>--NoAuth:Strict=true</c>.
/// The client does not follow redirects or keep cookies, so each step of a flow can be inspected.
/// </summary>
internal sealed class NoAuthHost : IAsyncDisposable
{
    public const string Issuer = "https://noauth";
    public const string RedirectUri = "https://app.local/callback";

    private readonly Server server;

    private NoAuthHost(Server server)
    {
        this.server = server;
        Client = server.App.GetTestClient();
    }

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public HttpClient Client { get; }

    public static async Task<NoAuthHost> Start(params string[] args)
    {
        var server = Server.Create(args, builder => builder.WebHost.UseTestServer());
        await server.App.StartAsync(Ct);
        return new NoAuthHost(server);
    }

    public Task<HttpResponseMessage> Get(string uri, string? bearerToken = null, string? cookie = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (bearerToken is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        return Client.SendAsync(request, Ct);
    }

    public async Task<JsonElement> GetJson(string uri) => await Json(await Get(uri));

    public Task<HttpResponseMessage> Post(string uri, params (string Key, string Value)[] form) =>
        Client.PostAsync(uri, new FormUrlEncodedContent(form.Select(f => KeyValuePair.Create(f.Key, f.Value))), Ct);

    public Task<HttpResponseMessage> PostWithBasicAuth(string uri, string clientId, string clientSecret,
        params (string Key, string Value)[] form)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new FormUrlEncodedContent(form.Select(f => KeyValuePair.Create(f.Key, f.Value)))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));
        return Client.SendAsync(request, Ct);
    }

    public async Task<JsonElement> PostJson(string uri, params (string Key, string Value)[] form) =>
        await Json(await Post(uri, form));

    /// <summary>A token request with the client credentials grant.</summary>
    public Task<JsonElement> ClientCredentials(params (string Key, string Value)[] form) =>
        PostJson("/connect/token", [("grant_type", "client_credentials"), .. form]);

    /// <summary>
    /// Starts an authorization request for client <c>web</c>, and posts the login form with the given user.
    /// Returns the parameters of the redirect back to the client.
    /// </summary>
    public async Task<IDictionary<string, string>> LogIn(PkcePair? pkce = null, string uid = "12106346508",
        params (string Key, string Value)[] parameters)
    {
        var authorize = await Get(AuthorizeUri(pkce, parameters));
        authorize.StatusCode.ShouldBe(System.Net.HttpStatusCode.Redirect);
        var login = await Post(authorize.Headers.Location!.ToString(),
            ("uid", uid), ("given_name", "Ola"), ("family_name", "Nordmann"),
            ("claim_type", "role"), ("claim_value", "user"));
        login.StatusCode.ShouldBe(System.Net.HttpStatusCode.Redirect);
        return Query(login.Headers.Location!);
    }

    public static string AuthorizeUri(PkcePair? pkce = null, params (string Key, string Value)[] parameters)
    {
        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = "web",
            ["redirect_uri"] = RedirectUri,
            ["state"] = "the-state",
            ["nonce"] = "the-nonce",
            ["scope"] = "openid profile"
        };
        if (pkce is not null)
        {
            query["code_challenge"] = pkce.Challenge;
            query["code_challenge_method"] = "S256";
        }

        foreach (var (key, value) in parameters) query[key] = value;
        return QueryHelpers.AddQueryString("/connect/authorize", query);
    }

    /// <summary>Exchanges a code with the authorization code grant.</summary>
    public Task<HttpResponseMessage> ExchangeCode(string code, PkcePair? pkce = null,
        params (string Key, string Value)[] parameters)
    {
        (string, string)[] form = [("grant_type", "authorization_code"), ("code", code), ("client_id", "web"),
            ("redirect_uri", RedirectUri)];
        if (pkce is not null) form = [.. form, ("code_verifier", pkce.Verifier)];
        return Post("/connect/token", [.. form, .. parameters]);
    }

    public static IDictionary<string, string> Query(Uri uri) =>
        QueryHelpers.ParseQuery(uri.IsAbsoluteUri ? uri.Query : new Uri(new Uri("http://localhost"), uri).Query)
            .ToDictionary(q => q.Key, q => q.Value.ToString());

    public static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await server.App.StopAsync(Ct);
        await server.DisposeAsync();
    }
}

/// <summary>A PKCE code verifier and its S256 challenge.</summary>
internal sealed record PkcePair(string Verifier, string Challenge)
{
    public static PkcePair New()
    {
        var verifier = Base64Url(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        return new PkcePair(verifier, Base64Url(System.Security.Cryptography.SHA256.HashData(Encoding.ASCII.GetBytes(verifier))));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

internal static class JsonElementExtensions
{
    public static string Str(this JsonElement json, string property) => json.GetProperty(property).GetString()!;

    public static string[] Strings(this JsonElement json, string property) =>
        json.GetProperty(property).EnumerateArray().Select(e => e.GetString()!).ToArray();
}

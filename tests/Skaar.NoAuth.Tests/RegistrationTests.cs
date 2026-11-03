using System.Net;
using System.Net.Http.Json;

namespace Skaar.NoAuth.Tests;

public class RegistrationTests
{
    [Fact]
    public async Task Any_client_is_registered()
    {
        await using var host = await NoAuthHost.Start();

        var response = await host.Client.PostAsJsonAsync("/connect/register",
            new { client_name = "mcp-client", redirect_uris = new[] { "http://localhost:3000/callback" } },
            NoAuthHost.Ct);
        var client = await NoAuthHost.Json(response);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        client.Str("client_id").ShouldNotBeNullOrEmpty();
        client.Str("client_secret").ShouldNotBeNullOrEmpty();
        client.Str("client_name").ShouldBe("mcp-client");
    }

    [Fact]
    public async Task Public_clients_get_no_secret()
    {
        await using var host = await NoAuthHost.Start();

        var response = await host.Client.PostAsJsonAsync("/connect/register",
            new { token_endpoint_auth_method = "none" }, NoAuthHost.Ct);

        (await NoAuthHost.Json(response)).TryGetProperty("client_secret", out _).ShouldBeFalse();
    }
}

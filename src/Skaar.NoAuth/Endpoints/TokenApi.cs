using System.Net;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Exceptions;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Endpoints;

/// <summary>
/// Convenience api to get a token (as plain text) with a simple GET, or delete tokens for a client.
/// </summary>
internal sealed class TokenApi(Configuration config, ITokenRepository tokens) : IEndpoint
{
    public bool IsMatch(HttpRequest request) => request.IsPath(config.Paths.TokenRequestApi);

    public async Task Process(HttpContext context)
    {
        var query = context.Request.Query;
        var clientId = query["client_id"];

        if (HttpMethods.IsDelete(context.Request.Method))
        {
            if (clientId.Count != 1 || string.IsNullOrWhiteSpace(clientId))
                throw new StatusCodeException(HttpStatusCode.BadRequest, "Please provide one client_id");
            tokens.ClearAccessTokens(clientId.ToString());
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            return;
        }

        TimeSpan? ttl = int.TryParse(query["ttl_minutes"], out var minutes) ? TimeSpan.FromMinutes(minutes) : null;
        var audiences = query["resource"].Concat(query["audience"])
            .OfType<string>()
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct()
            .ToList();
        var token = tokens.GetAccessToken(new TokenRequestParameters(
            query["scope"].ToString().ParseScopeString(),
            clientId.ToString().WithFallback($"generated_client_id_{Guid.NewGuid()}"),
            query["client_secret"].ToString(),
            audiences,
            ttl));
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync(token.Token);
    }
}

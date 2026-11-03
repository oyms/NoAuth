using System.Net;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Extensions;

namespace Skaar.NoAuth.Endpoints;

internal sealed class UserInfo(Configuration config, ITokenRepository tokens) : IEndpoint
{
    public bool IsMatch(HttpRequest request) => request.IsPath(config.Paths.UserInfo);

    public async Task Process(HttpContext context)
    {
        var user = context.Request.TryReadBearerToken(out var accessToken) &&
                   tokens.FindAccessToken(accessToken) is { IsActive: true } token
            ? token.User
            : null;

        if (user is null)
        {
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer error=\"invalid_token\"";
            return;
        }

        var body = new Dictionary<string, object>
        {
            ["sub"] = user.Uid.ToString(),
            ["name"] = user.Name,
            ["given_name"] = user.GivenName,
            ["family_name"] = user.FamilyName
        };
        foreach (var claim in user.ExtraClaims.GroupBy(c => c.Type, c => c.Value))
            body.TryAdd(claim.Key, claim.ToArray());

        await context.Response.WriteAsJsonAsync(body);
    }
}

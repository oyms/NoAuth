using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Endpoints;

/// <summary>
/// Token introspection (RFC 7662).
/// </summary>
internal sealed class Introspection(Configuration config, ITokenRepository tokens, IWriter writer) : IEndpoint
{
    public bool IsMatch(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) && request.IsPath(config.Paths.Introspection);

    public async Task Process(HttpContext context)
    {
        var parameters = await RequestParameters.Read(context);
        var token = parameters.GetRequired("token");

        if (tokens.FindActiveAccessToken(token) is { } validToken)
        {
            await context.Response.Serialize(new
            {
                Active = true,
                Scope = validToken.ScopeString,
                validToken.ClientId,
                Sub = validToken.Subject,
                Aud = validToken.Audiences.Count > 0 ? validToken.Audiences : null,
                Iss = config.Issuer,
                TokenType = "Bearer",
                Iat = validToken.IssuedAt.ToUnixTimeSeconds(),
                Exp = validToken.Expires.ToUnixTimeSeconds()
            });
        }
        else
        {
            writer.WriteWarning($"Token {token} not found or expired.");
            await context.Response.Serialize(new { Active = false });
        }
    }
}

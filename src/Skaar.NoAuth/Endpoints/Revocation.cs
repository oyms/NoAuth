using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Endpoints;

/// <summary>
/// Token revocation (RFC 7009). Revokes access tokens and refresh tokens.
/// Responds with 200 also for unknown tokens.
/// </summary>
internal sealed class Revocation(Configuration config, ITokenRepository tokens, IWriter writer) : IEndpoint
{
    public bool IsMatch(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) && request.IsPath(config.Paths.Revocation);

    public async Task Process(HttpContext context)
    {
        var parameters = await RequestParameters.Read(context);
        var token = parameters.GetRequired("token");
        if (!tokens.Revoke(token)) writer.WriteWarning($"Token to revoke not found: {token}");
    }
}

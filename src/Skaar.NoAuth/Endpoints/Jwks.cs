using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Extensions;

namespace Skaar.NoAuth.Endpoints;

internal sealed class Jwks(Configuration config, IKeyRepository keys) : IEndpoint
{
    private const string ContentType = "application/jwk-set+json; charset=utf-8";

    public bool IsMatch(HttpRequest request) =>
        HttpMethods.IsGet(request.Method) && request.IsPath(config.Paths.Jwks);

    public Task Process(HttpContext context) =>
        context.Response.Serialize(new { Keys = keys.PublicKeys }, contentType: ContentType);
}

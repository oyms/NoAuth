using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Endpoints;

internal sealed class Favicon(Configuration config) : IEndpoint
{
    private const string Resource = "Views.favicon.png";

    public bool IsMatch(HttpRequest request) =>
        HttpMethods.IsGet(request.Method) && request.IsPath(config.Paths.Favicon);

    public async Task Process(HttpContext context)
    {
        await using var icon = EmbeddedResources.Open(Resource);
        context.Response.ContentType = "image/png";
        context.Response.Headers.CacheControl = "public, max-age=86400";
        await icon.CopyToAsync(context.Response.Body);
    }
}

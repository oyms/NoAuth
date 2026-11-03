using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Endpoints;

internal sealed class FrontPage(Configuration config, ViewRenderer views) : IEndpoint
{
    public bool IsMatch(HttpRequest request) =>
        HttpMethods.IsGet(request.Method) && request.IsPath(config.Paths.FrontPage);

    public Task Process(HttpContext context) => views.Render(context, "FrontPage.html");
}

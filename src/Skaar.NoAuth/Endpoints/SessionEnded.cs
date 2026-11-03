using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Endpoints;

/// <summary>
/// Where to end up after logging out, when no post_logout_redirect_uri is provided.
/// </summary>
internal sealed class SessionEnded(Configuration config, ViewRenderer views) : IEndpoint
{
    public bool IsMatch(HttpRequest request) => request.IsPath(config.Paths.LogOutRedirect);

    public Task Process(HttpContext context) => views.Render(context, "SessionEnded.html");
}

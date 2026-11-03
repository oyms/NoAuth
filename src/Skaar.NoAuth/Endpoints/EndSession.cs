using Microsoft.AspNetCore.Http.Extensions;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Endpoints;

/// <summary>
/// https://openid.net/specs/openid-connect-rpinitiated-1_0.html
/// </summary>
internal sealed class EndSession(
    ITokenRepository tokens,
    ISessionStore sessions,
    Configuration config,
    IWriter @out) : IEndpoint
{
    public bool IsMatch(HttpRequest request) => request.IsPath(config.Paths.EndSession);

    public async Task Process(HttpContext context)
    {
        var parameters = await RequestParameters.Read(context);
        var redirectUri = parameters.GetOptional("post_logout_redirect_uri");
        var state = parameters.GetOptional("state");
        var idTokenHint = parameters.GetOptional("id_token_hint");

        sessions.End(context.Request.Cookies[Authorize.SessionCookieName]);
        context.Response.Cookies.Delete(Authorize.SessionCookieName);

        if (idTokenHint is null)
        {
            @out.WriteWarning("No id_token_hint provided. Cannot remove id tokens.");
        }
        else if (tokens.RemoveIdTokens(idTokenHint) is { } subject)
        {
            sessions.EndAllFor(subject);
            @out.WriteInfo($"Ending session for {subject}.");
        }

        var target = redirectUri is null
            ? config.Url(config.Paths.LogOutRedirect, context.Request)
            : HttpResponseExtensions.ParseAbsoluteUri(redirectUri, "post_logout_redirect_uri");
        if (target is null)
        {
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync("Logged out.");
            return;
        }

        var uri = new UriBuilder(target);
        var query = new QueryBuilder();
        query.AddFromQuery(uri.Query);
        query.AddIfNotNullOrWhiteSpace("state", state);
        uri.Query = query.ToString();

        context.Response.Redirect(uri.Uri.AbsoluteUri);
    }
}

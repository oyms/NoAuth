using Microsoft.AspNetCore.Http.Extensions;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Exceptions;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Models;
using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Endpoints;

internal sealed class Authorize(
    ITokenRepository tokens,
    ISessionStore sessions,
    IUserDataFactory userData,
    Configuration config,
    IWriter @out) : IEndpoint
{
    public const string SessionCookieName = "noauth-session";

    public bool IsMatch(HttpRequest request) => request.IsPath(config.Paths.Authorize);

    public async Task Process(HttpContext context)
    {
        var parameters = await RequestParameters.Read(context);
        var (clientId, redirectUri) = AuthorizationRequestReader.ReadClient(parameters);
        AuthorizationRequest request;
        try
        {
            request = AuthorizationRequestReader.Read(parameters, config, clientId, redirectUri);
        }
        catch (OAuthException e)
        {
            @out.WriteError(e);
            context.Response.RedirectToClient(redirectUri, config.Issuer, parameters.GetOptional("state"),
                ("error", e.Error), ("error_description", e.Message));
            return;
        }

        if (config.Oidc.Strategy == OidcStrategy.WebForm)
        {
            RedirectToLogin(context, userData.GenerateRandomProfile());
            return;
        }

        var user = RememberedUser(context) ?? NewRandomUser(context);
        var code = tokens.CreateAuthorizationCode(request, user);
        context.Response.RedirectToClient(request.RedirectUri, config.Issuer, request.State, ("code", code.ToString()));
    }

    private UserProfile? RememberedUser(HttpContext context)
    {
        if (!config.Oidc.RememberUser) return null;
        var user = sessions.Find(context.Request.Cookies[SessionCookieName]);
        if (user is not null) @out.WriteInfo($"User {user.Uid} found in session.");
        return user;
    }

    private UserProfile NewRandomUser(HttpContext context)
    {
        var user = userData.GenerateRandomProfile();
        if (config.Oidc.RememberUser)
            context.Response.Cookies.Append(SessionCookieName, sessions.Create(user),
                new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
        return user;
    }

    /// <summary>
    /// Passes the authorization request on to the login form, with a suggested random profile.
    /// </summary>
    private void RedirectToLogin(HttpContext context, UserProfile suggestedProfile)
    {
        var query = new QueryBuilder();
        query.AddFromQuery(context.Request.QueryString.Value ?? string.Empty);
        if (context.Request.HasFormContentType)
            foreach (var (key, values) in context.Request.Form)
                query.Add(key, values.OfType<string>());
        foreach (var suggestedClaim in suggestedProfile.AllClaims())
            query.Add(suggestedClaim.Type, suggestedClaim.Value);

        context.Response.Redirect($"{config.Paths.Login}{query}");
    }
}

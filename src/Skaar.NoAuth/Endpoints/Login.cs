using System.Net;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Exceptions;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Models;
using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Endpoints;

/// <summary>
/// The login form used by the <see cref="OidcStrategy.WebForm"/> strategy.
/// The authorization request parameters are passed on in the query string.
/// </summary>
internal sealed class Login(
    ITokenRepository tokens,
    IUserDataFactory userData,
    Configuration config,
    ViewRenderer views,
    IWriter @out) : IEndpoint
{
    public bool IsMatch(HttpRequest request) => request.IsPath(config.Paths.Login);

    public Task Process(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method)) return views.Render(context, "Login.html");
        if (HttpMethods.IsPost(context.Request.Method)) return ProcessFormData(context);

        context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
        return Task.CompletedTask;
    }

    private async Task ProcessFormData(HttpContext context)
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

        var fallback = userData.GenerateRandomProfile();
        var user = new UserProfile(
            Fodselsnummer.TryParse(parameters.GetOptional("uid"), out var uid) ? uid : fallback.Uid,
            parameters.GetOptional("given_name").WithFallback(fallback.GivenName),
            parameters.GetOptional("family_name").WithFallback(fallback.FamilyName))
        {
            ExtraClaims = ParseClaims(parameters).ToList()
        };

        var code = tokens.CreateAuthorizationCode(request, user);
        context.Response.RedirectToClient(request.RedirectUri, config.Issuer, request.State, ("code", code.ToString()));
    }

    private IEnumerable<ClaimModel> ParseClaims(RequestParameters parameters)
    {
        var types = parameters["claim_type"];
        var values = parameters["claim_value"];
        for (var i = 0; i < Math.Min(types.Count, values.Count); i++)
        {
            var type = types[i];
            var value = values[i];
            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(value)) continue;
            if (ReservedClaims.IsReserved(type))
            {
                @out.WriteWarning($"Ignoring the reserved claim '{type}' from the login form.");
                continue;
            }

            yield return new ClaimModel(type, value);
        }
    }
}

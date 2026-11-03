using System.Net;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Serialization;

namespace Skaar.NoAuth.Endpoints;

internal sealed class TokenDump(Configuration config, ITokenRepository tokens) : IEndpoint
{
    public bool IsMatch(HttpRequest request) => request.IsPath(config.Paths.TokenDump);

    public Task Process(HttpContext context)
    {
        if (HttpMethods.IsDelete(context.Request.Method))
        {
            tokens.FlushAll();
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            return Task.CompletedTask;
        }

        return context.Response.Serialize(new
        {
            AccessTokens = tokens.AccessTokens.Select(t => new
            {
                t.ClientId,
                Sub = t.Subject,
                Aud = t.Audiences,
                Exp = t.Expires,
                Active = t.IsActive,
                t.Scopes,
                Token = t.ToString(),
                t.RefreshToken
            }),
            IdTokens = tokens.IdTokens.Select(t => new
            {
                Sub = t.UserProfile.Uid,
                Aud = t.ClientId,
                Exp = t.Expires,
                t.Iat,
                t.UserProfile.GivenName,
                t.UserProfile.FamilyName,
                Claims = t.UserProfile.ExtraClaims
                    .GroupBy(c => c.Type)
                    .ToDictionary(g => g.Key, g => g.Select(c => c.Value)),
                AuthorizationCode = t.Code?.ToString(),
                t.AccessToken,
                Token = t.ToString(),
                t.Nonce
            }),
            AuthorizationCodes = tokens.AuthorizationGrants.Select(g => new
            {
                Code = g.Code.ToString(),
                g.Request.ClientId,
                g.Request.RedirectUri,
                g.Request.Scopes,
                g.Request.Resources,
                g.Request.CodeChallengeMethod,
                Sub = g.User.Uid,
                g.IssuedAt,
                g.Redeemed
            })
        }, JsonOptions.CamelCase);
    }
}

using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Endpoints;

/// <summary>
/// OpenID Connect discovery, and OAuth authorization server metadata (RFC 8414). Both serve the same document.
/// </summary>
internal sealed class Discovery(Configuration config) : IEndpoint
{
    public bool IsMatch(HttpRequest request) =>
        HttpMethods.IsGet(request.Method) &&
        (request.IsPath(config.Paths.Discovery) || request.IsPath(config.Paths.AuthorizationServerMetadata));

    public Task Process(HttpContext context)
    {
        var paths = config.Paths;
        var request = context.Request;
        var body = new
        {
            config.Issuer,
            AuthorizationEndpoint = config.Url(paths.Authorize, request),
            TokenEndpoint = config.Url(paths.Token.FirstOrDefault(), request),
            UserinfoEndpoint = config.Url(paths.UserInfo, request),
            JwksUri = config.Url(paths.Jwks, request),
            IntrospectionEndpoint = config.Url(paths.Introspection, request),
            RevocationEndpoint = config.Url(paths.Revocation, request),
            RegistrationEndpoint = config.Url(paths.Registration, request),
            EndSessionEndpoint = config.Url(paths.EndSession, request),
            ScopesSupported = new[] { "openid", "profile", "offline_access" },
            ResponseTypesSupported = new[] { "code" },
            ResponseModesSupported = new[] { "query" },
            GrantTypesSupported = new[] { "authorization_code", "client_credentials", "refresh_token" },
            SubjectTypesSupported = new[] { "public" },
            IdTokenSigningAlgValuesSupported = new[] { "RS256" },
            CodeChallengeMethodsSupported = config.Strict ? new[] { Pkce.S256 } : new[] { Pkce.S256, Pkce.Plain },
            TokenEndpointAuthMethodsSupported = new[] { "client_secret_post", "client_secret_basic", "none" },
            RevocationEndpointAuthMethodsSupported = new[] { "client_secret_post", "client_secret_basic", "none" },
            IntrospectionEndpointAuthMethodsSupported = new[] { "client_secret_post", "client_secret_basic", "none" },
            AuthorizationResponseIssParameterSupported = true,
            ClientIdMetadataDocumentSupported = true
        };
        return context.Response.Serialize(body);
    }
}

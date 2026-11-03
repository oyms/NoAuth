using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Exceptions;
using Skaar.NoAuth.Extensions;

namespace Skaar.NoAuth.Endpoints;

/// <summary>
/// Dynamic client registration (RFC 7591), as used by MCP clients.
/// Every client is accepted. The metadata is not stored, since any client_id is accepted anyway.
/// </summary>
internal sealed class Registration(Configuration config, IWriter writer) : IEndpoint
{
    public bool IsMatch(HttpRequest request) =>
        HttpMethods.IsPost(request.Method) && request.IsPath(config.Paths.Registration);

    public async Task Process(HttpContext context)
    {
        JsonObject metadata;
        try
        {
            metadata = await JsonNode.ParseAsync(context.Request.Body) as JsonObject
                       ?? throw new OAuthException(OAuthException.InvalidClientMetadata, "Expected a json object");
        }
        catch (JsonException e)
        {
            throw new OAuthException(OAuthException.InvalidClientMetadata, e.Message);
        }

        var clientId = Guid.NewGuid().ToString();
        var authMethod = metadata["token_endpoint_auth_method"]?.GetValue<string>() ?? "client_secret_basic";
        metadata["client_id"] = clientId;
        metadata["client_id_issued_at"] = DateTimeOffset.Now.ToUnixTimeSeconds();
        metadata["token_endpoint_auth_method"] = authMethod;
        if (authMethod != "none")
        {
            metadata["client_secret"] = Guid.NewGuid().ToString();
            metadata["client_secret_expires_at"] = 0;
        }

        writer.WriteInfo($"Registered client {clientId} ({metadata["client_name"]?.ToString() ?? "unnamed"}).");
        context.Response.StatusCode = (int)HttpStatusCode.Created;
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.Serialize(metadata);
    }
}

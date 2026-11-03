using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Models;
using Skaar.NoAuth.Serialization;

namespace Skaar.NoAuth.Endpoints;

/// <summary>
/// Api for login and client profiles.
/// <c>GET</c> lists all, <c>POST</c> adds a login profile or clients,
/// <c>DELETE {path}/{name}</c> deletes a profile, and <c>{path}/schema.json</c> serves the json schema.
/// </summary>
internal sealed class LoginProfiles(IProfileStore profiles, Configuration config) : IEndpoint
{
    private const string SchemaResource = "loginProfiles.schema.json";

    public bool IsMatch(HttpRequest request) =>
        config.Paths.LoginProfile.Length > 0 &&
        request.Path.StartsWithSegments(new PathString(config.Paths.LoginProfile));

    public Task Process(HttpContext context)
    {
        context.Request.Path.StartsWithSegments(new PathString(config.Paths.LoginProfile), out var remainingPath);
        var method = context.Request.Method;

        if (remainingPath.Equals("/schema.json")) return RenderSchema(context);
        if (HttpMethods.IsDelete(method) && remainingPath.HasValue) return Delete(context, remainingPath.Value![1..]);
        if (HttpMethods.IsPost(method)) return Add(context);

        return context.Response.Serialize(new PredefinedProfiles(profiles.Logins, profiles.Clients),
            JsonOptions.CamelCase);
    }

    private async Task Add(HttpContext context)
    {
        try
        {
            if (await JsonNode.ParseAsync(context.Request.Body) is JsonObject json)
            {
                if (json.ContainsKey("profileName"))
                {
                    profiles.Add(json.Deserialize<LoginProfile>(JsonOptions.CamelCase)!);
                }
                else
                {
                    var clients = json.Deserialize<Dictionary<string, IDictionary<string, IEnumerable<string>>>>(
                        JsonOptions.CamelCase)!;
                    foreach (var (id, claims) in clients) profiles.Add(new ClientProfile(id, claims));
                }

                context.Response.StatusCode = (int)HttpStatusCode.Created;
                return;
            }
        }
        catch (JsonException)
        {
        }

        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        await context.Response.Serialize(new { Error = "Cannot read login or client profile." });
    }

    private Task Delete(HttpContext context, string name)
    {
        context.Response.StatusCode = profiles.Delete(name)
            ? (int)HttpStatusCode.NoContent
            : (int)HttpStatusCode.NotFound;
        return Task.CompletedTask;
    }

    private static async Task RenderSchema(HttpContext context)
    {
        await using var schema = typeof(LoginProfiles).Assembly.GetManifestResourceStream(SchemaResource)
                                 ?? throw new InvalidOperationException($"Cannot find resource {SchemaResource}");
        context.Response.ContentType = "application/schema+json; charset=utf-8";
        await schema.CopyToAsync(context.Response.Body);
    }
}

using System.Net;
using Microsoft.AspNetCore.Http.Extensions;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Exceptions;
using Skaar.NoAuth.Extensions;

namespace Skaar.NoAuth.Services;

/// <summary>
/// Routes requests to the first matching <see cref="IEndpoint"/>.
/// Paths are read from configuration, so all routes are configurable.
/// With Entra enabled, Entra paths are rewritten to the configured paths (see <see cref="EntraPaths"/>).
/// </summary>
internal sealed class EndpointRouter(IEnumerable<IEndpoint> endpoints, Configuration config, IWriter writer)
{
    private readonly IEndpoint[] endpoints = endpoints.ToArray();

    public async Task Invoke(HttpContext context, RequestDelegate next)
    {
        writer.WriteInfo($"Request: {context.Request.Method} {context.Request.GetDisplayUrl()}");
        if (config.Entra.Enabled) EntraPaths.TryRewrite(context.Request, config.Paths);
        var endpoint = endpoints.FirstOrDefault(e => e.IsMatch(context.Request));
        if (endpoint is null)
        {
            writer.WriteWarning("Not found");
            await next(context);
            return;
        }

        try
        {
            await endpoint.Process(context);
        }
        catch (OAuthException e)
        {
            writer.WriteError(e);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = (int)e.StatusCode;
                await context.Response.Serialize(new { e.Error, ErrorDescription = e.Message });
            }
        }
        catch (StatusCodeException e)
        {
            writer.WriteError(e);
            await WriteError(context, e.StatusCode, e.Message);
        }
        catch (Exception e)
        {
            writer.WriteError(e);
            await WriteError(context, HttpStatusCode.InternalServerError, e.Message);
        }
    }

    private static Task WriteError(HttpContext context, HttpStatusCode statusCode, string message)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;
        context.Response.StatusCode = (int)statusCode;
        return context.Response.Serialize(new { Error = message });
    }
}

using System.Net;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Serialization;

namespace Skaar.NoAuth.Endpoints;

internal sealed class LogDump(Configuration config, ILog log) : IEndpoint
{
    public bool IsMatch(HttpRequest request) => request.IsPath(config.Paths.LogDump);

    public Task Process(HttpContext context)
    {
        if (HttpMethods.IsDelete(context.Request.Method))
        {
            log.Flush();
            context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            return Task.CompletedTask;
        }

        return context.Response.Serialize(new
        {
            Messages = log.Select(m => new { m.Timestamp, m.Level, m.Message })
        }, JsonOptions.CamelCase);
    }
}

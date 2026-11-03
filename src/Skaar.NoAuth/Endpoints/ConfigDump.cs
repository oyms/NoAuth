using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Extensions;
using Skaar.NoAuth.Serialization;

namespace Skaar.NoAuth.Endpoints;

internal sealed class ConfigDump(Configuration config) : IEndpoint
{
    public bool IsMatch(HttpRequest request) => request.IsPath(config.Paths.ConfigDump);

    public Task Process(HttpContext context) => context.Response.Serialize(config, JsonOptions.CamelCase);
}

using System.Text;
using Skaar.NoAuth.Config;

namespace Skaar.NoAuth.Services;

/// <summary>
/// Renders embedded html views, replacing <c>{PathName}</c> placeholders with configured paths.
/// </summary>
internal sealed class ViewRenderer(Configuration config)
{
    public async Task Render(HttpContext context, string view)
    {
        var resourceName = $"Views.{view}";
        await using var stream = EmbeddedResources.Open(resourceName);
        using var reader = new StreamReader(stream);
        var html = new StringBuilder(await reader.ReadToEndAsync());
        foreach (var path in config.Paths.All().DistinctBy(p => p.Name))
            html.Replace($"{{{path.Name}}}", path.Path);

        context.Response.Headers.CacheControl = "no-cache";
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(html.ToString());
    }
}

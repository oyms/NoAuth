using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseNoAuth(this IApplicationBuilder app)
    {
        var router = app.ApplicationServices.GetRequiredService<EndpointRouter>();
        return app.Use((HttpContext context, RequestDelegate next) => router.Invoke(context, next));
    }
}

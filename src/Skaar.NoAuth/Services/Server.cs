using System.Text;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Extensions;

namespace Skaar.NoAuth.Services;

public sealed class Server : IAsyncDisposable
{
    private readonly WebApplication app;
    private readonly bool listensOnBaseUrl;

    private Server(WebApplication app, Configuration configuration, bool listensOnBaseUrl)
    {
        this.app = app;
        this.listensOnBaseUrl = listensOnBaseUrl;
        Configuration = configuration;
    }

    public Configuration Configuration { get; }

    /// <summary>
    /// Configuration is read from appsettings.json, environment variables and command line arguments.
    /// The server listens to <see cref="Config.Configuration.BaseUrl"/>, unless urls are set explicitly
    /// (e.g. with ASPNETCORE_URLS when running in a container).
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <param name="configureBuilder">Additional configuration of the builder, e.g. a test server.</param>
    public static Server Create(string[] args, Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });
        builder.Logging.ClearProviders();

        var configuration = builder.Configuration.GetSection(Configuration.SectionName).Get<Configuration>()
                            ?? throw new InvalidOperationException(
                                $"Missing configuration section '{Configuration.SectionName}'");
        configuration.Paths.Validate();

        var listensOnBaseUrl = string.IsNullOrWhiteSpace(builder.Configuration[WebHostDefaults.ServerUrlsKey]);
        if (listensOnBaseUrl) builder.WebHost.UseUrls(configuration.BaseUrl.AbsoluteUri);

        builder.Services.AddNoAuth(configuration, ProfileSource.Resolve(args));
        configureBuilder?.Invoke(builder);

        var app = builder.Build();
        app.UseNoAuth();
        app.Lifetime.ApplicationStarted.Register(() =>
            RenderStatus(app.Services.GetRequiredService<IWriter>(), configuration, app.Urls));

        return new Server(app, configuration, listensOnBaseUrl);
    }

    public async Task Run()
    {
        if (listensOnBaseUrl) await new ServerListener(Configuration).EnsureUrlAvailable();
        await app.RunAsync();
    }

    internal WebApplication App => app;

    public ValueTask DisposeAsync() => app.DisposeAsync();

    public static void BuildStatusString(StringBuilder bld, Configuration configuration, int lineLength)
    {
        string? previousName = null;
        foreach (var (name, path) in configuration.Paths.All())
        {
            var label = name == previousName ? string.Empty : $"{name}:";
            bld.AppendLine($"{label.PadRight(lineLength)} {configuration.Url(path)}");
            previousName = name;
        }
    }

    private static void RenderStatus(IWriter writer, Configuration configuration, IEnumerable<string> urls)
    {
        const int lineLength = 30;
        var bld = new StringBuilder();
        bld.AppendLine(new string('=', lineLength));
        bld.AppendLine("NoAuth server started");
        bld.AppendLine(new string('-', lineLength));
        bld.AppendLine($"{"Listening:".PadRight(lineLength)} {string.Join(", ", urls)}");
        bld.AppendLine($"{"Issuer:".PadRight(lineLength)} {configuration.Issuer}");
        BuildStatusString(bld, configuration, lineLength);
        bld.AppendLine(new string('=', lineLength));
        writer.WriteInfo(bld.ToString());
    }
}

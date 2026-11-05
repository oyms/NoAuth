using Microsoft.Extensions.Configuration.Json;

namespace Skaar.NoAuth.Config;

/// <summary>
/// A settings file overriding the defaults in appsettings.json (which is installed with the application).
/// Given with <c>--config &lt;file&gt;</c>, otherwise <see cref="FileName"/> in the working directory, if present.
/// It takes precedence over appsettings.json, but not over environment variables and command line arguments.
/// </summary>
public static class SettingsFile
{
    public const string FileName = "noauth.json";
    public const string ArgumentKey = "config";

    /// <summary>Adds the settings file to the configuration.</summary>
    /// <returns>The full path of the file, or <c>null</c> if no file is used.</returns>
    public static string? Add(ConfigurationManager configuration)
    {
        var explicitPath = configuration[ArgumentKey];
        var path = Path.GetFullPath(explicitPath ?? FileName);
        if (explicitPath is null && !File.Exists(path)) return null;
        if (!File.Exists(path)) throw new FileNotFoundException($"The settings file '{path}' does not exist.", path);

        var source = new JsonConfigurationSource { Path = path, Optional = false };
        source.ResolveFileProvider();
        configuration.Sources.Insert(IndexAfterAppSettings(configuration.Sources), source);
        return path;
    }

    private static int IndexAfterAppSettings(IList<IConfigurationSource> sources)
    {
        for (var i = sources.Count - 1; i >= 0; i--)
        {
            if (sources[i] is JsonConfigurationSource) return i + 1;
        }

        return 0;
    }
}

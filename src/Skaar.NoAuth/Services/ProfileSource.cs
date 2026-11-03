namespace Skaar.NoAuth.Services;

/// <summary>
/// The location of the file with predefined login and client profiles.
/// </summary>
public sealed record ProfileSource(string FilePath)
{
    public const string FileName = "loginProfiles.json";

    /// <summary>
    /// Uses the first positional command line argument (a file or a folder) if present.
    /// Otherwise <see cref="FileName"/> in the working directory, then in the application directory.
    /// </summary>
    public static ProfileSource Resolve(string[] args) => FromArgs(args).Source;

    /// <summary>
    /// Resolves the profiles file, and returns the remaining arguments for configuration.
    /// The path must be removed, since the configuration reads an argument starting with <c>/</c>
    /// (an absolute path on Linux and macOS) as a key, which swallows the next argument.
    /// </summary>
    public static (ProfileSource Source, string[] ConfigurationArgs) FromArgs(string[] args)
    {
        var index = FirstPositionalArgument(args);
        return index is { } i
            ? (FromPath(args[i]), [.. args[..i], .. args[(i + 1)..]])
            : (Default(), args);
    }

    private static ProfileSource FromPath(string path)
    {
        path = Path.GetFullPath(path);
        return new ProfileSource(Directory.Exists(path) ? Path.Combine(path, FileName) : path);
    }

    private static ProfileSource Default()
    {
        var inWorkingDirectory = Path.GetFullPath(FileName);
        return new ProfileSource(File.Exists(inWorkingDirectory)
            ? inWorkingDirectory
            : Path.Combine(AppContext.BaseDirectory, FileName));
    }

    private static int? FirstPositionalArgument(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith('-'))
            {
                // Skip the value of "--key value" style configuration arguments.
                if (!arg.Contains('=')) i++;
                continue;
            }

            if (!arg.Contains('=')) return i;
        }

        return null;
    }
}

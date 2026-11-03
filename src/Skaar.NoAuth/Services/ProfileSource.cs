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
    public static ProfileSource Resolve(string[] args)
    {
        var path = FirstPositionalArgument(args);
        if (path is not null)
        {
            path = Path.GetFullPath(path);
            return new ProfileSource(Directory.Exists(path) ? Path.Combine(path, FileName) : path);
        }

        var inWorkingDirectory = Path.GetFullPath(FileName);
        return new ProfileSource(File.Exists(inWorkingDirectory)
            ? inWorkingDirectory
            : Path.Combine(AppContext.BaseDirectory, FileName));
    }

    private static string? FirstPositionalArgument(string[] args)
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

            if (!arg.Contains('=')) return arg;
        }

        return null;
    }
}

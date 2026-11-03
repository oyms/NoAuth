namespace Skaar.NoAuth.Tests;

/// <summary>A temporary login profiles file.</summary>
internal sealed class ProfilesFile : IDisposable
{
    public ProfilesFile(string json)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"noauth-profiles-{Guid.NewGuid()}.json");
        File.WriteAllText(Path, json);
    }

    public string Path { get; }

    public void Dispose() => File.Delete(Path);
}

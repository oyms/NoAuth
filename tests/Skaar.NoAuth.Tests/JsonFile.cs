namespace Skaar.NoAuth.Tests;

/// <summary>A temporary json file, e.g. login profiles or settings.</summary>
internal sealed class JsonFile : IDisposable
{
    public JsonFile(string json)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"noauth-{Guid.NewGuid()}.json");
        File.WriteAllText(Path, json);
    }

    public string Path { get; }

    public void Dispose() => File.Delete(Path);
}

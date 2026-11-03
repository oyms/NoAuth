namespace Skaar.NoAuth.Services;

/// <summary>
/// Files embedded in the assembly (see the <c>EmbeddedResource</c> items in the project file).
/// </summary>
internal static class EmbeddedResources
{
    public static Stream Open(string name) =>
        typeof(EmbeddedResources).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"Cannot find resource {name}");
}

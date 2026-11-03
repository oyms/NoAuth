namespace Skaar.NoAuth.Config;

/// <summary>
/// The paths of all endpoints. An empty path disables the endpoint.
/// </summary>
public record Paths
{
    public string FrontPage { get; set; } = string.Empty;
    public string Discovery { get; set; } = string.Empty;
    public string AuthorizationServerMetadata { get; set; } = string.Empty;
    public string Jwks { get; set; } = string.Empty;
    public string[] Token { get; set; } = [];
    public string Authorize { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string EndSession { get; set; } = string.Empty;
    public string LogOutRedirect { get; set; } = string.Empty;
    public string Introspection { get; set; } = string.Empty;
    public string Revocation { get; set; } = string.Empty;
    public string Registration { get; set; } = string.Empty;
    public string UserInfo { get; set; } = string.Empty;
    public string TokenDump { get; set; } = string.Empty;
    public string LogDump { get; set; } = string.Empty;
    public string ConfigDump { get; set; } = string.Empty;
    public string TokenRequestApi { get; set; } = string.Empty;
    public string LoginProfile { get; set; } = string.Empty;

    /// <summary>
    /// All configured paths, by property name. Properties with multiple paths yield one entry per path.
    /// </summary>
    public IEnumerable<(string Name, string Path)> All()
    {
        foreach (var property in typeof(Paths).GetProperties())
        {
            switch (property.GetValue(this))
            {
                case string path when path.Length > 0:
                    yield return (property.Name, path);
                    break;
                case IEnumerable<string> paths:
                    foreach (var path in paths.Where(p => p.Length > 0)) yield return (property.Name, path);
                    break;
            }
        }
    }

    public void Validate()
    {
        var invalid = All().Where(p => !p.Path.StartsWith('/')).ToList();
        if (invalid.Count > 0)
            throw new InvalidOperationException(
                $"Paths must start with '/': {string.Join(", ", invalid.Select(p => $"{p.Name}={p.Path}"))}");
    }
}

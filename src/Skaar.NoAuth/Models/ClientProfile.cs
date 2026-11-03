namespace Skaar.NoAuth.Models;

/// <summary>
/// Extra claims granted to tokens issued to a specific client_id.
/// </summary>
public record ClientProfile(string Id, IDictionary<string, IEnumerable<string>> Claims);

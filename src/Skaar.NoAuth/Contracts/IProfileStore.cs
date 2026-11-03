using System.Diagnostics.CodeAnalysis;
using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Contracts;

/// <summary>
/// Login and client profiles, initially read from the profiles file.
/// </summary>
public interface IProfileStore
{
    IReadOnlyCollection<LoginProfile> Logins { get; }
    IReadOnlyCollection<ClientProfile> Clients { get; }
    void Add(LoginProfile login);
    void Add(ClientProfile client);

    /// <summary>Deletes login profiles with the profile name and client profiles with the id.</summary>
    bool Delete(string name);

    bool TryGetClient(string clientId, [NotNullWhen(true)] out ClientProfile? client);
}

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Models;
using Skaar.NoAuth.Serialization;

namespace Skaar.NoAuth.Services;

internal sealed class ProfileStore(ProfileSource source, IWriter @out) : IProfileStore
{
    private readonly Lock gate = new();
    private List<LoginProfile>? logins;
    private List<ClientProfile>? clients;

    public IReadOnlyCollection<LoginProfile> Logins
    {
        get
        {
            lock (gate) return [..EnsureLoaded().Logins];
        }
    }

    public IReadOnlyCollection<ClientProfile> Clients
    {
        get
        {
            lock (gate) return [..EnsureLoaded().Clients];
        }
    }

    public void Add(LoginProfile login)
    {
        lock (gate) EnsureLoaded().Logins.Add(login);
    }

    public void Add(ClientProfile client)
    {
        lock (gate)
        {
            var all = EnsureLoaded().Clients;
            all.RemoveAll(c => c.Id == client.Id);
            all.Add(client);
        }
    }

    public bool Delete(string name)
    {
        lock (gate)
        {
            var (allLogins, allClients) = EnsureLoaded();
            return allLogins.RemoveAll(p => p.ProfileName == name) +
                allClients.RemoveAll(c => c.Id == name) > 0;
        }
    }

    public bool TryGetClient(string clientId, [NotNullWhen(true)] out ClientProfile? client)
    {
        lock (gate) client = EnsureLoaded().Clients.FirstOrDefault(c => c.Id == clientId);
        return client is not null;
    }

    private (List<LoginProfile> Logins, List<ClientProfile> Clients) EnsureLoaded()
    {
        if (logins is null || clients is null)
        {
            var profiles = Load();
            logins = profiles.Logins.ToList();
            clients = profiles.Clients.ToList();
        }

        return (logins, clients);
    }

    private PredefinedProfiles Load()
    {
        if (!File.Exists(source.FilePath))
        {
            @out.WriteWarning($"Couldn't find login profiles at {source.FilePath}.");
            return PredefinedProfiles.Empty;
        }

        try
        {
            using var file = File.OpenRead(source.FilePath);
            var profiles = JsonSerializer.Deserialize<PredefinedProfiles>(file, JsonOptions.CamelCase);
            @out.WriteInfo($"Login profiles read from {source.FilePath}.");
            return profiles ?? PredefinedProfiles.Empty;
        }
        catch (Exception e)
        {
            @out.WriteWarning($"Couldn't read login profiles from {source.FilePath}.");
            @out.WriteError(e);
            return PredefinedProfiles.Empty;
        }
    }
}

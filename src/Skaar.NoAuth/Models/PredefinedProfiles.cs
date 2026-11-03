using System.Text.Json.Serialization;
using Skaar.NoAuth.Serialization;

namespace Skaar.NoAuth.Models;

[JsonConverter(typeof(PredefinedProfilesJsonConverter))]
public record PredefinedProfiles(IEnumerable<LoginProfile> Logins, IEnumerable<ClientProfile> Clients)
{
    public static PredefinedProfiles Empty { get; } = new([], []);
}

using Skaar.ValueType;

namespace Skaar.NoAuth.Models;

[ValueType<Guid>]
public partial struct AuthorizationCode
{
    public bool IsValid => _value != Guid.Empty;
    public static AuthorizationCode NewCode() => new(Guid.NewGuid());
}
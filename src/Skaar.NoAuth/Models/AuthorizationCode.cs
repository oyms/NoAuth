namespace Skaar.NoAuth.Models;

public readonly record struct AuthorizationCode(Guid Value)
{
    public static AuthorizationCode NewCode() => new(Guid.NewGuid());

    public bool IsValid => Value != Guid.Empty;

    public override string ToString() => Value.ToString();

    public static bool TryParse(string? code, out AuthorizationCode result)
    {
        result = Guid.TryParse(code, out var guid) ? new AuthorizationCode(guid) : default;
        return result.IsValid;
    }
}

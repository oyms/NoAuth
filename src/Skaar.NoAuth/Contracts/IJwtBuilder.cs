using System.Security.Claims;

namespace Skaar.NoAuth.Contracts;

public interface IJwtBuilder
{
    /// <summary>The <c>typ</c> header. Defaults to <c>JWT</c>.</summary>
    IJwtBuilder Type(string type);

    IJwtBuilder Aud(params IEnumerable<string> audiences);
    IJwtBuilder Sub(string subject);
    IJwtBuilder Exp(DateTimeOffset expires);

    /// <summary>Scopes are written as one space separated <c>scope</c> claim (RFC 9068).</summary>
    IJwtBuilder Scope(string scope);

    IJwtBuilder Claim(Claim claim);
    string Build();
}

public interface IJwtBuilderFactory
{
    IJwtBuilder NewBuilder();
}

using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;

namespace Skaar.NoAuth.Services;

internal sealed class JwtBuilderFactory(Configuration config, IKeyRepository keys, TimeProvider time) : IJwtBuilderFactory
{
    private static readonly JsonWebTokenHandler Handler = new();

    public IJwtBuilder NewBuilder() => new Builder(config, keys, time);

    private sealed class Builder(Configuration config, IKeyRepository keys, TimeProvider time) : IJwtBuilder
    {
        private readonly List<Claim> claims = [];
        private readonly List<string> audiences = [];
        private readonly List<string> scopes = [];
        private string? type;
        private DateTimeOffset? expires;

        public IJwtBuilder Type(string value)
        {
            type = value;
            return this;
        }

        public IJwtBuilder Aud(params IEnumerable<string> values)
        {
            audiences.AddRange(values.Except(audiences));
            return this;
        }

        public IJwtBuilder Sub(string subject) => Claim(new Claim(JwtRegisteredClaimNames.Sub, subject));

        public IJwtBuilder Exp(DateTimeOffset value)
        {
            expires = value;
            return this;
        }

        public IJwtBuilder Scope(string scope)
        {
            if (!scopes.Contains(scope)) scopes.Add(scope);
            return this;
        }

        public IJwtBuilder Claim(Claim claim)
        {
            if (!claims.Any(c => c.Type == claim.Type && c.Value == claim.Value)) claims.Add(claim);
            return this;
        }

        public string Build()
        {
            var subject = new ClaimsIdentity(claims);
            subject.AddClaim(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
            if (scopes.Count > 0) subject.AddClaim(new Claim("scope", string.Join(' ', scopes)));

            // An already expired token must still be issued before it expires.
            var now = time.GetUtcNow();
            var issuedAt = expires <= now ? expires.Value.AddMinutes(-1) : now;
            var descriptor = new SecurityTokenDescriptor
            {
                TokenType = type,
                Issuer = config.Issuer,
                IssuedAt = issuedAt.UtcDateTime,
                NotBefore = issuedAt.UtcDateTime,
                Expires = expires?.UtcDateTime,
                Subject = subject,
                SigningCredentials = keys.SigningCredentials
            };

            if (audiences.Count == 1) descriptor.Audience = audiences[0];
            else foreach (var audience in audiences) descriptor.Audiences.Add(audience);

            return Handler.CreateToken(descriptor);
        }
    }
}

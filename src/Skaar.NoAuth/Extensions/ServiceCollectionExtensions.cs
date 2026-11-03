using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Endpoints;
using Skaar.NoAuth.Services;
using Skaar.NoAuth.Services.DataGeneration;

namespace Skaar.NoAuth.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNoAuth(this IServiceCollection services, Configuration config,
        ProfileSource profileSource) =>
        services
            .AddSingleton(config)
            .AddSingleton(profileSource)
            .AddSingleton<ILog, Log>()
            .AddTransient<IWriter, LoggingWriter>()
            .AddSingleton<IKeyRepository, KeyRepository>()
            .AddSingleton<IJwtBuilderFactory, JwtBuilderFactory>()
            .AddSingleton<ITokenRepository, TokenRepository>()
            .AddSingleton<IProfileStore, ProfileStore>()
            .AddSingleton<ISessionStore, SessionStore>()
            .AddSingleton<PersonDataGenerator>()
            .AddSingleton<IUserDataFactory, UserDataFactory>()
            .AddSingleton<ViewRenderer>()
            .AddSingleton<EndpointRouter>()
            .AddEndpoints();

    private static IServiceCollection AddEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, FrontPage>()
            .AddSingleton<IEndpoint, Discovery>()
            .AddSingleton<IEndpoint, Jwks>()
            .AddSingleton<IEndpoint, Token>()
            .AddSingleton<IEndpoint, Authorize>()
            .AddSingleton<IEndpoint, Login>()
            .AddSingleton<IEndpoint, EndSession>()
            .AddSingleton<IEndpoint, SessionEnded>()
            .AddSingleton<IEndpoint, Introspection>()
            .AddSingleton<IEndpoint, Revocation>()
            .AddSingleton<IEndpoint, Registration>()
            .AddSingleton<IEndpoint, UserInfo>()
            .AddSingleton<IEndpoint, TokenDump>()
            .AddSingleton<IEndpoint, LogDump>()
            .AddSingleton<IEndpoint, ConfigDump>()
            .AddSingleton<IEndpoint, TokenApi>()
            .AddSingleton<IEndpoint, LoginProfiles>();
}

using Microsoft.Extensions.DependencyInjection.Extensions;
using Skaar.NoAuth.Config;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Services;
using Skaar.NoAuth.Services.DataGeneration;

namespace Skaar.NoAuth.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNoAuth(this IServiceCollection services, Configuration config,
        ProfileSource profileSource)
    {
        services.TryAddSingleton(TimeProvider.System);
        return services
            .AddSingleton(config)
            .AddSingleton(profileSource)
            .AddSingleton<ILog, Log>()
            .AddTransient<IWriter, LoggingWriter>()
            .AddSingleton<IKeyRepository, KeyRepository>()
            .AddSingleton<IJwtBuilderFactory, JwtBuilderFactory>()
            .AddSingleton<TokenIssuer>()
            .AddSingleton<ITokenRepository, TokenRepository>()
            .AddSingleton<IProfileStore, ProfileStore>()
            .AddSingleton<ISessionStore, SessionStore>()
            .AddSingleton<PersonDataGenerator>()
            .AddSingleton<IUserDataFactory, UserDataFactory>()
            .AddSingleton<ViewRenderer>()
            .AddSingleton<EndpointRouter>()
            .AddEndpoints();
    }

    /// <summary>
    /// Registers every <see cref="IEndpoint"/> in the assembly. Endpoints match distinct paths, so the order
    /// does not matter, but it is kept stable by sorting on the name.
    /// </summary>
    private static IServiceCollection AddEndpoints(this IServiceCollection services)
    {
        var endpoints = typeof(IEndpoint).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IEndpoint)))
            .OrderBy(t => t.FullName, StringComparer.Ordinal);
        foreach (var endpoint in endpoints) services.AddSingleton(typeof(IEndpoint), endpoint);
        return services;
    }
}

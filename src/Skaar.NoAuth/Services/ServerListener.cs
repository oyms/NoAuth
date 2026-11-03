using System.Net.Http;
using Skaar.NoAuth.Config;

namespace Skaar.NoAuth.Services;

/// <summary>
/// Checks that nothing (including another instance of NoAuth) is listening to the base url.
/// </summary>
internal sealed class ServerListener(Configuration config)
{
    public async Task EnsureUrlAvailable()
    {
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(5);
        var probe = config.Url(config.Paths.ConfigDump) ?? config.BaseUrl;
        try
        {
            using var response = await client.GetAsync(probe);
            throw new InvalidOperationException(response.IsSuccessStatusCode
                ? $"NoAuth is already listening to {config.BaseUrl}"
                : $"Url {config.BaseUrl} is in use");
        }
        catch (HttpRequestException e) when (e.HttpRequestError is HttpRequestError.ConnectionError
                                                 or HttpRequestError.NameResolutionError)
        {
            // Nothing is listening.
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Url {config.BaseUrl} is in use", e);
        }
    }
}

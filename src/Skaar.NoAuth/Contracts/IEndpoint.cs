namespace Skaar.NoAuth.Contracts;

/// <summary>
/// An endpoint handled by the custom routing in <see cref="Services.EndpointRouter"/>.
/// The first endpoint matching a request processes it.
/// </summary>
public interface IEndpoint
{
    bool IsMatch(HttpRequest request);
    Task Process(HttpContext context);
}

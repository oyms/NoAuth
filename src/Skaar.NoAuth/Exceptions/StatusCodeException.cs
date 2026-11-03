using System.Net;

namespace Skaar.NoAuth.Exceptions;

public class StatusCodeException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

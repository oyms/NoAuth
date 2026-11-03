using System.Net;

namespace Skaar.NoAuth.Exceptions;

/// <summary>
/// An OAuth error response, e.g. <c>{"error":"invalid_grant","error_description":"..."}</c> (RFC 6749 5.2).
/// </summary>
public class OAuthException(string error, string description, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
    : StatusCodeException(statusCode, description)
{
    public const string InvalidRequest = "invalid_request";
    public const string InvalidClient = "invalid_client";
    public const string InvalidGrant = "invalid_grant";
    public const string InvalidTarget = "invalid_target";
    public const string UnsupportedGrantType = "unsupported_grant_type";
    public const string UnsupportedResponseType = "unsupported_response_type";
    public const string InvalidClientMetadata = "invalid_client_metadata";

    public string Error { get; } = error;
}

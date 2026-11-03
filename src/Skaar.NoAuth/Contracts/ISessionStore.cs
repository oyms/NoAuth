using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Contracts;

/// <summary>
/// Users remembered between logins, identified by a session cookie.
/// </summary>
public interface ISessionStore
{
    string Create(UserProfile user);
    UserProfile? Find(string? sessionId);
    void End(string? sessionId);
    void EndAllFor(string subject);
}

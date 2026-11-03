using System.Collections.Concurrent;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Services;

internal sealed class SessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, UserProfile> sessions = new();

    public string Create(UserProfile user)
    {
        var sessionId = Guid.NewGuid().ToString();
        sessions[sessionId] = user;
        return sessionId;
    }

    public UserProfile? Find(string? sessionId) =>
        sessionId is null ? null : sessions.GetValueOrDefault(sessionId);

    public void End(string? sessionId)
    {
        if (sessionId is not null) sessions.TryRemove(sessionId, out _);
    }

    public void EndAllFor(string subject)
    {
        foreach (var (sessionId, user) in sessions)
            if (user.Uid.Equals(subject)) sessions.TryRemove(sessionId, out _);
    }
}

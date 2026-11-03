using System.Collections;
using System.Collections.Concurrent;
using Skaar.NoAuth.Contracts;
using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Services;

internal sealed class Log : ILog
{
    private readonly ConcurrentQueue<LogMessage> messages = new();

    public void Add(LogMessage message) => messages.Enqueue(message);

    public void Flush() => messages.Clear();

    public IEnumerator<LogMessage> GetEnumerator() => messages.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

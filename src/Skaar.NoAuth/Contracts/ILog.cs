using Skaar.NoAuth.Models;

namespace Skaar.NoAuth.Contracts;

public interface ILog : IEnumerable<LogMessage>
{
    void Add(LogMessage message);
    void Flush();
}

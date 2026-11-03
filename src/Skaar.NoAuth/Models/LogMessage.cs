namespace Skaar.NoAuth.Models;

public record LogMessage(string Level, string Message)
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
}

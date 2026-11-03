using Skaar.NoAuth.Contracts;

namespace Skaar.NoAuth.Services;

/// <summary>
/// Writes to the console and to the log exposed by the log api.
/// </summary>
internal sealed class LoggingWriter(ILog log) : ConsoleWriter
{
    public override void WriteInfo(string text)
    {
        log.Add(new("Info", text));
        base.WriteInfo(text);
    }

    public override void WriteWarning(string text)
    {
        log.Add(new("Warning", text));
        base.WriteWarning(text);
    }

    public override void WriteError(Exception e)
    {
        log.Add(new("Error", e.Message));
        base.WriteError(e);
    }
}

using System.Diagnostics;
using Skaar.NoAuth.Contracts;

namespace Skaar.NoAuth.Services;

public class ConsoleWriter : IWriter
{
    public virtual void WriteInfo(string text) => Console.WriteLine(text);

    public virtual void WriteWarning(string text)
    {
        using (new Color(ConsoleColor.Blue))
        {
            Console.WriteLine(text);
        }
    }

    public virtual void WriteError(Exception e)
    {
        using (new Color(ConsoleColor.Red))
        {
            Console.WriteLine(e.Message);
        }

        WriteExceptionDetails(e);
    }

    [Conditional("DEBUG")]
    private static void WriteExceptionDetails(Exception exception)
    {
        using (new Color(ConsoleColor.Magenta))
        {
            Console.WriteLine(exception.StackTrace ?? "***");
        }
    }

    private sealed class Color : IDisposable
    {
        public Color(ConsoleColor foreground) => Console.ForegroundColor = foreground;
        public void Dispose() => Console.ResetColor();
    }
}

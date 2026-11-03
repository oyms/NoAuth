namespace Skaar.NoAuth.Contracts;

public interface IWriter
{
    void WriteInfo(string text);
    void WriteWarning(string text);
    void WriteError(Exception e);
}

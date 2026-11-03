using Skaar.NoAuth.Services;

var writer = new ConsoleWriter();

try
{
    await using var server = Server.Create(args);
    await server.Run();
}
catch (Exception e)
{
    writer.WriteError(e);
    return 1;
}

writer.WriteInfo("Server terminated.");
return 0;

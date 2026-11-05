using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Tests;

public class SettingsFileTests
{
    [Fact]
    public async Task The_settings_file_overrides_the_defaults()
    {
        using var file = new JsonFile("""{ "NoAuth": { "Issuer": "https://from-file" } }""");
        await using var host = await NoAuthHost.Start("--config", file.Path);

        var metadata = await host.GetJson("/.well-known/openid-configuration");

        metadata.Str("issuer").ShouldBe("https://from-file");
        metadata.Str("token_endpoint").ShouldBe("https://localhost:44367/connect/token");
    }

    [Fact]
    public async Task Command_line_arguments_override_the_settings_file()
    {
        using var file = new JsonFile("""{ "NoAuth": { "Issuer": "https://from-file" } }""");
        await using var host = await NoAuthHost.Start($"--config={file.Path}", "--NoAuth:Issuer=https://from-args");

        var metadata = await host.GetJson("/.well-known/openid-configuration");

        metadata.Str("issuer").ShouldBe("https://from-args");
    }

    [Fact]
    public void A_missing_settings_file_is_an_error() =>
        Should.Throw<FileNotFoundException>(() => Server.Create(["--config", "does-not-exist.json"]));
}

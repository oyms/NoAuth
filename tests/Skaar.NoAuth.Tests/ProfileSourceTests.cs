using Skaar.NoAuth.Services;

namespace Skaar.NoAuth.Tests;

public class ProfileSourceTests
{
    [Fact]
    public void The_first_positional_argument_is_the_profiles_file() =>
        ProfileSource.Resolve(["--NoAuth:Strict=true", "profiles.json"]).FilePath
            .ShouldBe(Path.GetFullPath("profiles.json"));

    [Fact]
    public void Values_of_configuration_arguments_are_skipped() =>
        ProfileSource.Resolve(["--NoAuth:Issuer", "https://issuer", "profiles.json"]).FilePath
            .ShouldBe(Path.GetFullPath("profiles.json"));

    [Fact]
    public void A_folder_is_combined_with_the_default_file_name()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            ProfileSource.Resolve([folder]).FilePath.ShouldBe(Path.Combine(folder, ProfileSource.FileName));
        }
        finally
        {
            Directory.Delete(folder);
        }
    }

    [Fact]
    public void Without_arguments_the_default_file_name_is_used() =>
        Path.GetFileName(ProfileSource.Resolve([]).FilePath).ShouldBe(ProfileSource.FileName);
}

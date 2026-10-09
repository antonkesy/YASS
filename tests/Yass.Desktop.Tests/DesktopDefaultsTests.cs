using Yass.Core.Config;

namespace Yass.Desktop.Tests;

public sealed class DesktopDefaultsTests
{
    [Fact]
    public void The_server_sessions_folder_becomes_the_per_user_one()
    {
        YassConfig applied = DesktopDefaults.Apply(new YassConfig(), "/home/me/.local/share/yass/sessions", 8);
        Assert.Equal("/home/me/.local/share/yass/sessions", applied.Sessions.Directory);
    }

    [Fact]
    public void A_sessions_folder_the_file_sets_is_kept()
    {
        YassConfig config = new YassConfig() with { Sessions = new SessionsConfig { Directory = "/srv/scans" } };
        Assert.Equal("/srv/scans", DesktopDefaults.Apply(config, "/elsewhere", 8).Sessions.Directory);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(4, 2)]
    [InlineData(6, 3)]
    [InlineData(16, DesktopDefaults.MaxRenders)]
    public void Renders_follow_the_cores_within_the_memory_budget(int cores, int renders) =>
        Assert.Equal(renders, DesktopDefaults.Apply(new YassConfig(), "/s", cores).Processing.MaxConcurrentRenders);

    [Fact]
    public void A_render_limit_the_file_sets_is_kept()
    {
        YassConfig config = new YassConfig() with { Processing = new ProcessingConfig { MaxConcurrentRenders = 1 } };
        Assert.Equal(1, DesktopDefaults.Apply(config, "/s", 32).Processing.MaxConcurrentRenders);
    }

    [Fact]
    public void Config_and_data_live_in_per_user_folders()
    {
        Assert.Equal("yass.yaml", Path.GetFileName(DesktopDefaults.ConfigPath()));
        Assert.True(Path.IsPathRooted(DesktopDefaults.ConfigPath()));
        Assert.EndsWith(Path.Combine("yass", "sessions"), DesktopDefaults.SessionsDirectory(), StringComparison.Ordinal);
    }
}

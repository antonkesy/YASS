using Yass.Core.Config;

namespace Yass.Desktop.Tests;

public sealed class DesktopDefaultsTests
{
    [Fact]
    public void The_server_sessions_folder_becomes_the_per_user_one()
    {
        YassConfig applied = DesktopDefaults.Apply(new YassConfig(), "/home/me/.local/share/yass/sessions");
        Assert.Equal("/home/me/.local/share/yass/sessions", applied.Sessions.Directory);
    }

    [Fact]
    public void A_sessions_folder_the_file_sets_is_kept()
    {
        YassConfig config = new YassConfig() with { Sessions = new SessionsConfig { Directory = "/srv/scans" } };
        Assert.Equal("/srv/scans", DesktopDefaults.Apply(config, "/elsewhere").Sessions.Directory);
    }

    [Fact]
    public void Config_and_data_live_in_per_user_folders()
    {
        Assert.Equal("yass.yaml", Path.GetFileName(DesktopDefaults.ConfigPath()));
        Assert.True(Path.IsPathRooted(DesktopDefaults.ConfigPath()));
        Assert.EndsWith(Path.Combine("yass", "sessions"), DesktopDefaults.SessionsDirectory(), StringComparison.Ordinal);
    }
}

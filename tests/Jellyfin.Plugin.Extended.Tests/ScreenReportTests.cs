using Xunit;

namespace Jellyfin.Plugin.Extended.Tests;

public class ScreenReportTests
{
    [Theory]
    [InlineData(HdrMode.Auto, "sdr", true)]
    [InlineData(HdrMode.Auto, "SDR", true)]
    [InlineData(HdrMode.Auto, "hdr", false)]
    [InlineData(HdrMode.Auto, null, false)]
    [InlineData(HdrMode.Auto, "", false)]
    [InlineData(HdrMode.Auto, "unexpected", false)]
    [InlineData(HdrMode.On, "sdr", false)]
    [InlineData(HdrMode.On, null, false)]
    [InlineData(HdrMode.Off, "hdr", true)]
    [InlineData(HdrMode.Off, null, true)]
    public void LimitsToSdr_FollowsTheModeAndTheReportedScreen(HdrMode mode, string? screen, bool expected)
    {
        Assert.Equal(expected, ScreenReport.LimitsToSdr(mode, screen));
    }
}

using System;

namespace Jellyfin.Plugin.Extended;

/// <summary>
/// Decides what a playback request does with the screen the web client reported.
/// </summary>
public static class ScreenReport
{
    /// <summary>
    /// The request header the web client reports its screen in.
    /// </summary>
    public const string HeaderName = "X-Extended-Screen";

    /// <summary>
    /// Gets whether a playback request should be limited to SDR.
    /// </summary>
    /// <param name="mode">The device's setting.</param>
    /// <param name="reportedScreen">The reported screen, or null when the client reported nothing.</param>
    /// <returns>True when the request should ask for SDR.</returns>
    public static bool LimitsToSdr(HdrMode mode, string? reportedScreen)
        => mode == HdrMode.Off
            || (mode == HdrMode.Auto && string.Equals(reportedScreen, "sdr", StringComparison.OrdinalIgnoreCase));
}

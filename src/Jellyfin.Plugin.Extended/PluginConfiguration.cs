using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Extended;

/// <summary>
/// What a device should do with HDR video.
/// </summary>
public enum HdrMode
{
    /// <summary>
    /// Leave the device's request unchanged.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Never change what the device asks for.
    /// </summary>
    On = 1,

    /// <summary>
    /// Always ask for SDR.
    /// </summary>
    Off = 2
}

/// <summary>
/// A device's saved setting.
/// </summary>
public class DeviceOverride
{
    /// <summary>
    /// Gets or sets the device ID.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device's mode.
    /// </summary>
    public HdrMode Mode { get; set; }
}

/// <summary>
/// The plugin's saved settings.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the devices that don't use Auto.
    /// </summary>
    public List<DeviceOverride> Devices { get; set; } = [];
}

using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Extended;

/// <summary>
/// The plugin's entry point, which also stores each device's HDR setting.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// The plugin's ID, which must match the manifest.
    /// </summary>
    public static readonly Guid PluginId = Guid.Parse("6f0c2b7e-4d1a-4c86-9a53-2e7b8d1f3c40");

    private readonly object _configLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="xmlSerializer">The XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the running plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Jellyfin Extended";

    /// <inheritdoc />
    public override Guid Id => PluginId;

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
        => [new PluginPageInfo { Name = Name, EmbeddedResourcePath = typeof(Plugin).Namespace + ".Web.devices.html" }];

    /// <summary>
    /// Gets a device's setting. Devices with no saved setting use Auto.
    /// </summary>
    /// <param name="deviceId">The device ID.</param>
    /// <returns>The device's mode.</returns>
    public HdrMode GetMode(string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return HdrMode.Auto;
        }

        lock (_configLock)
        {
            return Configuration.Devices.FirstOrDefault(d => d.DeviceId == deviceId)?.Mode ?? HdrMode.Auto;
        }
    }

    /// <summary>
    /// Saves a device's setting. Auto removes the entry, as it is the default.
    /// </summary>
    /// <param name="deviceId">The device ID.</param>
    /// <param name="mode">The mode to save.</param>
    public void SetMode(string deviceId, HdrMode mode)
    {
        lock (_configLock)
        {
            Configuration.Devices.RemoveAll(d => d.DeviceId == deviceId);
            if (mode != HdrMode.Auto)
            {
                Configuration.Devices.Add(new DeviceOverride { DeviceId = deviceId, Mode = mode });
            }

            SaveConfiguration();
        }
    }
}

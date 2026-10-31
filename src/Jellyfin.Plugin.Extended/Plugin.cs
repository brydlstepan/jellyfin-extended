using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Extended;

/// <summary>
/// The plugin's entry point.
/// </summary>
public class Plugin : BasePlugin<BasePluginConfiguration>
{
    /// <summary>
    /// The plugin's ID, which must match the manifest.
    /// </summary>
    public static readonly Guid PluginId = Guid.Parse("6f0c2b7e-4d1a-4c86-9a53-2e7b8d1f3c40");

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">The application paths.</param>
    /// <param name="xmlSerializer">The XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
    }

    /// <inheritdoc />
    public override string Name => "Jellyfin Extended";

    /// <inheritdoc />
    public override Guid Id => PluginId;
}

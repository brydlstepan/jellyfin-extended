using System;
using System.IO;
using System.Text.Json.Nodes;
using Xunit;

namespace Jellyfin.Plugin.Extended.Tests;

public class ManifestTests
{
    private static JsonNode Manifest() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "meta.json")))!;

    [Fact]
    public void Manifest_HasThePluginsId()
    {
        Assert.Equal(Plugin.PluginId, Guid.Parse((string)Manifest()["guid"]!));
    }

    [Fact]
    public void Manifest_DescribesTheBuiltAssemblyForJellyfin12()
    {
        var manifest = Manifest();
        var assembly = typeof(Plugin).Assembly.GetName();

        Assert.Equal(assembly.Version, Version.Parse((string)manifest["version"]!));
        Assert.Equal("12.0.0.0", (string?)manifest["targetAbi"]);
        Assert.Equal(assembly.Name + ".dll", (string?)manifest["assemblies"]![0]);
    }
}

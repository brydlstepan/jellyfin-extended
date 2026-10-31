using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Extended.Tests;

public class AdminDevicesTests
{
    private readonly Plugin _plugin = new(
        Stub<IApplicationPaths>.Create((method, _) => method.ReturnType == typeof(string) ? Path.GetTempPath() : null),
        Stub<IXmlSerializer>.Create((method, _) => method.Name == nameof(IXmlSerializer.DeserializeFromFile) ? new PluginConfiguration() : null));

    private static AdminDevicesController Controller(params DeviceInfoDto[] devices)
        => new(Stub<IDeviceManager>.Create((method, args) => method.Name switch
        {
            nameof(IDeviceManager.GetDevicesForUser) => new QueryResult<DeviceInfoDto>(devices),
            nameof(IDeviceManager.GetDevice) => devices.FirstOrDefault(d => d.Id == (string?)args![0]),
            _ => null
        }));

    private static DeviceInfoDto Device(string id, string name = "Phone", string? customName = null, string lastUser = "owner")
        => new() { Id = id, Name = name, CustomName = customName, AppName = "Jellyfin Android", LastUserName = lastUser, AccessToken = "secret-token" };

    [Fact]
    public void List_ShowsADeviceWithNoSavedSettingAsAuto()
    {
        var device = Assert.Single(Controller(Device("phone")).GetDevices().Value!);

        Assert.Equal("phone", device.Id);
        Assert.Equal("Phone", device.Name);
        Assert.Equal("Jellyfin Android", device.AppName);
        Assert.Equal("owner", device.LastUserName);
        Assert.Equal(HdrMode.Auto, device.Mode);
    }

    [Fact]
    public void List_ShowsEachDevicesSavedSetting()
    {
        _plugin.SetMode("phone", HdrMode.Off);
        _plugin.SetMode("tv", HdrMode.On);

        var devices = Controller(Device("phone"), Device("tv"), Device("tablet")).GetDevices().Value!;

        Assert.Equal(
            ["phone:Off", "tv:On", "tablet:Auto"],
            devices.Select(d => $"{d.Id}:{d.Mode}"));
    }

    [Fact]
    public void List_ShowsADeviceOnceWhenSeveralUsersSignedInOnIt()
    {
        var devices = Controller(Device("phone", lastUser: "latest"), Device("phone", lastUser: "earlier")).GetDevices().Value!;

        Assert.Equal("latest", Assert.Single(devices).LastUserName);
    }

    [Fact]
    public void List_PrefersTheNameAnAdminGaveTheDevice()
    {
        var device = Assert.Single(Controller(Device("phone", customName: "Bedroom phone")).GetDevices().Value!);

        Assert.Equal("Bedroom phone", device.Name);
    }

    [Fact]
    public void List_NeverExposesAccessTokens()
    {
        Assert.DoesNotContain(typeof(AdminDevice).GetProperties(), p => p.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(HdrMode.Off)]
    [InlineData(HdrMode.On)]
    public void Set_SavesTheModeForTheChosenDevice(HdrMode mode)
    {
        var result = Controller(Device("phone"), Device("tv")).SetDevice("phone", mode);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(mode, _plugin.GetMode("phone"));
        Assert.Equal(HdrMode.Auto, _plugin.GetMode("tv"));
    }

    [Fact]
    public void Set_AutoClearsTheSavedEntry()
    {
        _plugin.SetMode("phone", HdrMode.Off);

        Controller(Device("phone")).SetDevice("phone", HdrMode.Auto);

        Assert.Empty(_plugin.Configuration.Devices);
    }

    [Fact]
    public void Set_RefusesADeviceTheServerDoesNotKnow()
    {
        var result = Controller(Device("phone")).SetDevice("made-up", HdrMode.Off);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(_plugin.Configuration.Devices);
    }

    [Theory]
    [InlineData(nameof(AdminDevicesController.GetDevices))]
    [InlineData(nameof(AdminDevicesController.SetDevice))]
    public void Endpoints_AreAdminOnly(string action)
    {
        var authorize = typeof(AdminDevicesController).GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>();

        // The name Jellyfin.Api's own admin endpoints use, read from Jellyfin.Api.dll in the 12.4 image.
        Assert.Equal("RequiresElevation", authorize?.Policy);
    }

    [Fact]
    public void Page_IsRegisteredAndItsResourceIsEmbedded()
    {
        var page = Assert.Single(_plugin.GetPages());

        Assert.NotNull(typeof(Plugin).Assembly.GetManifestResourceStream(page.EmbeddedResourcePath));
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Devices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Jellyfin.Plugin.Extended;

/// <summary>
/// A device as the admin page shows it. It leaves out the device's access token.
/// </summary>
public class AdminDevice
{
    /// <summary>
    /// Gets or sets the device ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device's name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the app the device last used.
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>
    /// Gets or sets the user who last used the device.
    /// </summary>
    public string? LastUserName { get; set; }

    /// <summary>
    /// Gets or sets when the device was last used.
    /// </summary>
    public DateTime? DateLastActivity { get; set; }

    /// <summary>
    /// Gets or sets the device's mode.
    /// </summary>
    public HdrMode Mode { get; set; }
}

/// <summary>
/// Lets an admin read and change the setting of any device.
/// </summary>
[ApiController]
[Route("Extended/Hdr/Devices")]
public class AdminDevicesController : ControllerBase
{
    // The admin-only policy Jellyfin.Api registers. Its constant isn't in a package a plugin can reference.
    private const string AdminOnly = "RequiresElevation";

    private readonly IDeviceManager _deviceManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminDevicesController"/> class.
    /// </summary>
    /// <param name="deviceManager">The device manager.</param>
    public AdminDevicesController(IDeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
    }

    /// <summary>
    /// Gets every device the server knows, with its setting.
    /// </summary>
    /// <returns>The devices, most recently used first.</returns>
    [HttpGet]
    [Authorize(Policy = AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<IReadOnlyList<AdminDevice>> GetDevices()
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return BadRequest();
        }

        // The server has one entry per user signed in on a device, most recent first, but the setting is per device.
        return _deviceManager.GetDevicesForUser(null).Items
            .Where(d => !string.IsNullOrEmpty(d.Id))
            .DistinctBy(d => d.Id)
            .Select(d => new AdminDevice
            {
                Id = d.Id!,
                Name = string.IsNullOrEmpty(d.CustomName) ? d.Name : d.CustomName,
                AppName = d.AppName,
                LastUserName = d.LastUserName,
                DateLastActivity = d.DateLastActivity,
                Mode = plugin.GetMode(d.Id)
            })
            .ToList();
    }

    /// <summary>
    /// Changes a device's setting.
    /// </summary>
    /// <param name="deviceId">The device ID.</param>
    /// <param name="mode">The new mode.</param>
    /// <returns>No content, or not found if the server doesn't know the device.</returns>
    [HttpPost]
    [Authorize(Policy = AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult SetDevice([FromQuery, BindRequired] string deviceId, [FromQuery, BindRequired] HdrMode mode)
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return BadRequest();
        }

        if (_deviceManager.GetDevice(deviceId) is null)
        {
            return NotFound();
        }

        plugin.SetMode(deviceId, mode);
        return NoContent();
    }
}

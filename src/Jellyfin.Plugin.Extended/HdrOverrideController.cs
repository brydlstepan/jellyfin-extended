using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Queries;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Jellyfin.Plugin.Extended;

/// <summary>
/// Lets a signed-in device read and change its own setting.
/// </summary>
[ApiController]
[Route("Extended/Hdr")]
public class HdrOverrideController : ControllerBase
{
    private readonly IAuthorizationContext _authorizationContext;
    private readonly IDeviceManager _deviceManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="HdrOverrideController"/> class.
    /// </summary>
    /// <param name="authorizationContext">The authorization context.</param>
    /// <param name="deviceManager">The device manager.</param>
    public HdrOverrideController(IAuthorizationContext authorizationContext, IDeviceManager deviceManager)
    {
        _authorizationContext = authorizationContext;
        _deviceManager = deviceManager;
    }

    /// <summary>
    /// Gets the calling device's setting.
    /// </summary>
    /// <returns>The device's mode.</returns>
    [HttpGet("Device")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeviceOverride>> GetDevice()
    {
        var plugin = Plugin.Instance;
        var deviceId = await GetCallingDeviceId().ConfigureAwait(false);
        if (plugin is null || string.IsNullOrEmpty(deviceId))
        {
            return BadRequest();
        }

        return new DeviceOverride { DeviceId = deviceId, Mode = plugin.GetMode(deviceId) };
    }

    /// <summary>
    /// Changes the calling device's setting.
    /// </summary>
    /// <param name="mode">The new mode.</param>
    /// <returns>No content.</returns>
    [HttpPost("Device")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SetDevice([FromQuery, BindRequired] HdrMode mode)
    {
        var plugin = Plugin.Instance;
        var deviceId = await GetCallingDeviceId().ConfigureAwait(false);
        if (plugin is null || string.IsNullOrEmpty(deviceId))
        {
            return BadRequest();
        }

        plugin.SetMode(deviceId, mode);
        return NoContent();
    }

    // The request's own DeviceId can claim any device, so use the one its token was issued to.
    private async Task<string?> GetCallingDeviceId()
    {
        var token = (await _authorizationContext.GetAuthorizationInfo(Request).ConfigureAwait(false)).Token;
        return _deviceManager.GetDevices(new DeviceQuery { AccessToken = token }).Items.FirstOrDefault()?.DeviceId;
    }
}

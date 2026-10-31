using System.Threading.Tasks;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Extended;

/// <summary>
/// Lets a signed-in device read and change its own setting.
/// </summary>
[ApiController]
[Route("Extended/Hdr")]
public class HdrOverrideController : ControllerBase
{
    private readonly IAuthorizationContext _authorizationContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="HdrOverrideController"/> class.
    /// </summary>
    /// <param name="authorizationContext">The authorization context.</param>
    public HdrOverrideController(IAuthorizationContext authorizationContext)
    {
        _authorizationContext = authorizationContext;
    }

    /// <summary>
    /// Gets the calling device's setting.
    /// </summary>
    /// <returns>The device's mode.</returns>
    [HttpGet("Device")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceOverride>> GetDevice()
    {
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return NotFound();
        }

        var deviceId = (await _authorizationContext.GetAuthorizationInfo(Request).ConfigureAwait(false)).DeviceId;
        return new DeviceOverride { DeviceId = deviceId ?? string.Empty, Mode = plugin.GetMode(deviceId) };
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
    public async Task<ActionResult> SetDevice([FromQuery] HdrMode mode)
    {
        var plugin = Plugin.Instance;
        var deviceId = (await _authorizationContext.GetAuthorizationInfo(Request).ConfigureAwait(false)).DeviceId;
        if (plugin is null || string.IsNullOrEmpty(deviceId))
        {
            return BadRequest();
        }

        plugin.SetMode(deviceId, mode);
        return NoContent();
    }
}

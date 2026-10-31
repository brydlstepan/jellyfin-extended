using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Extended;

/// <summary>
/// Limits PlaybackInfo requests to SDR for the devices set to HDR off, and for browsers on Auto that report an SDR screen.
/// Anything unexpected passes through untouched: unchanged HDR is better than failed playback.
/// </summary>
public sealed class PlaybackInfoMiddleware : IStartupFilter
{
    private const int BufferThresholdBytes = 64 * 1024;
    private const long MaxBodyBytes = 4 * 1024 * 1024;

    private static readonly Regex PlaybackInfoPath = new(
        @"/Items/[0-9a-fA-F-]{32,36}/PlaybackInfo/?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly ILogger<PlaybackInfoMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackInfoMiddleware"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PlaybackInfoMiddleware(ILogger<PlaybackInfoMiddleware> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.Use(InvokeAsync);
            next(app);
        };

    private async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        if (HttpMethods.IsPost(context.Request.Method)
            && PlaybackInfoPath.IsMatch(context.Request.Path.Value ?? string.Empty))
        {
            try
            {
                await ApplyAsync(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HDR override skipped for {Path}", context.Request.Path);
                if (context.Request.Body.CanSeek)
                {
                    context.Request.Body.Position = 0;
                }
            }
        }

        await next().ConfigureAwait(false);
    }

    private async Task ApplyAsync(HttpContext context)
    {
        var request = context.Request;
        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return;
        }

        var auth = await context.RequestServices.GetRequiredService<IAuthorizationContext>()
            .GetAuthorizationInfo(request)
            .ConfigureAwait(false);
        if (!ScreenReport.LimitsToSdr(plugin.GetMode(auth.DeviceId), request.Headers[ScreenReport.HeaderName]))
        {
            return;
        }

        if (request.ContentLength > MaxBodyBytes)
        {
            _logger.LogWarning("HDR override skipped for {Path}: the body is too large", request.Path);
            return;
        }

        // shortcut: a body with no length that passes the limit fails the request instead of passing through, handle it if a real client sends one that large.
        request.EnableBuffering(BufferThresholdBytes, MaxBodyBytes);
        string json;
        using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
        {
            json = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
        }

        request.Body.Position = 0;
        if (json.Length == 0)
        {
            return;
        }

        var rewritten = SdrProfile.Rewrite(json);
        if (rewritten is null)
        {
            _logger.LogDebug("No device profile to limit for device {DeviceId}", auth.DeviceId);
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(rewritten);
        request.Body = new MemoryStream(bytes);
        request.ContentLength = bytes.Length;
        _logger.LogInformation("Limited playback to SDR for device {DeviceId}", auth.DeviceId);
    }
}

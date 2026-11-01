using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Plugin.Extended;

/// <summary>
/// Adds the screen reporting and toggle script to the web client's page.
/// Anything unexpected passes through untouched: an unreported screen is better than a broken page.
/// </summary>
public sealed class WebClientPageMiddleware : IStartupFilter
{
    private const string HeadEnd = "</head>";

    private static readonly Regex PagePath = new(
        @"/web/(index\.html)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Lazy<string> Script = new(ReadScript);

    /// <inheritdoc />
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.Use(InvokeAsync);
            next(app);
        };

    private static async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        if (!HttpMethods.IsGet(context.Request.Method)
            || !PagePath.IsMatch(context.Request.Path.Value ?? string.Empty))
        {
            await next().ConfigureAwait(false);
            return;
        }

        // Compressed bytes can't be edited, and a 304 or a partial response has no page to edit.
        // shortcut: the page goes out uncompressed, fine while it is this small.
        var requestHeaders = context.Request.Headers;
        requestHeaders.Remove(HeaderNames.AcceptEncoding);
        requestHeaders.Remove(HeaderNames.IfNoneMatch);
        requestHeaders.Remove(HeaderNames.IfModifiedSince);
        requestHeaders.Remove(HeaderNames.Range);
        requestHeaders.Remove(HeaderNames.IfRange);

        var response = context.Response;
        var original = response.Body;
        using var buffer = new MemoryStream();
        response.Body = buffer;
        try
        {
            await next().ConfigureAwait(false);
        }
        finally
        {
            response.Body = original;
        }

        var page = IsPlainPage(response) ? WithScript(buffer) : null;
        if (page is null)
        {
            buffer.Position = 0;
            await buffer.CopyToAsync(original, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        // The file's validators describe the page without the script.
        response.Headers.Remove(HeaderNames.ETag);
        response.Headers.Remove(HeaderNames.LastModified);
        response.Headers.Remove(HeaderNames.AcceptRanges);
        response.Headers.CacheControl = "no-cache";
        response.ContentLength = page.Length;
        await original.WriteAsync(page, context.RequestAborted).ConfigureAwait(false);
    }

    private static bool IsPlainPage(HttpResponse response)
        => response.StatusCode == StatusCodes.Status200OK
            && StringValues.IsNullOrEmpty(response.Headers.ContentEncoding)
            && MediaTypeHeaderValue.TryParse(response.ContentType, out var type)
            && type.MediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase);

    private static byte[]? WithScript(MemoryStream buffer)
    {
        var html = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        var at = html.IndexOf(HeadEnd, StringComparison.OrdinalIgnoreCase);
        return at < 0 ? null : Encoding.UTF8.GetBytes(html.Insert(at, $"<script>{Script.Value}</script>"));
    }

    private static string ReadScript()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Jellyfin.Plugin.Extended.screen-report.js")
            ?? throw new InvalidOperationException("The screen reporting script is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

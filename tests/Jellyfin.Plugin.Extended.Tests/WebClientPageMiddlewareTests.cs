using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.Extended.Tests;

public class WebClientPageMiddlewareTests
{
    private const string Page = "<!doctype html><html><head><title>Jellyfin</title></head><body></body></html>";
    private const string Marker = "window.JellyfinExtended";

    [Theory]
    [InlineData("/web/")]
    [InlineData("/web/index.html")]
    [InlineData("/jellyfin/web/index.html")]
    public async Task AddsTheScriptToThePageBeforeTheHeadEnds(string path)
    {
        var context = await Run(path, Respond(Page));

        var body = Body(context);
        Assert.Contains(Marker, body, StringComparison.Ordinal);
        Assert.True(body.IndexOf(Marker, StringComparison.Ordinal) < body.IndexOf("</head>", StringComparison.Ordinal));
        Assert.Equal(Encoding.UTF8.GetByteCount(body), context.Response.ContentLength);
    }

    [Fact]
    public async Task ThePageCarriesThePlaybackSettingsToggle()
    {
        var body = Body(await Run("/web/", Respond(Page)));

        Assert.Contains("mypreferencesplayback", body, StringComparison.Ordinal);
        Assert.Contains("Extended/Hdr/Device'", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Extended/Hdr/Devices", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AsksTheServerForAPlainFullPage()
    {
        // Compressed bytes can't be edited, and a 304 or a partial response has no page to edit.
        var seen = new HeaderDictionary();
        await Run(
            "/web/",
            inner =>
            {
                foreach (var header in inner.Request.Headers)
                {
                    seen[header.Key] = header.Value;
                }

                return Respond(Page)(inner);
            },
            request =>
            {
                request.Headers.AcceptEncoding = "gzip, deflate, br";
                request.Headers.IfNoneMatch = "\"abc\"";
                request.Headers.IfModifiedSince = "Sat, 31 Oct 2026 00:00:00 GMT";
                request.Headers.Range = "bytes=0-10";
                request.Headers.IfRange = "\"abc\"";
            });

        Assert.Empty(seen);
    }

    [Fact]
    public async Task DropsValidatorsOfTheUneditedFile()
    {
        var context = await Run("/web/", inner =>
        {
            inner.Response.Headers.ETag = "\"abc\"";
            inner.Response.Headers.LastModified = "Sat, 31 Oct 2026 00:00:00 GMT";
            inner.Response.Headers.AcceptRanges = "bytes";
            return Respond(Page)(inner);
        });

        Assert.False(context.Response.Headers.ContainsKey("ETag"));
        Assert.False(context.Response.Headers.ContainsKey("Last-Modified"));
        Assert.False(context.Response.Headers.ContainsKey("Accept-Ranges"));
        Assert.Equal("no-cache", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task LeavesACompressedResponseUntouched()
    {
        var context = await Run("/web/", inner =>
        {
            inner.Response.Headers.ContentEncoding = "gzip";
            return Respond(Page)(inner);
        });

        Assert.Equal(Page, Body(context));
    }

    [Fact]
    public async Task LeavesAPageWithNoHeadUntouched()
    {
        var context = await Run("/web/", Respond("<html><body></body></html>"));

        Assert.Equal("<html><body></body></html>", Body(context));
    }

    [Fact]
    public async Task LeavesFailedResponsesUntouched()
    {
        var context = await Run("/web/", inner =>
        {
            inner.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Respond(Page)(inner);
        });

        Assert.Equal(Page, Body(context));
    }

    [Fact]
    public async Task LeavesOtherContentTypesUntouched()
    {
        var context = await Run("/web/", inner =>
        {
            var response = Respond(Page)(inner);
            inner.Response.ContentType = "text/plain";
            return response;
        });

        Assert.Equal(Page, Body(context));
    }

    [Theory]
    [InlineData("/web/main.jellyfin.bundle.js")]
    [InlineData("/web/index.html.bak")]
    [InlineData("/System/Info/Public")]
    public async Task LeavesOtherPathsUntouched(string path)
    {
        var context = await Run(path, Respond(Page), request => request.Headers.AcceptEncoding = "gzip");

        Assert.Equal(Page, Body(context));
        Assert.Equal("gzip", context.Request.Headers.AcceptEncoding);
    }

    [Fact]
    public async Task LeavesOtherMethodsUntouched()
    {
        var context = await Run("/web/", Respond(Page), request => request.Method = HttpMethods.Post);

        Assert.Equal(Page, Body(context));
    }

    [Fact]
    public async Task AnInnerFailureStillRestoresTheResponseStream()
    {
        var original = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/web/";
        context.Response.Body = original;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Pipeline(_ => throw new InvalidOperationException())(context));

        Assert.Same(original, context.Response.Body);
    }

    private static RequestDelegate Respond(string html)
        => async context =>
        {
            var bytes = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength = bytes.Length;
            await context.Response.Body.WriteAsync(bytes);
        };

    private static RequestDelegate Pipeline(RequestDelegate inner)
    {
        var app = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        new WebClientPageMiddleware().Configure(next => next.Run(inner))(app);
        return app.Build();
    }

    private static async Task<DefaultHttpContext> Run(string path, RequestDelegate inner, Action<HttpRequest>? setUp = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        setUp?.Invoke(context.Request);
        await Pipeline(inner)(context);
        return context;
    }

    private static string Body(HttpContext context)
        => Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
}

using System.Net;
using System.Net.Http.Headers;
using BookmarkFeeder.WebApi.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookmarkFeeder.WebApi.Tests.Services;

/// <summary>
/// Favicon discovery. No real HTTP: a stub handler answers by URL and records every request,
/// so the origin-only guarantee can be asserted rather than assumed.
/// </summary>
public class FaviconResolverTests
{
    private const string PageUrl = "https://site.example/blog/post";

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(responder(request));
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static HttpResponseMessage Html(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "text/html") };

    private static HttpResponseMessage Image(string contentType = "image/png")
    {
        var content = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound);

    private static (FaviconResolver Resolver, StubHandler Handler) Build(
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHandler(responder);
        return (new FaviconResolver(new StubFactory(handler), NullLogger<FaviconResolver>.Instance), handler);
    }

    [Fact]
    public async Task Resolves_LinkRelIcon_ToAnAbsoluteUrl()
    {
        var (resolver, _) = Build(request =>
            request.RequestUri!.AbsolutePath == "/assets/icon.png"
                ? Image()
                : Html("""<html><head><link rel="icon" href="/assets/icon.png"></head></html>"""));

        var result = await resolver.ResolveAsync(PageUrl);

        result.Should().Be("https://site.example/assets/icon.png");
    }

    [Fact]
    public async Task Resolves_AHrefRelativeToThePage()
    {
        var (resolver, _) = Build(request =>
            request.RequestUri!.AbsolutePath.EndsWith("icon.png")
                ? Image()
                : Html("""<html><head><link rel="icon" href="icon.png"></head></html>"""));

        var result = await resolver.ResolveAsync(PageUrl);

        // Relative to /blog/post, not to the root.
        result.Should().Be("https://site.example/blog/icon.png");
    }

    [Fact]
    public async Task Resolves_ShortcutIcon_AndAppleTouchIcon()
    {
        var (resolver, _) = Build(request =>
            request.RequestUri!.AbsolutePath == "/touch.png"
                ? Image()
                : Html("""<html><head><link rel="apple-touch-icon" href="/touch.png"></head></html>"""));

        (await resolver.ResolveAsync(PageUrl)).Should().Be("https://site.example/touch.png");

        var (shortcut, _) = Build(request =>
            request.RequestUri!.AbsolutePath == "/short.ico"
                ? Image("image/x-icon")
                : Html("""<html><head><link rel="shortcut icon" href="/short.ico"></head></html>"""));

        (await shortcut.ResolveAsync(PageUrl)).Should().Be("https://site.example/short.ico");
    }

    [Fact]
    public async Task PrefersTheLargestDeclaredSize()
    {
        var (resolver, _) = Build(request =>
            request.RequestUri!.AbsolutePath.StartsWith("/big") || request.RequestUri.AbsolutePath.StartsWith("/small")
                ? Image()
                : Html("""
                    <html><head>
                      <link rel="icon" href="/small.png" sizes="16x16">
                      <link rel="icon" href="/big.png" sizes="180x180">
                    </head></html>
                    """));

        var result = await resolver.ResolveAsync(PageUrl);

        result.Should().Be("https://site.example/big.png");
    }

    [Fact]
    public async Task FallsBackToFaviconIco_WhenThePageDeclaresNoIcon()
    {
        var (resolver, handler) = Build(request =>
            request.RequestUri!.AbsolutePath == "/favicon.ico"
                ? Image("image/vnd.microsoft.icon")
                : Html("<html><head><title>No icon here</title></head></html>"));

        var result = await resolver.ResolveAsync(PageUrl);

        result.Should().Be("https://site.example/favicon.ico");
        // The fallback is the origin root, not relative to the page path.
        handler.Requests.Should().Contain(u => u.AbsolutePath == "/favicon.ico");
    }

    [Fact]
    public async Task NeverRequestsAThirdPartyOrigin_EvenWhenThePageDeclaresOne()
    {
        // A cross-origin icon can't be fetched without telling another host what we bookmarked,
        // so it is ignored and discovery falls back to the site's own /favicon.ico.
        var (resolver, handler) = Build(request =>
            request.RequestUri!.AbsolutePath == "/favicon.ico"
                ? Image()
                : Html("""<html><head><link rel="icon" href="https://cdn.other-host.example/i.png"></head></html>"""));

        var result = await resolver.ResolveAsync(PageUrl);

        result.Should().Be("https://site.example/favicon.ico");
        handler.Requests.Should().OnlyContain(u => u.Host == "site.example");
    }

    [Fact]
    public async Task ReturnsNull_WhenNothingIsDiscoverable()
    {
        var (resolver, _) = Build(request =>
            request.RequestUri!.AbsolutePath == "/favicon.ico"
                ? NotFound()
                : Html("<html><head></head></html>"));

        (await resolver.ResolveAsync(PageUrl)).Should().BeNull();
    }

    [Fact]
    public async Task ReturnsNull_WhenTheCandidateIsNotAnImage()
    {
        // Plenty of sites answer /favicon.ico with an HTML error page and a 200.
        var (resolver, _) = Build(_ => Html("<html><body>404-ish</body></html>"));

        (await resolver.ResolveAsync(PageUrl)).Should().BeNull();
    }

    [Fact]
    public async Task ReturnsNull_AndRequestsNothing_ForANonHttpUrl()
    {
        var (resolver, handler) = Build(_ => Image());

        // Real bookmark collections contain these.
        (await resolver.ResolveAsync("javascript:void(0)")).Should().BeNull();
        (await resolver.ResolveAsync("chrome://extensions")).Should().BeNull();
        (await resolver.ResolveAsync("not a url at all")).Should().BeNull();

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ReturnsNull_WhenTheSiteFails_RatherThanThrowing()
    {
        // A dead host must not surface as an exception: the caller is a background worker.
        var (resolver, _) = Build(_ => throw new HttpRequestException("no such host"));

        (await resolver.ResolveAsync(PageUrl)).Should().BeNull();
    }

    [Fact]
    public async Task ReturnsNull_WhenCancelled_RatherThanThrowing()
    {
        var (resolver, _) = Build(_ => throw new TaskCanceledException("timed out"));

        (await resolver.ResolveAsync(PageUrl)).Should().BeNull();
    }
}

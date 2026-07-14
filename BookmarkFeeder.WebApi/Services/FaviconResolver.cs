using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;

namespace BookmarkFeeder.WebApi.Services;

public interface IFaviconResolver
{
    /// <summary>
    /// Discovers the site's favicon from its own origin, or null when nothing usable is found.
    /// Never throws: the caller is a background worker, and a dead site is normal.
    /// </summary>
    Task<string?> ResolveAsync(string bookmarkUrl, CancellationToken ct = default);
}

public class FaviconResolver(IHttpClientFactory httpClientFactory, ILogger<FaviconResolver> logger)
    : IFaviconResolver
{
    /// <summary>Name of the configured HttpClient (short timeout, bounded buffer, browser UA).</summary>
    public const string HttpClientName = "favicon";

    public async Task<string?> ResolveAsync(string bookmarkUrl, CancellationToken ct = default)
    {
        // Bookmark collections contain javascript: bookmarklets, chrome:// pages and junk.
        if (!Uri.TryCreate(bookmarkUrl, UriKind.Absolute, out var pageUri) ||
            (pageUri.Scheme != Uri.UriSchemeHttp && pageUri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient(HttpClientName);

            var declared = await DiscoverFromPageAsync(client, pageUri, ct);
            if (declared is not null) return declared.ToString();

            // Every site is entitled to an implicit /favicon.ico at its origin root.
            var fallback = new Uri(pageUri, "/favicon.ico");
            return await IsImageAsync(client, fallback, ct) ? fallback.ToString() : null;
        }
        catch (Exception ex)
        {
            // A missing favicon is not a problem worth surfacing — the UI falls back to a monogram.
            logger.LogDebug(ex, "Favicon discovery failed for {Url}", bookmarkUrl);
            return null;
        }
    }

    private async Task<Uri?> DiscoverFromPageAsync(HttpClient client, Uri pageUri, CancellationToken ct)
    {
        using var response = await client.GetAsync(pageUri, ct);
        if (!response.IsSuccessStatusCode) return null;

        var html = await response.Content.ReadAsStringAsync(ct);
        var document = await new HtmlParser().ParseDocumentAsync(html, ct);

        // Ordered best-first; the first one that is same-origin and really an image wins.
        var candidates = document
            .QuerySelectorAll("link[rel~='icon'], link[rel='shortcut icon'], link[rel='apple-touch-icon']")
            .Select(link => new
            {
                Href = link.GetAttribute("href"),
                Size = LargestSize(link.GetAttribute("sizes")),
            })
            .Where(c => !string.IsNullOrWhiteSpace(c.Href))
            .OrderByDescending(c => c.Size)
            .ToList();

        foreach (var candidate in candidates)
        {
            // The page's own URL is the base, so "icon.png" resolves against its directory.
            if (!Uri.TryCreate(pageUri, candidate.Href, out var absolute)) continue;

            // Origin-only: fetching a third-party host would tell it what was bookmarked, which
            // is exactly what a self-hosted collection exists to avoid. A cross-origin icon is
            // skipped rather than trusted, and discovery falls through to /favicon.ico.
            if (!IsSameOrigin(absolute, pageUri)) continue;

            if (await IsImageAsync(client, absolute, ct)) return absolute;
        }

        return null;
    }

    private static bool IsSameOrigin(Uri candidate, Uri page) =>
        candidate.Scheme == page.Scheme &&
        string.Equals(candidate.Host, page.Host, StringComparison.OrdinalIgnoreCase) &&
        candidate.Port == page.Port;

    /// <summary>Largest edge from a sizes attribute ("16x16 32x32"), or 0 when unspecified.</summary>
    private static int LargestSize(string? sizes)
    {
        if (string.IsNullOrWhiteSpace(sizes)) return 0;

        return sizes
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => int.TryParse(token.Split('x', 'X')[0], out var edge) ? edge : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private async Task<bool> IsImageAsync(HttpClient client, Uri uri, CancellationToken ct)
    {
        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            // Plenty of sites answer /favicon.ico with a 200 and an HTML error page.
            return response.IsSuccessStatusCode &&
                   response.Content.Headers.ContentType?.MediaType?
                       .StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Favicon candidate {Url} could not be validated", uri);
            return false;
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using BookmarkFeeder.WebApi.Dtos;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

public class RateLimitTests
{
    // A factory whose "writes" policy allows only 2 requests/window so the 3rd is rejected.
    private static WebApplicationFactory<Program> LowWriteLimitFactory(BookmarkApiFactory factory) =>
        factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:Writes", "2"));

    private static HttpClient AuthClient(WebApplicationFactory<Program> f)
    {
        var client = f.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", BookmarkApiFactory.TestApiKey);
        return client;
    }

    [Fact]
    public async Task ExceedingWriteLimit_Returns429_WithRetryAfter()
    {
        using var factory = new BookmarkApiFactory();
        using var limited = LowWriteLimitFactory(factory);
        var client = AuthClient(limited);

        CreateTagRequest tag() => new($"rl-{Guid.NewGuid():N}", null);

        (await client.PostAsJsonAsync("/api/tags", tag())).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsJsonAsync("/api/tags", tag())).StatusCode.Should().Be(HttpStatusCode.Created);
        var rejected = await client.PostAsJsonAsync("/api/tags", tag());

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.Contains("Retry-After").Should().BeTrue();
    }

    [Fact]
    public async Task Reads_AreNotThrottled_WhenWritesAreCapped()
    {
        using var factory = new BookmarkApiFactory();
        using var limited = LowWriteLimitFactory(factory);
        var client = AuthClient(limited);

        // Reads use a separate, generous policy — many succeed even though writes are capped at 2.
        for (var i = 0; i < 6; i++)
        {
            (await client.GetAsync("/api/tags")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FluentAssertions;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

public class ProductionHardeningTests(BookmarkApiFactory factory) : IClassFixture<BookmarkApiFactory>
{
    [Fact]
    public async Task Health_And_Alive_AreReachable_WithoutApiKey()
    {
        var client = factory.CreateClient();

        var health = await client.GetAsync("/health");
        var alive = await client.GetAsync("/alive");

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        alive.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Api_StillRequiresApiKey_AfterPipelineChanges()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tags");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void ForwardedHeaders_AreConfigured_ForXForwardedForAndProto()
    {
        var options = factory.Services
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;

        options.ForwardedHeaders.Should().HaveFlag(ForwardedHeaders.XForwardedFor);
        options.ForwardedHeaders.Should().HaveFlag(ForwardedHeaders.XForwardedProto);
        // Cleared so forwarded headers from the (internal) gateway are honoured.
        options.KnownProxies.Should().BeEmpty();
        options.KnownNetworks.Should().BeEmpty();
    }

    [Fact]
    public async Task HttpsRedirect_IsOffByDefault()
    {
        // No Https:Redirect config → the request is served, not 3xx-redirected to HTTPS.
        var client = factory.CreateAuthenticatedClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/tags");

        ((int)response.StatusCode).Should().BeLessThan(300);
    }
}

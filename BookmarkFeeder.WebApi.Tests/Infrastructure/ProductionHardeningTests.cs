using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace BookmarkFeeder.WebApi.Tests.Infrastructure;

public class ProductionHardeningTests(BookmarkApiFactory factory) : IClassFixture<BookmarkApiFactory>
{
    [Fact]
    public async Task Health_And_Alive_AreReachable_WithoutCredentials()
    {
        var client = factory.CreateClient();

        var health = await client.GetAsync("/health");
        var alive = await client.GetAsync("/alive");

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        alive.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Api_RequiresCredentials_AfterPipelineChanges()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/tags");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Config_IsPublic_SoTheClientsCanBootstrap()
    {
        // The SPA and the extension need the tenant coordinates BEFORE they can obtain a token,
        // so this one endpoint must sit outside the authorized group.
        var response = await factory.CreateClient().GetAsync("/api/config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("")]
    public async Task MalformedToken_Is401(string token)
    {
        var client = factory.CreateClientWithRawToken(token);

        var response = await client.GetAsync("/api/tags");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenSignedWithTheWrongKey_Is401()
    {
        // A structurally perfect token from an issuer we do not trust. This is the test that would
        // catch signature validation being accidentally disabled.
        var foreign = new SymmetricSecurityKey("a-different-key-that-is-long-enough-000000"u8.ToArray());
        var client = factory.CreateClientWithRawToken(
            TestTokens.Create("auth0|forged", signingKey: foreign));

        var response = await client.GetAsync("/api/tags");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TokenForAnotherAudience_Is401()
    {
        // Auth0 issues tokens for many APIs; one minted for a different audience must not open ours.
        var client = factory.CreateClientWithRawToken(
            TestTokens.Create("auth0|wrong-aud", audience: "https://someone-elses-api"));

        var response = await client.GetAsync("/api/tags");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredToken_Is401()
    {
        // Comfortably past the 30-second ClockSkew the app configures.
        var client = factory.CreateClientWithRawToken(
            TestTokens.Create("auth0|expired", expires: DateTime.UtcNow.AddMinutes(-10)));

        var response = await client.GetAsync("/api/tags");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GarbageApiKey_Is401()
    {
        var response = await factory.CreateKeyClient("bmf_not-a-real-key").GetAsync("/api/tags");

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

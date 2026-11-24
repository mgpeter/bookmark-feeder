using System.Net;
using System.Net.Sockets;
using BookmarkFeeder.WebApi.Services;
using BookmarkFeeder.WebApi.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookmarkFeeder.WebApi.Tests.Services;

/// <summary>
/// SSRF protection for the favicon fetcher. Bookmark URLs are user input, so the worker must
/// never connect to the deployment's own network - checked on the resolved address, per hop.
/// </summary>
public class FaviconNetworkGuardTests(BookmarkApiFactory factory) : IClassFixture<BookmarkApiFactory>
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")] // cloud metadata
    [InlineData("100.100.100.100")] // Tailscale / CGNAT
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fd7a:115c:a1e0::1")] // Tailscale IPv6 (ULA)
    [InlineData("::ffff:192.168.1.1")] // IPv4-mapped
    [InlineData("64:ff9b::a00:1")] // NAT64 of 10.0.0.1
    public void IsPublic_RejectsNonPublicAddresses(string address) =>
        FaviconNetworkGuard.IsPublic(IPAddress.Parse(address)).Should().BeFalse();

    [Theory]
    [InlineData("93.184.215.14")]
    [InlineData("1.1.1.1")]
    [InlineData("172.32.0.1")] // just outside 172.16.0.0/12
    [InlineData("100.128.0.1")] // just outside 100.64.0.0/10
    [InlineData("2606:4700:4700::1111")]
    [InlineData("64:ff9b::101:101")] // NAT64 of 1.1.1.1
    public void IsPublic_AcceptsPublicAddresses(string address) =>
        FaviconNetworkGuard.IsPublic(IPAddress.Parse(address)).Should().BeTrue();

    /// <summary>A listener on loopback stands in for an internal service.</summary>
    private static (TcpListener Listener, int Port) StartLoopbackListener()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return (listener, ((IPEndPoint)listener.LocalEndpoint).Port);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("localhost")] // a name that resolves privately is judged by its address
    public async Task ConfiguredFaviconClient_RefusesToConnectToInternalHosts(string host)
    {
        var (listener, port) = StartLoopbackListener();
        try
        {
            // The client exactly as Program.cs registers it, so the wiring is under test too.
            var client = factory.Services.GetRequiredService<IHttpClientFactory>()
                .CreateClient(FaviconResolver.HttpClientName);

            var act = () => client.GetAsync($"http://{host}:{port}/health");

            (await act.Should().ThrowAsync<HttpRequestException>())
                .Which.ToString().Should().Contain("blocked");
            listener.Pending().Should().BeFalse("no connection may reach the internal service");
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task Resolver_ReturnsNullForAnInternalBookmark_WithoutConnecting()
    {
        var (listener, port) = StartLoopbackListener();
        try
        {
            var resolver = new FaviconResolver(
                factory.Services.GetRequiredService<IHttpClientFactory>(),
                NullLogger<FaviconResolver>.Instance);

            (await resolver.ResolveAsync($"http://127.0.0.1:{port}/")).Should().BeNull();
            listener.Pending().Should().BeFalse();
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task AllowPrivateNetworks_OptIn_PermitsLanTargets()
    {
        var (listener, port) = StartLoopbackListener();
        try
        {
            await using var stream = await FaviconNetworkGuard.ConnectAsync(
                new DnsEndPoint("127.0.0.1", port), allowPrivateNetworks: true, CancellationToken.None);

            stream.Should().NotBeNull();
        }
        finally
        {
            listener.Stop();
        }
    }
}

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ProPresenterObsBridge.Util;

namespace ProPresenterObsBridge.Tests;

public class HostResolverTests
{
    private readonly HostResolver _resolver = new(NullLogger<HostResolver>.Instance);

    [Fact]
    public async Task IPv4Address_ReturnedDirectly()
    {
        var result = await _resolver.ResolveAsync("192.168.1.100");
        Assert.NotNull(result);
        Assert.Equal(IPAddress.Parse("192.168.1.100"), result);
    }

    [Fact]
    public async Task IPv6Address_ReturnedDirectly()
    {
        var result = await _resolver.ResolveAsync("::1");
        Assert.NotNull(result);
        Assert.Equal(IPAddress.IPv6Loopback, result);
    }

    [Fact]
    public async Task Localhost_ResolvesToLoopback()
    {
        var result = await _resolver.ResolveAsync("localhost");
        Assert.NotNull(result);
        // Should be 127.0.0.1 (IPv4 preferred) or ::1
        Assert.True(
            IPAddress.IsLoopback(result),
            $"Expected loopback address, got {result}");
    }

    [Fact]
    public async Task InvalidHost_ReturnsNull()
    {
        var result = await _resolver.ResolveAsync("this-host-definitely-does-not-exist-12345.invalid");
        Assert.Null(result);
    }

    [Fact]
    public async Task CancelledToken_ReturnsNull()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await _resolver.ResolveAsync("localhost", cts.Token);
        Assert.Null(result);
    }
}

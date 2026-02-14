using Microsoft.Extensions.Logging.Abstractions;
using ProPresenterObsBridge.Obs;
using ProPresenterObsBridge.Options;
using ProPresenterObsBridge.Util;

namespace ProPresenterObsBridge.Tests;

public class ObsClientTests : IDisposable
{
    private readonly ObsClient _client;

    public ObsClientTests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ObsOptions
        {
            Host = "127.0.0.1",
            Port = 4455,
            Password = null,
            ReconnectMaxSeconds = 5
        });
        var resolver = new HostResolver(NullLogger<HostResolver>.Instance);
        _client = new ObsClient(NullLogger<ObsClient>.Instance, options, resolver);
    }

    [Fact]
    public void IsConnected_InitiallyFalse()
    {
        Assert.False(_client.IsConnected);
    }

    [Fact]
    public async Task SetProgramScene_WhenDisconnected_ReturnsFalse()
    {
        var result = await _client.SetProgramSceneAsync("Cam 1", CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task GetCurrentProgramScene_WhenDisconnected_ReturnsNull()
    {
        var result = await _client.GetCurrentProgramSceneAsync(CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task ConnectLoop_CancellationExitsGracefully()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        // Should exit without throwing (host won't resolve to anything reachable)
        await _client.ConnectLoopAsync(cts.Token);
        Assert.False(_client.IsConnected);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}

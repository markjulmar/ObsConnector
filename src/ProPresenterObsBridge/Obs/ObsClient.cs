using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using ProPresenterObsBridge.Options;
using ProPresenterObsBridge.Util;

namespace ProPresenterObsBridge.Obs;

public sealed class ObsClient : IObsClient, IDisposable
{
    private readonly ILogger<ObsClient> _logger;
    private readonly IOptionsMonitor<ObsOptions> _optionsMonitor;
    private readonly HostResolver _hostResolver;
    private readonly OBSWebsocket _obs;

    // Signaled by Connected/Disconnected callbacks to unblock the connect loop
    private readonly SemaphoreSlim _connectionSignal = new(0, 1);
    private volatile bool _isConnected;
    private string? _resolvedUrl;

    public bool IsConnected => _isConnected;

    public ObsClient(
        ILogger<ObsClient> logger,
        IOptionsMonitor<ObsOptions> optionsMonitor,
        HostResolver hostResolver)
    {
        _logger = logger;
        _optionsMonitor = optionsMonitor;
        _hostResolver = hostResolver;
        _obs = new OBSWebsocket();

        _obs.Connected += OnConnected;
        _obs.Disconnected += OnDisconnected;
    }

    public async Task ConnectLoopAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(1);
        var maxDelay = TimeSpan.FromSeconds(_optionsMonitor.CurrentValue.ReconnectMaxSeconds);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var url = await BuildUrlAsync(ct);
                if (url == null)
                {
                    _logger.LogWarning(new EventId(1005, "ObsReconnect"),
                        "Cannot resolve OBS host '{Host}', retrying in {Delay}s",
                        _optionsMonitor.CurrentValue.Host, delay.TotalSeconds);
                    await Task.Delay(delay, ct);
                    delay = NextDelay(delay, maxDelay);
                    continue;
                }

                _resolvedUrl = url;
                _logger.LogInformation(new EventId(1005, "ObsReconnect"),
                    "Connecting to OBS at {Url}", url);

                await _connectionSignal.WaitAsync(0, ct);
                _obs.ConnectAsync(url, _optionsMonitor.CurrentValue.Password ?? string.Empty);

                // Wait for Connected or Disconnected callback
                await _connectionSignal.WaitAsync(ct);

                if (_isConnected)
                {
                    // Successfully connected — reset backoff and wait until disconnected
                    delay = TimeSpan.FromSeconds(1);
                    await WaitForDisconnectAsync(ct);
                }
                // If not connected (Disconnected fired immediately), fall through to retry
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("OBS connection attempt failed, retrying in {Delay}s - {Message}", delay.TotalSeconds, ex.Message);
            }

            if (!ct.IsCancellationRequested)
            {
                await Task.Delay(delay, ct).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                delay = NextDelay(delay, maxDelay);
            }
        }

        Disconnect();
    }

    public Task<bool> SetProgramSceneAsync(string scene, CancellationToken ct)
    {
        if (!_isConnected)
            return Task.FromResult(false);

        try
        {
            _obs.SetCurrentProgramScene(scene);
            _logger.LogInformation(new EventId(1300, "SceneChangeSent"),
                "Scene change sent: \"{Scene}\"", scene);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(new EventId(1301, "SceneChangeFailed"),
                ex, "Scene change failed for \"{Scene}\"", scene);
            return Task.FromResult(false);
        }
    }

    public Task<string?> GetCurrentProgramSceneAsync(CancellationToken ct)
    {
        if (!_isConnected)
            return Task.FromResult<string?>(null);

        try
        {
            var scene = _obs.GetCurrentProgramScene();
            return Task.FromResult<string?>(scene);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get current program scene");
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>
    /// Fired after a successful OBS connection (after auth). Use for cache seeding.
    /// </summary>
    public event EventHandler? Connected;

    /// <summary>
    /// Exposes the underlying OBS CurrentProgramSceneChanged event for scene cache updates.
    /// </summary>
    public event EventHandler<OBSWebsocketDotNet.Types.Events.ProgramSceneChangedEventArgs>? CurrentProgramSceneChanged
    {
        add => _obs.CurrentProgramSceneChanged += value;
        remove => _obs.CurrentProgramSceneChanged -= value;
    }

    private void OnConnected(object? sender, EventArgs e)
    {
        _isConnected = true;
        _logger.LogInformation(new EventId(1003, "ObsConnected"),
            "Connected to OBS at {Url}", _resolvedUrl);
        Connected?.Invoke(this, EventArgs.Empty);
        TrySignal();
    }

    private void OnDisconnected(object? sender, ObsDisconnectionInfo info)
    {
        var wasConnected = _isConnected;
        _isConnected = false;

        if (wasConnected)
        {
            _logger.LogWarning(new EventId(1004, "ObsDisconnected"),
                "Disconnected from OBS: {Reason}", info.DisconnectReason ?? "unknown");
        }

        TrySignal();
    }

    private void TrySignal()
    {
        // Release the semaphore if it's not already released (CurrentCount == 0 means someone is waiting)
        if (_connectionSignal.CurrentCount == 0)
            _connectionSignal.Release();
    }

    private async Task WaitForDisconnectAsync(CancellationToken ct)
    {
        // Wait for the Disconnected event to signal
        await _connectionSignal.WaitAsync(0, ct);
        await _connectionSignal.WaitAsync(ct);
    }

    private async Task<string?> BuildUrlAsync(CancellationToken ct)
    {
        var ip = await _hostResolver.ResolveAsync(_optionsMonitor.CurrentValue.Host, ct);
        if (ip == null)
            return null;

        var opts = _optionsMonitor.CurrentValue;
        _logger.LogDebug("Resolved OBS host '{Host}' to {Address}", opts.Host, ip);
        return $"ws://{HostResolver.FormatForUri(ip)}:{opts.Port}";
    }

    private static TimeSpan NextDelay(TimeSpan current, TimeSpan max)
    {
        var next = current * 2;
        return next > max ? max : next;
    }

    private void Disconnect()
    {
        try
        {
            if (_obs.IsConnected)
                _obs.Disconnect();
        }
        catch
        {
            // ignored
        }
    }

    public void Dispose()
    {
        _obs.Connected -= OnConnected;
        _obs.Disconnected -= OnDisconnected;
        Disconnect();
        _connectionSignal.Dispose();
    }
}

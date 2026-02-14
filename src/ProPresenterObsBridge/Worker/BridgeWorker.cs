using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProPresenterObsBridge.Mapping;
using ProPresenterObsBridge.Midi;
using ProPresenterObsBridge.Obs;
using ProPresenterObsBridge.Options;
using ProPresenterObsBridge.Util;

namespace ProPresenterObsBridge.Worker;

public sealed class BridgeWorker : BackgroundService
{
    private readonly ILogger<BridgeWorker> _logger;
    private readonly IMidiSource _midiSource;
    private readonly IMappingEngine _mappingEngine;
    private readonly ObsClient _obsClient;
    private readonly BehaviorOptions _behavior;
    private readonly RateLimiter _disconnectedLimiter;

    // Scene cache for IgnoreIfAlreadyOnScene
    private string? _cachedScene;

    public BridgeWorker(
        ILogger<BridgeWorker> logger,
        IMidiSource midiSource,
        IMappingEngine mappingEngine,
        ObsClient obsClient,
        IOptions<BehaviorOptions> behavior)
    {
        _logger = logger;
        _midiSource = midiSource;
        _mappingEngine = mappingEngine;
        _obsClient = obsClient;
        _behavior = behavior.Value;
        _disconnectedLimiter = new RateLimiter(
            TimeSpan.FromSeconds(_behavior.ObsDisconnectedLogIntervalSeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(new EventId(1000, "Startup"), "BridgeWorker started");

        // Subscribe to MIDI events
        _midiSource.NoteReceived += OnNoteReceived;

        // Subscribe to OBS events for scene cache
        if (_behavior.IgnoreIfAlreadyOnScene)
        {
            _obsClient.Connected += OnObsConnected;
            _obsClient.CurrentProgramSceneChanged += OnSceneChanged;
        }

        try
        {
            // Run the OBS connect loop until shutdown
            await _obsClient.ConnectLoopAsync(stoppingToken);
        }
        finally
        {
            _midiSource.NoteReceived -= OnNoteReceived;

            if (_behavior.IgnoreIfAlreadyOnScene)
            {
                _obsClient.Connected -= OnObsConnected;
                _obsClient.CurrentProgramSceneChanged -= OnSceneChanged;
            }
        }
    }

    private void OnNoteReceived(MidiNoteEvent evt)
    {
        if (!_mappingEngine.TryMap(evt, out var scene))
            return;

        if (!_obsClient.IsConnected)
        {
            if (_disconnectedLimiter.TryAcquire())
            {
                _logger.LogWarning(new EventId(1400, "DroppedDisconnected"),
                    "OBS disconnected; dropped scene change request for \"{Scene}\"", scene);
            }
            return;
        }

        if (_behavior.IgnoreIfAlreadyOnScene
            && string.Equals(_cachedScene, scene, StringComparison.Ordinal))
        {
            _logger.LogDebug("Scene already \"{Scene}\", skipping", scene);
            return;
        }

        // Fire-and-forget the scene change (MIDI callback is synchronous)
        _ = SendSceneChangeAsync(scene);
    }

    private async Task SendSceneChangeAsync(string scene)
    {
        var success = await _obsClient.SetProgramSceneAsync(scene, CancellationToken.None);
        if (success)
        {
            _cachedScene = scene;
        }
    }

    private void OnObsConnected(object? sender, EventArgs e)
    {
        // Seed the scene cache on connect
        _ = SeedCacheAsync();
    }

    private async Task SeedCacheAsync()
    {
        var scene = await _obsClient.GetCurrentProgramSceneAsync(CancellationToken.None);
        if (scene != null)
        {
            _cachedScene = scene;
            _logger.LogDebug("Scene cache seeded: \"{Scene}\"", scene);
        }
    }

    private void OnSceneChanged(object? sender, OBSWebsocketDotNet.Types.Events.ProgramSceneChangedEventArgs e)
    {
        _cachedScene = e.SceneName;
        _logger.LogDebug("Scene cache updated from OBS event: \"{Scene}\"", e.SceneName);
    }
}

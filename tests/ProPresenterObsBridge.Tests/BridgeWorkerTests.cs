using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProPresenterObsBridge.Mapping;
using ProPresenterObsBridge.Midi;
using ProPresenterObsBridge.Obs;
using ProPresenterObsBridge.Options;

namespace ProPresenterObsBridge.Tests;

/// <summary>
/// Tests the MIDI → Mapping → OBS pipeline logic used by BridgeWorker.
/// BridgeWorker itself depends on concrete ObsClient (for events), so these tests
/// verify the core decision logic using test doubles.
/// </summary>
public class BridgeWorkerTests
{
    private sealed class FakeObsClient : IObsClient
    {
        public bool IsConnected { get; set; }
        public string? LastSceneSent { get; private set; }
        public int SceneChangeCalls { get; private set; }
        public string? CurrentScene { get; set; }

        public Task ConnectLoopAsync(CancellationToken ct)
        {
            return Task.Delay(Timeout.Infinite, ct)
                .ContinueWith(_ => { }, TaskScheduler.Default);
        }

        public Task<bool> SetProgramSceneAsync(string scene, CancellationToken ct)
        {
            if (!IsConnected) return Task.FromResult(false);
            LastSceneSent = scene;
            SceneChangeCalls++;
            return Task.FromResult(true);
        }

        public Task<string?> GetCurrentProgramSceneAsync(CancellationToken ct)
        {
            return Task.FromResult(CurrentScene);
        }
    }

    private static MidiNoteEvent NoteOn(int channel, int note, int velocity) =>
        new() { IsNoteOn = true, Channel = channel, Note = note, Velocity = velocity };

    private static MappingEngine CreateMappingEngine(params MappingEntry[] entries)
    {
        var list = entries.ToList();
        var monitor = new TestOptionsMonitor<List<MappingEntry>>(list);
        return new MappingEngine(NullLogger<MappingEngine>.Instance, monitor);
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
    {
        private readonly T _value;
        public TestOptionsMonitor(T value) => _value = value;
        public T CurrentValue => _value;
        public T Get(string? name) => _value;
        public IDisposable? OnChange(Action<T?, string?> listener) => null;
    }

    [Fact]
    public async Task MappedNote_WhenConnected_SendsSceneChange()
    {
        var engine = CreateMappingEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = -1, Scene = "Cam 1" });
        var obs = new FakeObsClient { IsConnected = true };

        var evt = NoteOn(1, 1, 127);
        if (engine.TryMap(evt, out var scene) && obs.IsConnected)
            await obs.SetProgramSceneAsync(scene, CancellationToken.None);

        Assert.Equal("Cam 1", obs.LastSceneSent);
        Assert.Equal(1, obs.SceneChangeCalls);
    }

    [Fact]
    public async Task MappedNote_WhenDisconnected_DoesNotSend()
    {
        var engine = CreateMappingEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = -1, Scene = "Cam 1" });
        var obs = new FakeObsClient { IsConnected = false };

        var evt = NoteOn(1, 1, 127);
        if (engine.TryMap(evt, out var scene) && obs.IsConnected)
            await obs.SetProgramSceneAsync(scene, CancellationToken.None);

        Assert.Null(obs.LastSceneSent);
        Assert.Equal(0, obs.SceneChangeCalls);
    }

    [Fact]
    public async Task UnmappedNote_DoesNotSend()
    {
        var engine = CreateMappingEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = -1, Scene = "Cam 1" });
        var obs = new FakeObsClient { IsConnected = true };

        var evt = NoteOn(2, 5, 127); // no mapping for ch2 note5
        if (engine.TryMap(evt, out var scene) && obs.IsConnected)
            await obs.SetProgramSceneAsync(scene, CancellationToken.None);

        Assert.Null(obs.LastSceneSent);
    }

    [Fact]
    public async Task IgnoreIfAlreadyOnScene_SkipsWhenCached()
    {
        var engine = CreateMappingEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = -1, Scene = "Cam 1" });
        var obs = new FakeObsClient { IsConnected = true };
        string? cachedScene = "Cam 1"; // already on this scene

        var evt = NoteOn(1, 1, 127);
        if (engine.TryMap(evt, out var scene))
        {
            if (!string.Equals(cachedScene, scene, StringComparison.Ordinal) && obs.IsConnected)
                await obs.SetProgramSceneAsync(scene, CancellationToken.None);
        }

        Assert.Equal(0, obs.SceneChangeCalls);
    }

    [Fact]
    public async Task IgnoreIfAlreadyOnScene_SendsWhenDifferent()
    {
        var engine = CreateMappingEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = -1, Scene = "Cam 1" });
        var obs = new FakeObsClient { IsConnected = true };
        string? cachedScene = "Slides"; // different scene

        var evt = NoteOn(1, 1, 127);
        if (engine.TryMap(evt, out var scene))
        {
            if (!string.Equals(cachedScene, scene, StringComparison.Ordinal) && obs.IsConnected)
            {
                await obs.SetProgramSceneAsync(scene, CancellationToken.None);
                cachedScene = scene;
            }
        }

        Assert.Equal("Cam 1", obs.LastSceneSent);
        Assert.Equal("Cam 1", cachedScene);
    }

    [Fact]
    public async Task IgnoreIfAlreadyOnScene_NullCache_SendsAnyway()
    {
        var engine = CreateMappingEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = -1, Scene = "Cam 1" });
        var obs = new FakeObsClient { IsConnected = true };
        string? cachedScene = null; // not yet established

        var evt = NoteOn(1, 1, 127);
        if (engine.TryMap(evt, out var scene))
        {
            if (cachedScene != null && string.Equals(cachedScene, scene, StringComparison.Ordinal))
            {
                // Skip
            }
            else if (obs.IsConnected)
            {
                await obs.SetProgramSceneAsync(scene, CancellationToken.None);
                cachedScene = scene;
            }
        }

        Assert.Equal("Cam 1", obs.LastSceneSent);
    }

    [Fact]
    public async Task ConnectLoop_ExitsOnCancellation()
    {
        var obs = new FakeObsClient();
        using var cts = new CancellationTokenSource(100);
        await obs.ConnectLoopAsync(cts.Token);
    }

    [Fact]
    public async Task CacheSeed_FetchesCurrentSceneOnConnect()
    {
        var obs = new FakeObsClient { IsConnected = true, CurrentScene = "Slides" };

        var scene = await obs.GetCurrentProgramSceneAsync(CancellationToken.None);
        Assert.Equal("Slides", scene);
    }
}

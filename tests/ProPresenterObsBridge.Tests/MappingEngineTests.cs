using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProPresenterObsBridge.Mapping;
using ProPresenterObsBridge.Midi;
using ProPresenterObsBridge.Options;

namespace ProPresenterObsBridge.Tests;

public class MappingEngineTests
{
    private static MappingEngine CreateEngine(params MappingEntry[] entries)
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

    private static MidiNoteEvent NoteOn(int channel, int note, int velocity) =>
        new() { IsNoteOn = true, Channel = channel, Note = note, Velocity = velocity };

    private static MidiNoteEvent NoteOff(int channel, int note, int velocity = 0) =>
        new() { IsNoteOn = false, Channel = channel, Note = note, Velocity = velocity };

    [Fact]
    public void ExactMatch_ReturnsScene()
    {
        var engine = CreateEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = 100, Scene = "Cam 1" });

        Assert.True(engine.TryMap(NoteOn(1, 1, 100), out var scene));
        Assert.Equal("Cam 1", scene);
    }

    [Fact]
    public void ExactMatch_WrongVelocity_NoMatch()
    {
        var engine = CreateEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = 100, Scene = "Cam 1" });

        Assert.False(engine.TryMap(NoteOn(1, 1, 50), out _));
    }

    [Fact]
    public void WildcardVelocity_MatchesAnyVelocity()
    {
        var engine = CreateEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = -1, Scene = "Cam 1" });

        Assert.True(engine.TryMap(NoteOn(1, 1, 0), out var s1));
        Assert.Equal("Cam 1", s1);

        Assert.True(engine.TryMap(NoteOn(1, 1, 127), out var s2));
        Assert.Equal("Cam 1", s2);
    }

    [Fact]
    public void FirstMatchWins_OrderDeterminesPriority()
    {
        // Wildcard listed first — it wins even when an exact match exists later
        var engine = CreateEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = -1, Scene = "Default" },
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = 100, Scene = "Specific" });

        Assert.True(engine.TryMap(NoteOn(1, 1, 100), out var scene));
        Assert.Equal("Default", scene);
    }

    [Fact]
    public void ExactBeforeWildcard_ExactWins()
    {
        // Exact listed first — it wins for matching velocity
        var engine = CreateEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = 100, Scene = "Specific" },
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = -1, Scene = "Default" });

        Assert.True(engine.TryMap(NoteOn(1, 1, 100), out var scene));
        Assert.Equal("Specific", scene);
    }

    [Fact]
    public void ExactBeforeWildcard_WildcardFallback()
    {
        // Exact listed first but velocity doesn't match — wildcard catches it
        var engine = CreateEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = 100, Scene = "Specific" },
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = -1, Scene = "Default" });

        Assert.True(engine.TryMap(NoteOn(1, 1, 50), out var scene));
        Assert.Equal("Default", scene);
    }

    [Fact]
    public void NoteOff_Matches_OffMapping()
    {
        var engine = CreateEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = -1, Scene = "Cam On" },
            new MappingEntry { NoteType = "Off", Channel = 1, Note = 1, Velocity = -1, Scene = "Cam Off" });

        Assert.True(engine.TryMap(NoteOff(1, 1), out var scene));
        Assert.Equal("Cam Off", scene);
    }

    [Fact]
    public void NoteOn_DoesNotMatch_OffMapping()
    {
        var engine = CreateEngine(
            new MappingEntry { NoteType = "Off", Channel = 1, Note = 1, Velocity = -1, Scene = "Cam Off" });

        Assert.False(engine.TryMap(NoteOn(1, 1, 127), out _));
    }

    [Fact]
    public void DifferentChannel_NoMatch()
    {
        var engine = CreateEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = -1, Scene = "Cam 1" });

        Assert.False(engine.TryMap(NoteOn(2, 1, 127), out _));
    }

    [Fact]
    public void DifferentNote_NoMatch()
    {
        var engine = CreateEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = -1, Scene = "Cam 1" });

        Assert.False(engine.TryMap(NoteOn(1, 2, 127), out _));
    }

    [Fact]
    public void NoteType_CaseInsensitive()
    {
        var engine = CreateEngine(
            new MappingEntry { NoteType = "ON", Channel = 1, Note = 1, Velocity = -1, Scene = "Cam 1" });

        Assert.True(engine.TryMap(NoteOn(1, 1, 127), out var scene));
        Assert.Equal("Cam 1", scene);
    }

    [Fact]
    public void MultipleMappings_CorrectSceneSelected()
    {
        var engine = CreateEngine(
            new MappingEntry { NoteType = "On", Channel = 1, Note = 1, Velocity = -1, Scene = "Cam 1" },
            new MappingEntry { NoteType = "On", Channel = 1, Note = 2, Velocity = -1, Scene = "Cam 2" },
            new MappingEntry { NoteType = "On", Channel = 1, Note = 3, Velocity = -1, Scene = "Slides" });

        Assert.True(engine.TryMap(NoteOn(1, 2, 127), out var scene));
        Assert.Equal("Cam 2", scene);
    }

    [Fact]
    public void NoMappings_NoMatch()
    {
        var engine = CreateEngine();

        Assert.False(engine.TryMap(NoteOn(1, 1, 127), out _));
    }
}

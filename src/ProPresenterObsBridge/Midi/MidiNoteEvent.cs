namespace ProPresenterObsBridge.Midi;

public sealed class MidiNoteEvent
{
    public required bool IsNoteOn { get; init; }
    public required int Channel { get; init; }
    public required int Note { get; init; }
    public required int Velocity { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

namespace ProPresenterObsBridge.Midi;

public interface IMidiSource : IAsyncDisposable
{
    event Action<MidiNoteEvent>? NoteReceived;

    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

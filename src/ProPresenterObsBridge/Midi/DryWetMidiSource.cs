#if !WINDOWS
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProPresenterObsBridge.Options;

namespace ProPresenterObsBridge.Midi;

public sealed class DryWetMidiSource(
    IOptions<MidiOptions> midiOptions,
    ILogger<DryWetMidiSource> logger) : IMidiSource
{
    private VirtualDevice? _virtualDevice;
    private InputDevice? _inputDevice;

    public event Action<MidiNoteEvent>? NoteReceived;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var deviceName = midiOptions.Value.DeviceName;

        _virtualDevice = VirtualDevice.Create(deviceName);

        _inputDevice = _virtualDevice.InputDevice
            ?? throw new InvalidOperationException(
                $"Virtual MIDI device '{deviceName}' was created but has no input subdevice.");

        _inputDevice.ErrorOccurred += OnError;
        _inputDevice.EventReceived += OnEventReceived;
        _inputDevice.StartEventsListening();

        logger.LogInformation("Virtual MIDI device '{DeviceName}' created and listening", deviceName);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        DisposeVirtualDevice();
        return Task.CompletedTask;
    }

    private void OnError(object? sender, ErrorOccurredEventArgs e)
    {
        logger.LogError(e.Exception, "MIDI device error: {Message}", e.Exception.Message);
    }

    private void OnEventReceived(object? sender, MidiEventReceivedEventArgs e)
    {
        logger.LogDebug("MIDI event received: {EventType}", e.Event.GetType().Name);
        switch (e.Event)
        {
            case NoteOnEvent noteOn when noteOn.Velocity > 0:
                FireNote(isNoteOn: true, noteOn.Channel, noteOn.NoteNumber, noteOn.Velocity);
                break;

            case NoteOnEvent noteOn: // velocity 0 = note off per MIDI spec
                FireNote(isNoteOn: false, noteOn.Channel, noteOn.NoteNumber, 0);
                break;

            case NoteOffEvent noteOff:
                FireNote(isNoteOn: false, noteOff.Channel, noteOff.NoteNumber, noteOff.Velocity);
                break;

            default:
                logger.LogDebug("Ignoring non-note MIDI event: {EventType}", e.Event.GetType().Name);
                break;
        }
    }

    private void FireNote(bool isNoteOn, int channel, int note, int velocity)
    {
        // DryWetMidi channels are 0-based; our model uses 1-based per MIDI spec display convention
        var evt = new MidiNoteEvent
        {
            IsNoteOn = isNoteOn,
            Channel = channel + 1,
            Note = note,
            Velocity = velocity
        };

        logger.LogDebug("{NoteType} Ch={Channel} Note={Note} Vel={Velocity}",
            isNoteOn ? "Note:On" : "Note:Off", evt.Channel, evt.Note, evt.Velocity);

        NoteReceived?.Invoke(evt);
    }

    public ValueTask DisposeAsync()
    {
        DisposeVirtualDevice();
        return ValueTask.CompletedTask;
    }

    private void DisposeVirtualDevice()
    {
        if (_virtualDevice is null)
            return;

        if (_inputDevice is not null)
        {
            try { _inputDevice.StopEventsListening(); }
            catch (ObjectDisposedException) { }

            _inputDevice.EventReceived -= OnEventReceived;
            _inputDevice.ErrorOccurred -= OnError;
            _inputDevice = null;
        }

        _virtualDevice.Dispose();
        _virtualDevice = null;
        logger.LogInformation("Virtual MIDI device stopped and released");
    }
}
#endif

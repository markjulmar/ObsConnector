using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ProPresenterObsBridge.Midi;

public sealed class MidiHostedService(IMidiSource midiSource, ILogger<MidiHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting MIDI source...");
        await midiSource.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping MIDI source...");
        await midiSource.StopAsync(cancellationToken);
    }
}

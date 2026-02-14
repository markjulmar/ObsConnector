using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProPresenterObsBridge.Midi;
using ProPresenterObsBridge.Options;

namespace ProPresenterObsBridge.Mapping;

public sealed class MappingEngine(ILogger<MappingEngine> logger, IOptionsMonitor<List<MappingEntry>> mappingsMonitor) : IMappingEngine
{
    public bool TryMap(MidiNoteEvent evt, out string scene)
    {
        var noteType = evt.IsNoteOn ? "On" : "Off";
        var mappings = mappingsMonitor.CurrentValue;

        var match = mappings.FirstOrDefault(m => 
            string.Equals(m.NoteType, noteType, StringComparison.OrdinalIgnoreCase)
                            && m.Channel == evt.Channel && m.Note == evt.Note
                            && (m.Velocity == -1 || m.Velocity == evt.Velocity));

        scene = match?.Scene ?? string.Empty;

        if (string.IsNullOrEmpty(scene))
        {
            logger.LogDebug("No mapping for {NoteType} Ch={Channel} Note={Note} Vel={Velocity}",
                noteType, evt.Channel, evt.Note, evt.Velocity);
            return false;
        }

        logger.LogDebug("{NoteType} Ch={Channel} Note={Note} Vel={Velocity} -> \"{Scene}\"",
            noteType, evt.Channel, evt.Note, evt.Velocity, scene);
        return true;
    }
}

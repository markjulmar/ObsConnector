using ProPresenterObsBridge.Midi;

namespace ProPresenterObsBridge.Mapping;

public interface IMappingEngine
{
    bool TryMap(MidiNoteEvent evt, out string scene);
}

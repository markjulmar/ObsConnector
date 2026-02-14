# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
dotnet build                           # Build entire solution
dotnet test                            # Run all 34 tests
dotnet test --filter "ClassName.Method" # Run a single test
dotnet run --project src/ProPresenterObsBridge  # Run locally (creates real virtual MIDI device on macOS)
```

Publish for Windows deployment:
```bash
dotnet publish src/ProPresenterObsBridge -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/
```

## Architecture

ProPresenter → Virtual MIDI → OBS Scene Switch Bridge. A .NET 9 Generic Host app that creates a virtual MIDI device, receives Note On/Off messages from ProPresenter, maps them to OBS scene names, and sends scene changes over the network via obs-websocket v5. See `spec.txt` for the full specification and `MILESTONES.md` for build plan/progress.

### Pipeline flow

`IMidiSource` (NoteReceived event) → `IMappingEngine.TryMap()` → `IObsClient.SetProgramSceneAsync()`, orchestrated by `BridgeWorker` (a BackgroundService).

### Key interfaces

- **IMidiSource** — MIDI event source with Start/Stop lifecycle and `NoteReceived` event
- **IMappingEngine** — `TryMap(MidiNoteEvent, out string scene)` with exact-then-wildcard priority
- **IObsClient** — OBS connection, scene switching, scene-changed event for cache

### Platform-conditional compilation

- `#if WINDOWS` compile constant defined in csproj via `$([MSBuild]::IsOSPlatform('Windows'))`
- Windows-only NuGet packages (WindowsServices, EventLog) gated by the same MSBuild condition
- macOS uses `DryWetMidiSource` (real virtual MIDI device via DryWetMidi); Windows uses `WindowsMidiEndpointManager`
- `Program.ConfigureWindows()` is the only method guarded by `#if WINDOWS`

### DI registration (Program.cs)

Options use `Configure<T>()`. Mappings are registered as `IReadOnlyList<MappingEntry>`. `ObsClient` is registered as both its concrete type and forwarded to `IObsClient`. Platform-specific `IMidiSource` is chosen at runtime. Two hosted services: `MidiHostedService` and `BridgeWorker`.

### Test structure

xUnit tests in `tests/ProPresenterObsBridge.Tests/`. Tests use NullLogger and hand-rolled fakes (no mocking framework). Test files mirror source structure: `MappingEngineTests`, `BridgeWorkerTests`, `ObsClientTests`, `HostResolverTests`, `RateLimiterTests`.

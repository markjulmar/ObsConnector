# Copilot Instructions

This repository contains ProPresenter-OBS Bridge, a cross-platform .NET 9 application that creates a virtual MIDI device and bridges ProPresenter MIDI output to OBS scene changes over the network.

## Architecture Overview

**Pipeline:** ProPresenter → Virtual MIDI Device → OBS Scene Switch Bridge

The application receives MIDI Note On/Off messages from ProPresenter, maps them to OBS scene names via configurable rules, and forwards scene changes to a remote OBS instance using obs-websocket v5. It runs as a Windows Service on Windows or a background daemon on macOS/Linux.

### Key Components

- **IMidiSource** — MIDI event source with Start/Stop lifecycle and `NoteReceived` event
- **IMappingEngine** — `TryMap(MidiNoteEvent, out string scene)` with exact-then-wildcard priority
- **IObsClient** — OBS connection, scene switching, scene-changed event for cache
- **BridgeWorker** — BackgroundService that orchestrates MIDI → mapping → OBS

### Platform-Specific Compilation

- `#if WINDOWS` compile constant defined in csproj via `$([MSBuild]::IsOSPlatform('Windows'))`
- Windows-only NuGet packages (WindowsServices, EventLog) gated by the same MSBuild condition
- macOS uses `DryWetMidiSource` (real virtual MIDI device via DryWetMidi)
- Windows uses `WindowsMidiEndpointManager`
- `Program.ConfigureWindows()` is the only method guarded by `#if WINDOWS`

## Build & Test Commands

```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run a specific test
dotnet test --filter "ClassName.Method"

# Run locally (creates real virtual MIDI device on macOS)
dotnet run --project src/ProPresenterObsBridge
```

### Publishing for Deployment

```bash
# Windows (x64)
dotnet publish src/ProPresenterObsBridge -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/

# macOS (x64)
dotnet publish src/ProPresenterObsBridge -c Release -r osx-x64 --self-contained -o publish/
```

## Coding Conventions

### Dependency Injection

- Options use `Configure<T>()` pattern
- Mappings are registered as `IReadOnlyList<MappingEntry>`
- `ObsClient` is registered as both its concrete type and forwarded to `IObsClient`
- Platform-specific `IMidiSource` is chosen at runtime
- Two hosted services: `MidiHostedService` and `BridgeWorker`

### Testing Practices

- xUnit tests in `tests/ProPresenterObsBridge.Tests/`
- Use `NullLogger<T>` for logger instances in tests
- Hand-rolled fakes (no mocking framework)
- Test files mirror source structure: `MappingEngineTests`, `BridgeWorkerTests`, `ObsClientTests`, `HostResolverTests`, `RateLimiterTests`
- Always test both success and failure paths

### Code Structure

```
src/ProPresenterObsBridge/
├── Program.cs                    # Host/bootstrap
├── Options/                      # Strongly typed config
├── Midi/                         # MIDI device management
├── Mapping/                      # MIDI → Scene mapping logic
├── Obs/                          # OBS websocket client
├── Worker/                       # Background service orchestration
└── Util/                         # Rate limiting, host resolution
```

### Logging

- Use structured logging with `Microsoft.Extensions.Logging`
- Windows Service mode: logs to Windows Event Log (source: `ProPresenterObsBridge`)
- Console mode: logs to console
- Use consistent event IDs:
  - 1000: Startup
  - 1001: MIDI runtime missing (Fatal)
  - 1002: Virtual MIDI device created
  - 1003: OBS connected
  - 1004: OBS disconnected
  - 1005: OBS reconnect attempt
  - 1300: Scene change sent
  - 1301: Scene change failed
  - 1400: Dropped due to OBS disconnected (rate-limited)
- Never log passwords or secrets

## Important Facts

- **MIDI Device Persistence:** Virtual MIDI devices only exist while the application is running. Install as a service (Windows) or LaunchAgent (macOS) for automatic startup after reboots.
- **Single Instance:** Only one instance of the app can run at a time (mutex guard).
- **OBS Disconnection:** If OBS is disconnected, requests are dropped immediately (no queue, no retry) with rate-limited logging.
- **Case-Sensitive Scene Names:** OBS scene names in mappings must match exactly (case-sensitive).
- **No Mocking Frameworks:** Tests use hand-rolled fakes to maintain simplicity and control.

## References

- See `spec.txt` for full technical specification
- See `CLAUDE.md` for Claude Code-specific guidance
- See `README.md` for user documentation and setup instructions

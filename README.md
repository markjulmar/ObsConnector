# ProPresenter-OBS Bridge

A Windows service that creates a virtual MIDI device and bridges ProPresenter MIDI output to OBS scene changes over the network. ProPresenter sends MIDI Note On/Off messages, which are mapped to OBS program scenes via configurable rules, and forwarded to a remote OBS instance using obs-websocket v5.

## Prerequisites

- **Windows 10/11 (x64)**
- **[Windows MIDI Services](https://aka.ms/midi)** runtime and tools installed on the ProPresenter PC. The app creates a virtual MIDI device using this SDK; without it the app will exit with event 1001.
- **OBS Studio** with **obs-websocket v5** enabled on the OBS PC (default port 4455). Set a password under Tools > obs-websocket Settings.
- **.NET 9 Runtime** (included if using the self-contained publish)

## Configuration

Edit `appsettings.json` next to the executable.

```json
{
  "Midi": {
    "DeviceName": "ProPresenter-OBS Bridge",
    "ProductInstanceId": "com.julmar.pp-obs-bridge"
  },
  "Obs": {
    "Host": "OBS-PC",
    "Port": 4455,
    "Password": "your-obs-password",
    "ReconnectMaxSeconds": 30
  },
  "Behavior": {
    "ObsDisconnectedLogIntervalSeconds": 10,
    "IgnoreIfAlreadyOnScene": false
  },
  "Mappings": [
    { "NoteType": "On", "Channel": 1, "Note": 1, "Velocity": -1, "Scene": "Cam 1" },
    { "NoteType": "On", "Channel": 1, "Note": 2, "Velocity": -1, "Scene": "Cam 2" },
    { "NoteType": "On", "Channel": 1, "Note": 3, "Velocity": -1, "Scene": "Slides" }
  ]
}
```

### Midi

| Setting | Description | Default |
|---------|-------------|---------|
| `DeviceName` | Name of the virtual MIDI device visible in ProPresenter | `ProPresenter-OBS Bridge` |
| `ProductInstanceId` | Stable unique ID for the virtual device | `com.julmar.pp-obs-bridge` |

### Obs

| Setting | Description | Default |
|---------|-------------|---------|
| `Host` | OBS machine hostname, IP, or FQDN | *(required)* |
| `Port` | obs-websocket port | `4455` |
| `Password` | obs-websocket password (never logged) | *(empty)* |
| `ReconnectMaxSeconds` | Max backoff delay between reconnect attempts | `30` |

### Behavior

| Setting | Description | Default |
|---------|-------------|---------|
| `ObsDisconnectedLogIntervalSeconds` | Rate-limit interval for "OBS disconnected" warnings | `10` |
| `IgnoreIfAlreadyOnScene` | Skip scene change if OBS is already on the target scene | `false` |

### Mappings

Each entry maps a MIDI event to an OBS program scene:

| Field | Description | Values |
|-------|-------------|--------|
| `NoteType` | MIDI message type | `On` or `Off` (case-insensitive, defaults to `On`) |
| `Channel` | MIDI channel | `1`–`16` |
| `Note` | MIDI note number | `0`–`127` |
| `Velocity` | Velocity match (`-1` = any velocity) | `-1` or `0`–`127` |
| `Scene` | OBS program scene name | *(required)* |

Match priority: exact velocity match first, then wildcard (`-1`), then no match.

## Building and Publishing

Build:
```
dotnet build src/ProPresenterObsBridge
```

Publish a self-contained single-file executable for Windows:
```
dotnet publish src/ProPresenterObsBridge -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/
```

Or run the included script:
```powershell
.\publish.ps1
```

The output is `publish\ProPresenterObsBridge.exe`.

## Running as Console

Run the executable directly for development or testing:

```
.\publish\ProPresenterObsBridge.exe
```

Logs output to the console. Press Ctrl+C to stop.

## Installing as Windows Service

Run PowerShell as Administrator:

```powershell
.\install-service.ps1
```

This will:
1. Register the `ProPresenterObsBridge` Event Log source
2. Create the Windows Service with automatic startup
3. Configure failure recovery (restart on failure, 3 attempts)
4. Start the service

Optional parameters:
```powershell
.\install-service.ps1 -ExePath "C:\path\to\ProPresenterObsBridge.exe" -ServiceName "MyBridge"
```

To uninstall:
```powershell
.\uninstall-service.ps1
```

When running as a service, logs go to the Windows Event Log (Application log, source `ProPresenterObsBridge`).

## Verifying the Virtual MIDI Device

1. Start the service (or run the console app)
2. Open ProPresenter > Preferences > Inputs & Outputs > MIDI
3. The configured device name (default: `ProPresenter-OBS Bridge`) should appear in the device list
4. Select it and configure ProPresenter actions to send MIDI Note On messages

## Troubleshooting

### "Windows MIDI Services runtime not found" (Event 1001)
Install the [Windows MIDI Services](https://aka.ms/midi) runtime and tools, then restart the service.

### OBS not connecting
- Verify OBS is running with obs-websocket v5 enabled (Tools > obs-websocket Settings)
- Check that `Obs.Host` resolves to the OBS machine (try `ping OBS-PC`)
- Verify `Obs.Port` matches the obs-websocket port setting (default 4455)
- Check that the password matches
- The app reconnects automatically with exponential backoff; check logs for events 1004/1005

### Scene changes not working
- Confirm the scene name in `appsettings.json` matches the OBS scene name exactly (case-sensitive)
- Check that ProPresenter is sending to the correct MIDI device and channel/note
- Enable Debug logging to see MIDI events and mapping decisions
- If `IgnoreIfAlreadyOnScene` is `true`, repeated notes for the current scene are intentionally suppressed

### Single-instance guard
Only one instance of the app can run at a time. If you see a "mutex" error, check for an existing running instance or service.

### Name resolution failures
If `Obs.Host` is a hostname, ensure DNS or NetBIOS resolution works from the ProPresenter PC. Try using an IP address as a fallback.

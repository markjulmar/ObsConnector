# ProPresenter-OBS Bridge

A cross-platform application that creates a virtual MIDI device and bridges ProPresenter MIDI output to OBS scene changes over the network. ProPresenter sends MIDI Note On/Off messages, which are mapped to OBS program scenes via configurable rules, and forwarded to a remote OBS instance using obs-websocket v5.

Runs as a Windows Service on Windows or a background daemon on macOS/Linux.

## Prerequisites

### Windows
- **Windows 10/11 (x64)**
- **[Windows MIDI Services](https://aka.ms/midi)** runtime and tools installed on the ProPresenter PC. The app creates a virtual MIDI device using this SDK; without it the app will exit with event 1001.
- **OBS Studio** with **obs-websocket v5** enabled on the OBS PC (default port 4455). Set a password under Tools > obs-websocket Settings.
- **.NET 9 Runtime** (included if using the self-contained publish)

### macOS
- **macOS 10.15 (Catalina) or later**
- **OBS Studio** with **obs-websocket v5** enabled
- **.NET 9 Runtime** (included if using the self-contained publish)

## Important: MIDI Device Persistence

**The virtual MIDI device only exists when the application is running.** To make the MIDI port appear automatically after system reboots:

- **Windows**: Install as a Windows Service (see below) — the service is configured for automatic startup
- **macOS**: Install as a LaunchAgent (see below) — launches automatically at login

Without automatic startup, you must manually start the application after each reboot for the MIDI device to be available.

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

## Running as Console (Windows & macOS)

Run the executable directly for development or testing:

**Windows:**
```
.\publish\ProPresenterObsBridge.exe
```

**macOS:**
```
./publish/ProPresenterObsBridge
```

Logs output to the console. Press Ctrl+C to stop. **Note:** The virtual MIDI device will disappear when the application stops.

## Installing as Windows Service (Recommended for Windows)

Run PowerShell as Administrator:

```powershell
.\install-service.ps1
```

This will:
1. Register the `ProPresenterObsBridge` Event Log source
2. Create the Windows Service with **delayed automatic startup** (starts after boot completes)
3. Set a descriptive service description
4. Configure failure recovery (restart on failure, 3 attempts)
5. Start the service

**The virtual MIDI device will now be available automatically after every reboot.**

Optional parameters:
```powershell
.\install-service.ps1 -ExePath "C:\path\to\ProPresenterObsBridge.exe" -ServiceName "MyBridge"
```

To uninstall:
```powershell
.\uninstall-service.ps1
```

When running as a service, logs go to the Windows Event Log (Application log, source `ProPresenterObsBridge`).

## Installing as macOS LaunchAgent (Recommended for macOS)

To make the virtual MIDI device available automatically after login:

1. Publish the application for macOS:
   ```bash
   dotnet publish src/ProPresenterObsBridge -c Release -r osx-x64 --self-contained -o ~/bin/
   ```

2. Run the installation script:
   ```bash
   ./install-macos-service.sh
   ```

3. When prompted, enter the path to the executable (default: `/usr/local/bin/ProPresenterObsBridge`)

The script will:
- Install a LaunchAgent that runs automatically at login
- Configure automatic restart if the application crashes
- Set up log files in `/usr/local/var/log/`

**The virtual MIDI device will now be available automatically after every login.**

### Managing the macOS LaunchAgent

Check status:
```bash
launchctl list | grep propresenter-obs-bridge
```

View logs:
```bash
tail -f /usr/local/var/log/propresenter-obs-bridge.log
```

Stop the service:
```bash
launchctl unload ~/Library/LaunchAgents/com.julmar.propresenter-obs-bridge.plist
```

Start the service:
```bash
launchctl load ~/Library/LaunchAgents/com.julmar.propresenter-obs-bridge.plist
```

Uninstall:
```bash
./uninstall-macos-service.sh
```

## Verifying the Virtual MIDI Device

**Windows:**
1. Ensure the service is running (check Services.msc or run the console app)
2. Open ProPresenter > Preferences > Inputs & Outputs > MIDI
3. The configured device name (default: `ProPresenter-OBS Bridge`) should appear in the device list
4. Select it and configure ProPresenter actions to send MIDI Note On messages

**macOS:**
1. Ensure the LaunchAgent is running (or run the console app)
2. Open ProPresenter > Preferences > Inputs & Outputs > MIDI
3. The configured device name should appear in the device list
4. Select it and configure ProPresenter actions to send MIDI Note On messages

## Troubleshooting

### MIDI device not appearing after reboot

**Windows:**
- Check if the service is running: Open Services.msc and look for "ProPresenter-OBS Bridge"
- If stopped, check Event Log (Application log, source `ProPresenterObsBridge`) for errors
- Verify the service startup type is "Automatic (Delayed Start)"
- Try starting the service manually to see if it works

**macOS:**
- Check if the LaunchAgent is running: `launchctl list | grep propresenter-obs-bridge`
- Check the log files: `tail -f /usr/local/var/log/propresenter-obs-bridge.error.log`
- Verify the plist file exists: `ls ~/Library/LaunchAgents/com.julmar.propresenter-obs-bridge.plist`
- Try loading it manually: `launchctl load ~/Library/LaunchAgents/com.julmar.propresenter-obs-bridge.plist`

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

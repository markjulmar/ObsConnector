#!/bin/bash

# install-macos-service.sh
# Installs ProPresenter-OBS Bridge as a macOS LaunchAgent for automatic startup

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLIST_FILE="com.julmar.propresenter-obs-bridge.plist"
PLIST_SOURCE="$SCRIPT_DIR/$PLIST_FILE"
LAUNCH_AGENTS_DIR="$HOME/Library/LaunchAgents"
PLIST_DEST="$LAUNCH_AGENTS_DIR/$PLIST_FILE"

# Check if running on macOS
if [[ "$(uname)" != "Darwin" ]]; then
    echo "Error: This script is for macOS only."
    exit 1
fi

# Verify the plist file exists
if [[ ! -f "$PLIST_SOURCE" ]]; then
    echo "Error: Plist file not found at $PLIST_SOURCE"
    exit 1
fi

# Prompt for the executable path
read -p "Enter the full path to ProPresenterObsBridge executable [/usr/local/bin/ProPresenterObsBridge]: " EXEC_PATH
EXEC_PATH=${EXEC_PATH:-/usr/local/bin/ProPresenterObsBridge}

# Verify the executable exists
if [[ ! -f "$EXEC_PATH" ]]; then
    echo "Error: Executable not found at $EXEC_PATH"
    echo "Please publish the application first using: dotnet publish -c Release -r osx-x64"
    exit 1
fi

# Create LaunchAgents directory if it doesn't exist
mkdir -p "$LAUNCH_AGENTS_DIR"

# Copy and customize the plist file
echo "Installing LaunchAgent..."
cp "$PLIST_SOURCE" "$PLIST_DEST"

# Update the executable path in the plist
if [[ "$(uname)" == "Darwin" ]]; then
    sed -i '' "s|/usr/local/bin/ProPresenterObsBridge|$EXEC_PATH|g" "$PLIST_DEST"
else
    sed -i "s|/usr/local/bin/ProPresenterObsBridge|$EXEC_PATH|g" "$PLIST_DEST"
fi

# Update working directory to match executable location
EXEC_DIR="$(dirname "$EXEC_PATH")"
if [[ "$(uname)" == "Darwin" ]]; then
    sed -i '' "s|<string>/usr/local/bin</string>|<string>$EXEC_DIR</string>|g" "$PLIST_DEST"
else
    sed -i "s|<string>/usr/local/bin</string>|<string>$EXEC_DIR</string>|g" "$PLIST_DEST"
fi

# Load the LaunchAgent
echo "Loading LaunchAgent..."
launchctl unload "$PLIST_DEST" 2>/dev/null || true
launchctl load "$PLIST_DEST"

echo ""
echo "✓ LaunchAgent installed successfully!"
echo ""
echo "The service will:"
echo "  • Start automatically at login"
echo "  • Restart automatically if it crashes"
echo "  • Create a virtual MIDI device '$MIDI_DEVICE_NAME' when running"
echo ""
echo "Log files:"
echo "  • Output: /usr/local/var/log/propresenter-obs-bridge.log"
echo "  • Errors: /usr/local/var/log/propresenter-obs-bridge.error.log"
echo ""
echo "To check status:"
echo "  launchctl list | grep propresenter-obs-bridge"
echo ""
echo "To view logs:"
echo "  tail -f /usr/local/var/log/propresenter-obs-bridge.log"
echo ""
echo "To stop the service:"
echo "  launchctl unload ~/Library/LaunchAgents/$PLIST_FILE"
echo ""
echo "To start the service:"
echo "  launchctl load ~/Library/LaunchAgents/$PLIST_FILE"

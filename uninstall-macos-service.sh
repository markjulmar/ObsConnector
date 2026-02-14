#!/bin/bash

# uninstall-macos-service.sh
# Uninstalls the ProPresenter-OBS Bridge LaunchAgent

set -e

PLIST_FILE="com.julmar.propresenter-obs-bridge.plist"
PLIST_PATH="$HOME/Library/LaunchAgents/$PLIST_FILE"

# Check if running on macOS
if [[ "$(uname)" != "Darwin" ]]; then
    echo "Error: This script is for macOS only."
    exit 1
fi

# Check if the plist exists
if [[ ! -f "$PLIST_PATH" ]]; then
    echo "LaunchAgent not found at $PLIST_PATH"
    echo "Nothing to uninstall."
    exit 0
fi

echo "Unloading LaunchAgent..."
launchctl unload "$PLIST_PATH" 2>/dev/null || true

echo "Removing LaunchAgent file..."
rm -f "$PLIST_PATH"

echo ""
echo "✓ LaunchAgent uninstalled successfully!"
echo ""
echo "The virtual MIDI device will no longer be created automatically."
echo "You can still run the application manually if needed."

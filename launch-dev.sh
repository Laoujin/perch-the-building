#!/bin/bash
set -e

dotnet build src/Perch.Desktop -c Debug

TEMP_DIR="/tmp/perch-dev-$$"
mkdir -p "$TEMP_DIR"
cp -r src/Perch.Desktop/bin/Debug/net10.0-windows/* "$TEMP_DIR/"

echo "Launching from $TEMP_DIR"
"$TEMP_DIR/Perch.Desktop.exe" &
echo "PID: $!"

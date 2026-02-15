#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
TARGETS_DIR="$SCRIPT_DIR/_targets"
CONFIG_REPO="$SCRIPT_DIR/sample-config-repo"

echo "=== Perch Integration Smoke Test ==="
echo ""

# Clean previous run
if [ -d "$TARGETS_DIR" ]; then
    echo "Cleaning previous test run..."
    rm -rf "$TARGETS_DIR"
fi

# Setup target directories (simulating home/appdata)
echo "Setting up target directories..."
mkdir -p "$TARGETS_DIR"
mkdir -p "$TARGETS_DIR/Code/User"

# Pre-populate a file for backup test (vscode settings already exists)
echo '{"old": "this should get backed up"}' > "$TARGETS_DIR/Code/User/settings.json"

echo "Pre-existing file at: $TARGETS_DIR/Code/User/settings.json"
echo ""

# Set the env var that manifests reference
export PERCH_TEST_HOME="$TARGETS_DIR"

echo "=== RUN 1: First deploy (creates symlinks, backs up existing) ==="
echo ""
dotnet run --project "$PROJECT_ROOT/src/Perch.Cli" -- deploy --config-path "$CONFIG_REPO"
EXIT_CODE=$?
echo ""
echo "Exit code: $EXIT_CODE"
echo ""

echo "=== Inspecting results ==="
echo ""
echo "--- Symlinks created ---"

# Check git symlink
if [ -L "$TARGETS_DIR/.gitconfig" ]; then
    echo "[OK] .gitconfig is a symlink -> $(readlink "$TARGETS_DIR/.gitconfig")"
else
    echo "[FAIL] .gitconfig is NOT a symlink"
fi

# Check vscode symlinks
if [ -L "$TARGETS_DIR/Code/User/settings.json" ]; then
    echo "[OK] Code/User/settings.json is a symlink -> $(readlink "$TARGETS_DIR/Code/User/settings.json")"
else
    echo "[FAIL] Code/User/settings.json is NOT a symlink"
fi

if [ -L "$TARGETS_DIR/Code/User/keybindings.json" ]; then
    echo "[OK] Code/User/keybindings.json is a symlink -> $(readlink "$TARGETS_DIR/Code/User/keybindings.json")"
else
    echo "[FAIL] Code/User/keybindings.json is NOT a symlink"
fi

# Check backup was created
echo ""
echo "--- Backups ---"
BACKUP_FILES=$(find "$TARGETS_DIR" -name "*.backup*" 2>/dev/null || true)
if [ -n "$BACKUP_FILES" ]; then
    echo "[OK] Backup files found:"
    echo "$BACKUP_FILES"
else
    echo "[WARN] No backup files found (expected settings.json.backup*)"
fi

# Check broken-app was skipped
echo ""
echo "--- Error handling ---"
if [ ! -e "$TARGETS_DIR/nonexistent-dir" ]; then
    echo "[OK] nonexistent-dir was not created (broken-app correctly skipped)"
else
    echo "[FAIL] nonexistent-dir was created (broken-app should have been skipped)"
fi

echo ""
echo "=== RUN 2: Re-deploy (should skip everything — idempotent) ==="
echo ""
dotnet run --project "$PROJECT_ROOT/src/Perch.Cli" -- deploy --config-path "$CONFIG_REPO"
EXIT_CODE=$?
echo ""
echo "Exit code: $EXIT_CODE"

echo ""
echo "=== Full directory listing ==="
find "$TARGETS_DIR" -type f -o -type l | sort

echo ""
echo "=== Done ==="
echo "Inspect results at: $TARGETS_DIR"
echo "To clean up: rm -rf $TARGETS_DIR"

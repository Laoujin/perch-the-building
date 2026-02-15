$ErrorActionPreference = 'Continue'

$ScriptDir = $PSScriptRoot
$ProjectRoot = Resolve-Path "$ScriptDir\..\.."
$TargetsDir = "$ScriptDir\_targets"
$ConfigRepo = "$ScriptDir\sample-config-repo"

Write-Host "=== Perch Integration Smoke Test ===" -ForegroundColor Cyan
Write-Host ""

# Clean previous run
if (Test-Path $TargetsDir) {
    Write-Host "Cleaning previous test run..."
    Remove-Item $TargetsDir -Recurse -Force
}

# Setup target directories
Write-Host "Setting up target directories..."
New-Item -ItemType Directory -Path "$TargetsDir\Code\User" -Force | Out-Null

# Pre-populate a file for backup test
'{"old": "this should get backed up"}' | Set-Content "$TargetsDir\Code\User\settings.json"
Write-Host "Pre-existing file at: $TargetsDir\Code\User\settings.json"
Write-Host ""

# Set the env var that manifests reference
$env:PERCH_TEST_HOME = $TargetsDir

# --- RUN 1 ---
Write-Host "=== RUN 1: First deploy (creates symlinks, backs up existing) ===" -ForegroundColor Yellow
Write-Host ""
dotnet run --project "$ProjectRoot\src\Perch.Cli" -- deploy --config-path $ConfigRepo
$exitCode = $LASTEXITCODE
Write-Host ""
Write-Host "Exit code: $exitCode"
Write-Host ""

# --- Inspect results ---
Write-Host "=== Inspecting results ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "--- Symlinks ---"

$checks = @(
    @{ Path = "$TargetsDir\.gitconfig"; Label = ".gitconfig" }
    @{ Path = "$TargetsDir\Code\User\settings.json"; Label = "Code\User\settings.json" }
    @{ Path = "$TargetsDir\Code\User\keybindings.json"; Label = "Code\User\keybindings.json" }
)

foreach ($check in $checks) {
    $item = Get-Item $check.Path -Force -ErrorAction SilentlyContinue
    $isSymlink = $item -and ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint)
    if ($isSymlink) {
        Write-Host "[OK]   $($check.Label) is a symlink" -ForegroundColor Green
    } elseif ($item) {
        Write-Host "[FAIL] $($check.Label) exists but is NOT a symlink" -ForegroundColor Red
    } else {
        Write-Host "[FAIL] $($check.Label) does not exist" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "--- Backups ---"
$backups = Get-ChildItem $TargetsDir -Recurse -Filter "*.backup*" -ErrorAction SilentlyContinue
if ($backups) {
    Write-Host "[OK]   Backup files found:" -ForegroundColor Green
    $backups | ForEach-Object { Write-Host "       $($_.FullName)" }
} else {
    Write-Host "[WARN] No backup files found (expected settings.json.backup*)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "--- Error handling ---"
if (-not (Test-Path "$TargetsDir\nonexistent-dir")) {
    Write-Host "[OK]   nonexistent-dir was not created (broken-app correctly skipped)" -ForegroundColor Green
} else {
    Write-Host "[FAIL] nonexistent-dir was created (broken-app should have been skipped)" -ForegroundColor Red
}

# --- RUN 2 ---
Write-Host ""
Write-Host "=== RUN 2: Re-deploy (should skip everything - idempotent) ===" -ForegroundColor Yellow
Write-Host ""
dotnet run --project "$ProjectRoot\src\Perch.Cli" -- deploy --config-path $ConfigRepo
$exitCode = $LASTEXITCODE
Write-Host ""
Write-Host "Exit code: $exitCode"

# --- Summary ---
Write-Host ""
Write-Host "=== Directory listing ===" -ForegroundColor Yellow
Get-ChildItem $TargetsDir -Recurse -Force | ForEach-Object {
    $isLink = $_.Attributes -band [System.IO.FileAttributes]::ReparsePoint
    $suffix = if ($isLink) { " [SYMLINK]" } else { "" }
    Write-Host "  $($_.FullName)$suffix"
}

Write-Host ""
Write-Host "=== Done ===" -ForegroundColor Cyan
Write-Host "Inspect results at: $TargetsDir"
Write-Host "To clean up: Remove-Item '$TargetsDir' -Recurse -Force"

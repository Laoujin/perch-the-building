# Perch

Cross-platform dotfiles and application settings manager. Symlink-first -- change a setting in your editor, it's immediately tracked in git.

Perch manages symlinks, registry entries, VS Code extensions, PowerShell modules, global npm packages, and system package installation across Windows, Linux, and macOS.

## Quick Start

```bash
# Prerequisites: .NET 10 SDK

# Build
dotnet build

# Run directly
dotnet run --project src/Perch.Cli -- deploy --config-path /path/to/your/config-repo

# Or install as a global tool
dotnet pack src/Perch.Cli -c Release
dotnet tool install --global --add-source src/Perch.Cli/bin/Release Perch.Cli
perch deploy --config-path /path/to/your/config-repo
```

After the first run, the config path is saved so subsequent commands don't need `--config-path`.

## Commands

### `perch deploy`

Creates symlinks, sets registry values, installs packages, extensions, and modules.

```
perch deploy [--config-path <path>] [--dry-run] [--interactive] [--output Pretty|Json]
```

| Flag | Description |
|------|-------------|
| `--config-path` | Path to config repo (saved for future runs) |
| `--dry-run` | Preview what would happen without making changes |
| `--interactive`, `-i` | Prompt before each module (apply / skip / abort) |
| `--output` | `Pretty` (default, live table) or `Json` |

Before deploying, Perch creates a snapshot of all target files so you can restore them later.

### `perch status`

Shows drift between your config repo and the current system state.

```
perch status [--config-path <path>] [--drift-only] [--output Pretty|Json]
```

| Flag | Description |
|------|-------------|
| `--drift-only` | Only show items that are missing, drifted, or errored |
| `--output` | `Pretty` (default, grouped by category) or `Json` |

Checks: symlinks, registry entries, global packages, VS Code extensions, PowerShell modules, and system packages.

### `perch apps`

Shows installed applications and whether they match a known module.

### `perch restore list` / `perch restore apply`

List and restore from pre-deploy snapshots.

### `perch diff start` / `perch diff stop`

Snapshot the filesystem to detect changes made outside of Perch.

### `perch git setup`

Register git clean filters defined in module manifests.

### `perch completion`

Output a shell tab-completion script.

## How It Works

Perch reads a **config repo** -- a folder of modules, each with a `manifest.yaml` and the actual config files. On deploy, it symlinks those files to their expected locations on the system. Edit a config file in its usual location and the change is immediately reflected in the repo.

```
your-config-repo/
  git/
    manifest.yaml
    .gitconfig
    .gitignore_global
  vscode/
    manifest.yaml
    settings.json
    keybindings.json
    snippets/
  powershell/
    manifest.yaml
    profiles/
    scripts/
  packages.yaml              # System packages to install
  machines/                   # Optional per-machine overrides
    base.yaml
    DESKTOP-ABC.yaml
```

### Module Discovery

Perch scans `*/manifest.yaml` one level deep in the config repo. Each subfolder with a `manifest.yaml` is a module.

### Manifest Format

```yaml
display-name: Git
enabled: true                   # Optional, default true
platforms:                      # Optional, runs on all if omitted
  - Windows
  - Linux
  - MacOS

links:
  - source: .gitconfig          # Relative to module folder
    target:                     # Platform-specific targets
      windows: "%USERPROFILE%\\.gitconfig"
      linux: "$HOME/.gitconfig"
      macos: "$HOME/.gitconfig"
  - source: scripts
    target: "%USERPROFILE%\\scripts"
    link-type: junction         # 'symlink' (default) or 'junction'

registry:                       # Windows only
  - key: HKEY_CURRENT_USER\Software\App
    name: SettingName
    value: 42
    type: dword                 # dword, string, etc.

global-packages:
  manager: npm                  # npm or bun
  packages:
    - prettier
    - eslint_d

vscode-extensions:
  - dbaeumer.vscode-eslint
  - esbenp.prettier-vscode

ps-modules:
  - Posh-Git
  - PSReadLine

hooks:
  pre-deploy: setup.ps1         # Run before deploying this module
  post-deploy: cleanup.ps1      # Run after deploying this module
```

**Target paths** support environment variables: `%USERPROFILE%`, `%APPDATA%`, `%LOCALAPPDATA%`, `$HOME`, `$XDG_CONFIG_HOME`, etc. Custom variables can be defined in machine profiles.

### System Packages (`packages.yaml`)

Declare system-level packages in a `packages.yaml` at the config repo root:

```yaml
packages:
  - name: Git.Git
    manager: winget
  - name: Mozilla.Firefox
    manager: winget
  - name: curl
    manager: apt
  - name: git
    manager: brew
```

Supported managers: `winget`, `chocolatey`, `apt`, `brew`. Each is only applied on its matching platform.

On Windows, `winget` is recommended over `chocolatey` -- winget detects all installed software (including apps installed via choco, MSI, or manually) through the Windows ARP registry.

### Machine Profiles

Place YAML files in a `machines/` folder to customize per-machine behavior:

- `machines/base.yaml` -- defaults for all machines
- `machines/HOSTNAME.yaml` -- overrides for a specific machine (matched by `$env:COMPUTERNAME`)

```yaml
include-modules:          # Only deploy these modules (whitelist)
  - git
  - vscode
exclude-modules:          # Skip these modules (blacklist)
  - cmder
variables:                # Custom variables for target path expansion
  PROJECTS: "D:\\Projects"
```

Hostname-specific values override base values. Variables are merged (hostname wins on conflict).

## Project Structure

```
src/
  Perch.Core/       # Engine: symlinks, registry, packages, status, deploy
  Perch.Cli/        # CLI commands (Spectre.Console)
  Perch.Desktop/    # Desktop GUI (first-run wizard)
tests/
  Perch.Core.Tests/ # Unit + integration tests
```

## Development

```bash
dotnet build     # Build (warnings as errors)
dotnet test      # Run all tests
```

.NET 10, C# latest, NUnit 4, NSubstitute 5, Roslynator analyzers.

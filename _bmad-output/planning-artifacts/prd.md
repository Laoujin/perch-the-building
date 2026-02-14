---
stepsCompleted: [step-01-init, step-02-discovery, step-03-success, step-04-journeys, step-05-domain, step-06-innovation, step-07-project-type, step-08-scoping, step-09-functional, step-10-nonfunctional, step-11-polish]
inputDocuments: ['_bmad-output/brainstorming/brainstorming-session-2026-02-08.md']
workflowType: 'prd'
documentCounts:
  briefs: 0
  research: 0
  brainstorming: 1
  projectDocs: 0
  projectContext: 0
classification:
  projectType: cli_tool
  domain: developer_tooling
  complexity: low
  projectContext: brownfield
  scopes:
    scope1: "Switch machines - core symlink/junction engine, PowerShell profile, git config, program settings via symlinks"
    scope2: "Rock solid + cross-platform - idempotency, drift detection, WhatIf, package management, git clean filters, discovery, automated testing, Linux/macOS support"
    scope3: "Accessible & complete - registry management (Windows), machine-specific overrides, MAUI UI, secrets/1Password, community tools, competitor migration"
---

# Product Requirements Document - Perch

**Author:** Wouter
**Date:** 2026-02-14

## Executive Summary

**Perch** is a cross-platform dotfiles and application settings manager built in C# / .NET 10. It uses symlinks to link config files from a git-tracked repository into their expected locations on the filesystem, enabling zero-friction config sync across multiple machines running Windows, Linux, and macOS.

**Core differentiator:** Symlink-first philosophy. Change a setting in any app, and it's immediately visible in git — no re-add step, no re-run. Perch thinks in *applications* (manifests, modules, conventions), not just files.

**Target user:** Developer managing personal dotfiles and program settings across multiple machines (Windows, Linux, macOS). Future: any user via MAUI UI.

**Technology:** C# / .NET 10, Spectre.Console for CLI output, NUnit + NSubstitute for testing, GitHub Actions CI. Distributed as .NET tool (`dotnet tool install perch -g`). Engine library shared between CLI and future MAUI app.

**Competitive context:** Existing tools (chezmoi, PSDotFiles, Dotter, Dotbot) are Linux/macOS-first or use copy-on-apply models. None combine symlink-first + app-level awareness + cross-platform with Windows-native features (registry, MAUI). See `competitive-research.md` and `chezmoi-comparison.md` for detailed analysis.

## Success Criteria

### User Success

- Run Perch on a fresh or existing machine — all managed configs symlinked into place, no verification step
- Re-run after adding a new module — only new symlinks created, existing ones untouched
- Ongoing workflow is pure git: change a setting, commit, push, pull. No Perch re-run needed
- Zero maintenance after setup — symlinks persist, git handles sync
- Cross-platform modules (git, VS Code, PowerShell) work on Windows, Linux, and macOS from the same config repo

### Business Success

- **Scope 1:** Switch to new Windows PC (already here, apps installed) using Perch. Immediate priority
- **Scope 2:** Engine robust, tested, CI-green. Cross-platform support for Linux/macOS. Confidence to run on any machine without fear
- **Scope 3:** New users onboard via UI with minimal friction. Registry management (Windows), secrets integration, and machine-specific overrides work across all machines

### Technical Success

- C# / .NET 10 core engine shared between CLI and future MAUI UI
- NUnit + NSubstitute test suite covering core engine logic
- GitHub Actions CI on Windows and Linux runners
- Engine/config separation clean — no personal config in engine repo
- Platform-specific logic isolated — core engine is OS-agnostic

### Measurable Outcomes

- Scope 1: new Windows PC fully configured via Perch in a single session
- Scope 2: `perch deploy` idempotent — running twice produces zero changes
- Scope 2: CI pipeline green on every push (Windows + Linux)
- Scope 2: same config repo deploys correctly on both Windows and Linux
- Scope 3: machine-specific overrides work across 2-4 machines with shared base config
- Scope 3: non-author user can onboard without reading source code

## User Journeys

### Journey 1: Fresh Machine Setup (Scope 1)

Wouter's new PC has been sitting there for days. Apps are installed via Boxstarter, but every tool opens with factory defaults. He clones perch-config, clones the perch engine next to it, and runs `perch deploy`. PowerShell profile — linked. Git config — linked. VS Code settings, Windows Terminal config, Greenshot preferences — all symlinked from the repo into their expected locations. He opens PowerShell and it's *his* PowerShell. He opens his editor and his keybindings are there. The new machine feels like his machine. He commits from the new PC for the first time and pushes — the old machine is now the secondary one.

**Capabilities revealed:** Manifest discovery, symlink/junction creation, deploy command, engine/config repo coordination.

### Journey 2: Onboarding a New Program (Scope 1-2)

Wouter installs a new tool — say, a new terminal emulator. He likes it, tweaks the settings, and decides it's worth managing. He creates a folder in perch-config named after the package, adds a `manifest.json` pointing to where the settings file lives, copies the settings file into the module folder. Runs `perch deploy` — only the new symlinks are created, everything else untouched. He commits and pushes. On his other machine he pulls, runs `perch deploy`, and the new app's settings appear.

**Capabilities revealed:** Re-runnable deploy (additive only), manifest format, convention-over-config folder structure.

### Journey 3: Day-to-Day Config Sync (Scope 1)

Wouter changes a keybinding in his editor on his desktop. The settings file is a symlink into the perch-config repo, so `git diff` shows the change immediately. He commits, pushes. Later on his laptop, he pulls. The symlink already points to the same repo file — the change is just there. Perch was never involved.

**Capabilities revealed:** Symlink persistence, git-native workflow, zero Perch re-runs for setting changes.

### Journey 4: AI-Assisted App Discovery (Scope 3)

Wouter wants to onboard a complex app where he's not sure where the settings live. He launches the onboarding tool (CLI or MAUI). The tool can work two ways: if the app is already installed, it scans the system; if not, it spins up a Windows Sandbox, installs the app there. Either way, an AI lookup finds the known config locations online. The tool cross-references that against the actual filesystem — "found `settings.json` at `%AppData%\ToolName\config\`." Wouter pokes around in the MAUI UI to verify, maybe discovers additional files the AI missed. The tool generates the manifest, including version-specific paths if needed (e.g., v3.x stores config here, v4.x stores it there). He reviews, approves, and the module is ready.

**Capabilities revealed:** AI config path lookup, Windows Sandbox integration, version-range aware manifests, MAUI interactive explorer, CLI fallback for the workflow.

### Journey 5: Cross-Platform Sync (Scope 2)

Wouter sets up a Linux dev server. He installs .NET 10, runs `dotnet tool install perch -g`, clones his perch-config repo, and runs `perch deploy`. Git config — linked (same `~/.gitconfig` path). PowerShell profile — linked to `~/.config/powershell/`. VS Code settings — linked to `~/.config/Code/User/`. Windows-only modules (Windows Terminal, Greenshot, registry) are skipped automatically. His Linux environment has the same editor keybindings, same git aliases, same shell customizations. One config repo, multiple platforms.

**Capabilities revealed:** Platform-aware manifests, cross-platform path resolution, platform-specific module filtering.

### Journey Requirements Summary

| Capability | J1 | J2 | J3 | J4 | J5 | Scope |
|---|---|---|---|---|---|---|
| Symlink creation engine | x | x | | | x | 1 |
| Manifest discovery (convention-over-config) | x | x | | | x | 1 |
| Deploy command | x | x | | | x | 1 |
| Engine/config repo split | x | x | x | | x | 1 |
| Re-runnable, additive deploy | | x | | | | 1 |
| Platform-aware manifest paths | | | | | x | 2 |
| Platform-specific module filtering | | | | | x | 2 |
| Cross-platform path resolution | | | | | x | 2 |
| Version-range manifest paths | | | | x | | 3 |
| AI config path lookup | | | | x | | 3 |
| Windows Sandbox integration | | | | x | | 3 |
| MAUI interactive explorer | | | | x | | 3 |
| CLI onboarding fallback | | | | x | | 3 |

## Domain-Specific Requirements

### Filesystem Constraints

- **Dynamic config paths:** Some apps (e.g., Visual Studio Community 2019+) store settings in paths containing random/hash strings. Manifest format must support pattern-based or glob-style path resolution [Scope 2]
- **Special folder path variables:** Support platform-appropriate variables out of the box. Windows: `%AppData%\Roaming`, `%AppData%\Local`, `%UserProfile%`, etc. Linux/macOS: `$HOME`, `$XDG_CONFIG_HOME`, `~/.config`, etc. Extensible on a need basis [Scope 1 Windows, Scope 2 cross-platform]
- **Short root path recommendation (Windows):** Document that perch-config should be cloned near the filesystem root (e.g., `C:\tools\dotfiles`) to mitigate long path issues

### Cross-Platform Path Resolution

- **Platform-aware manifests:** Manifests specify target paths per platform. A single module can define different target paths for Windows, Linux, and macOS [Scope 2]
- **Git config:** `~/.gitconfig` on all platforms — identical path, no platform branching needed
- **PowerShell profiles:** Windows: `$HOME\Documents\PowerShell\Microsoft.PowerShell_profile.ps1`. Linux/macOS: `~/.config/powershell/Microsoft.PowerShell_profile.ps1`. Manifest handles the difference
- **Platform-specific gitconfig:** Handled via `includeIf` with `.windows.gitconfig` / `.linux.gitconfig` — Perch doesn't manage this

### Git on Windows

- **Git identity bootstrap:** Username/email setup and initial `.gitconfig` copy is manual. Scope 1-2: document in git-config module. Scope 3: consider automating
- **Symlink edge cases in third-party tools:** Some tools have historically had issues with symlinks. Outside Perch's control — document as known limitation

### App Config Handling

- **Symlink conflict resolution [Scope 1]:** If a target file already exists as a regular file, move it to `<filename>.backup` and create the symlink. Backup files are available for future restore
- **File locking detection [Scope 2]:** If a config file is locked by a running app during deploy, detect and report it. At end of run, offer choice: close programs and retry, or skip
- **Sync discipline:** Don't have the target app open during sync. User behavior expectation, not technical enforcement

## CLI Tool Specific Requirements

### Command Structure

- **Primary command:** `perch deploy` — creates symlinks for all managed configs
- **Default mode:** Non-interactive, streams actions in real-time. User can `Ctrl+C` to abort
- **Interactive mode [Scope 3]:** Step-level and command-level confirmation prompts
- **CI mode:** No color, no live rendering, porcelain output only

### Output & Console UI

- **Scope 1:** Spectre.Console colored text streaming — action-by-action output with status indicators (success/skip/fail/backup)
- **Scope 2:** Live-updating summary table showing progress per category (e.g., "Settings linked: 5/15") alongside action streaming. Porcelain mode: structured C# result objects serialized to JSON. Output mode via flag (`--output pretty|json`)

### Config Schema

- **App manifests:** Co-located in the config repo alongside config files. Minimum required fields: source path(s), target path(s) per platform, link type (symlink vs junction). Platform filter (optional — which OSes this module applies to)
- **Future [Scope 3]:** Manifests become templates from a separate repository, hosted via GitHub Pages as a public gallery/registry

### Backup & Restore

- **Conflict backup [Scope 1]:** Existing files moved to `.backup` when creating symlinks
- **Pre-deploy backup snapshots [Scope 2]:** Full backup of all target files before deploy
- **Restore from backup [Scope 3]:** Reverse a deploy by restoring backed-up files

### Scripting & Automation

- Clean exit codes (0 = success, non-zero = specific failure types)
- No prompts in default mode — CI-safe
- Porcelain output for piping [Scope 2]
- Re-runnable safely — additive only, never destructive without explicit flag

## Project Scoping & Phased Development

### MVP Strategy

**Approach:** Problem-solving MVP — the minimum to get Wouter off the old machine and onto the new one. Every feature must serve the "clone, deploy, switch" story.

**Resource:** Solo developer + AI assistance. C# / .NET 10.

### Phase 1: Switch Machines (MVP) — Windows Only

**Journeys supported:** J1 (Fresh Machine Setup), J2 (Onboarding New Program), J3 (Day-to-Day Sync)

**Must-have:**
- Assess existing codebase from previous AI session before building
- Symlink/junction creation engine reading co-located manifests
- Convention-over-config discovery (folder name = package name)
- `perch deploy` command — creates all symlinks, re-runnable (additive)
- Symlink conflict: move existing file to `.backup`, create symlink
- Engine/config repo split
- Config repo location via CLI argument, persisted for future runs (settings file alongside engine)
- Spectre.Console colored text streaming (action-by-action output)
- Basic error reporting (what failed and why)
- Clean exit codes
- Run from source (`dotnet run`)
- perch-config README documenting the folder convention and getting started
- Architecture: no hard-coded Windows assumptions in core engine — platform-specific logic isolated

**Explicitly NOT in MVP:**
- Linux/macOS support
- `dotnet tool install` distribution
- Live-updating tables / rich progress UI
- Porcelain/JSON output
- Interactive mode
- Dry-run / WhatIf
- Full backup/restore (conflict `.backup` IS in MVP)
- Git clean filters
- App discovery tooling
- Shell completion

### Phase 2: Rock Solid + Cross-Platform

- Linux/macOS support — platform-aware path resolution, cross-platform modules
- Platform-specific module filtering (Windows-only modules skip on Linux, etc.)
- `dotnet tool install perch -g` distribution (works on all platforms)
- Multiple package manager support (chocolatey, winget, apt, brew, etc.)
- Rich Spectre.Console UI (live tables, progress tracking)
- Porcelain/JSON output mode
- Idempotent deploy with drift reporting
- Dry-run / WhatIf mode (`--dry-run`)
- Pre-deploy backup snapshots
- File locking detection + reporting
- Dynamic config path resolution (glob/pattern matching)
- Git clean filters for noisy configs
- Before/after diffing for settings discovery
- Installed app detection + missing config detection
- NUnit + NSubstitute test suite
- GitHub Actions CI on Windows + Linux runners
- Lifecycle hooks per plugin

### Phase 3: Accessible & Complete

- Interactive mode (step-level and command-level confirmation)
- Machine-specific overrides (layered config system)
- Registry management — Windows only (requires dedicated brainstorm)
- Secrets/password manager integration (1Password CLI) — template + inject model for files containing secrets (NuGet credentials, npm tokens, SSH config for Synology/GitHub, API keys). Non-symlinked generated files
- Manifest templates from external repo (GitHub Pages gallery)
- Version-range-aware manifest paths
- Restore from backup (full restore capability)
- Shell completion
- MAUI onboarding app (AI-assisted discovery, Windows Sandbox) — shares engine library
- MAUI drift dashboard — shares engine library
- Competitor migration tool (chezmoi → Perch, other popular formats)
- Community config path database
- Git identity bootstrap automation

### Risk Mitigation

**Technical:**
- *Existing repo split may not work* — previous AI session started engine/config split, current state unknown. Mitigation: assess before building on it, be prepared to restructure. Added as phase 1 must-have
- *Dynamic config paths* — complex glob/pattern matching needed. Mitigation: scope 1 uses static paths only, dynamic paths deferred to scope 2
- *Symlink permissions on Windows* — brainstorm flagged as paper tiger. Mitigation: verify on new machine early in scope 1
- *Cross-platform path differences* — each OS has different config conventions. Mitigation: platform-aware manifest format designed in scope 1 architecture (even though Linux support ships in scope 2)

**Resource:**
- Solo developer — if blocked, machine switch stalls. Mitigation: scope 1 deliberately minimal
- AI-written code needs review — Mitigation: strong test suite in scope 2

## Functional Requirements

### Manifest & Module Management

- **FR1:** User can define an app module by creating a named folder containing a manifest file and config files [Scope 1]
- **FR2:** System discovers all app modules automatically by scanning for manifest files in the config repo (no central registration) [Scope 1]
- **FR3:** User can specify in a manifest where config files should be symlinked to, using platform-appropriate path variables (`%AppData%`, `$HOME`, `$XDG_CONFIG_HOME`, etc.) [Scope 1]
- **FR4:** System resolves pattern-based/glob config paths for apps with dynamic settings locations [Scope 2]
- **FR5:** User can specify version-range-aware symlink paths in a manifest [Scope 3]
- **FR6:** User can pull manifest templates from an external repository/gallery [Scope 3]
- **FR41:** Manifests support platform-aware target paths — different target locations per OS from a single module [Scope 2]
- **FR42:** Modules can be marked as platform-specific — only processed on matching OS [Scope 2]

### Symlink Engine

- **FR7:** System creates symlinks (and junctions on Windows) from config repo files to target locations on the filesystem. If a target file already exists, it is moved to `.backup` before creating the symlink [Scope 1]
- **FR8:** System re-runs deploy without affecting existing symlinks — only modules without an existing symlink are processed [Scope 1]
- **FR9:** System detects locked target files and reports them [Scope 2]
- **FR10:** System detects drift between expected and actual symlink state [Scope 2]
- **FR11:** System performs dry-run showing what would change without modifying the filesystem [Scope 2]
- **FR12:** System creates full pre-deploy backup snapshots of all target files [Scope 2]
- **FR13:** User can restore files from a backup (conflict `.backup` or full snapshot) [Scope 3]

### CLI Interface

- **FR14:** User can run a deploy command that processes all discovered modules [Scope 1]
- **FR15:** System streams each action to the console in real-time with colored status indicators [Scope 1]
- **FR16:** System returns clean exit codes indicating success or specific failure types [Scope 1]
- **FR17:** User can abort execution mid-deploy via Ctrl+C (graceful shutdown via CancellationToken) [Scope 1]
- **FR18:** System outputs structured JSON results for machine consumption [Scope 2]
- **FR19:** System displays a live-updating progress table alongside action streaming [Scope 2]
- **FR20:** User can run deploy in interactive mode with step-level and command-level confirmation [Scope 3]
- **FR21:** User can tab-complete Perch commands in the shell [Scope 3]

### Package Management

- **FR22:** User can define all managed packages in a single manifest file, supporting multiple package managers (chocolatey, winget, apt, brew, etc.) with per-package manager specification [Scope 2]
- **FR23:** System detects installed apps and cross-references against managed modules [Scope 2]
- **FR24:** System reports apps installed but without a config module [Scope 2]

### Git Integration

- **FR25:** System registers per-app git clean filters to suppress noisy config diffs [Scope 2]
- **FR26:** System performs before/after filesystem diffing to discover config file changes [Scope 2]

### App Discovery & Onboarding

- **FR27:** User can scan the system for installed apps and see which have config modules [Scope 2]
- **FR28:** System looks up known config file locations for popular apps via AI [Scope 3]
- **FR29:** System launches an app in Windows Sandbox to discover its config locations [Scope 3]
- **FR30:** User can generate a new module manifest via interactive onboarding workflow (CLI or MAUI) [Scope 3]

### Machine Configuration

- **FR31:** User can define base config values with per-machine overrides [Scope 3]
- **FR32:** User can specify which modules apply to which machines [Scope 3]
- **FR33:** User can manage Windows registry settings declaratively [Scope 3]
- **FR34:** System applies and reports on registry state (context menus, default programs, power settings, etc.) [Scope 3]

### Secrets Management

- **FR43:** System can inject secrets from a password manager (1Password CLI) into config files at deploy time, producing generated (non-symlinked) files [Scope 3]
- **FR44:** User can define secret placeholders in config templates that are resolved from 1Password during deploy [Scope 3]
- **FR45:** System manages secret-containing configs: NuGet registry credentials, npm tokens, SSH config (Synology, GitHub), API keys, and similar [Scope 3]

### MAUI UI

- **FR35:** User can view sync status of all managed modules in a visual dashboard [Scope 3]
- **FR36:** User can interactively explore an app's filesystem to find config locations [Scope 3]
- **FR37:** User can generate and edit module manifests via a visual interface [Scope 3]

### Plugin Lifecycle

- **FR38:** User can define pre-deploy and post-deploy hooks per module [Scope 2]

### Engine Configuration

- **FR39:** User can specify the config repo location as a CLI argument [Scope 1]
- **FR40:** System persists the config repo location (settings file alongside engine) so it doesn't need to be specified on subsequent runs [Scope 1]

### Migration & Compatibility

- **FR46:** System can import/convert a chezmoi-managed dotfiles repo into Perch format [Scope 3]
- **FR47:** System can import/convert other popular dotfiles formats (Dotbot, Dotter, etc.) into Perch format [Scope 3]

## Non-Functional Requirements

### Reliability

- Deploy safe to interrupt (`Ctrl+C`) at any point — partially completed deploys are valid
- Failed symlink operations for one module must not prevent other modules from processing
- Missing target directories handled gracefully (report, don't crash)

### Maintainability

- Codebase understandable for AI-assisted development — clear separation of concerns, well-named types
- Platform-specific logic isolated behind abstractions — core engine is OS-agnostic
- NUnit test coverage on core engine logic: symlink creation, manifest parsing, module discovery [Scope 2]
- GitHub Actions CI ensures no regressions on push (Windows + Linux runners) [Scope 2]

### Portability

- Scope 1: runs on Windows 10+ with .NET 10 runtime
- Scope 2: runs on Windows 10+, Linux (major distros), and macOS with .NET 10 runtime
- No dependency on specific shell (PowerShell, cmd, bash, zsh all work)
- Config repo format: plain files + JSON manifests — no binary formats, no database, no proprietary encoding
- `dotnet tool install perch -g` works on all supported platforms [Scope 2]

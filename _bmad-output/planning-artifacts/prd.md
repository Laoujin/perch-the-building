---
stepsCompleted: [step-01-init, step-02-discovery, step-03-success, step-04-journeys, step-05-domain]
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
    scope2: "Rock solid - smart bootstrap, idempotency, drift detection, WhatIf, package management, git clean filters, discovery, automated testing"
    scope3: "Accessible & complete - registry management, machine-specific overrides, MAUI UI, community tools"
---

# Product Requirements Document - Perch

**Author:** Wouter
**Date:** 2026-02-14

## Success Criteria

### User Success
- Run Perch on a fresh (or existing) Windows machine and all managed configs are symlinked into place — no verification step
- Re-run Perch after adding a new program module and only the new symlinks are created, existing ones left alone
- Ongoing workflow is pure git: change a setting, commit, push, pull on other machines. No Perch re-run needed for setting changes
- Zero maintenance after setup — symlinks are persistent, git handles sync

### Business Success
- **Scope 1:** Wouter switches to new PC (already here, apps installed) using Perch. Immediate priority
- **Scope 2:** Engine is robust, tested, CI-green. Confidence to run on any machine without fear
- **Scope 3:** New users onboard with minimal friction via UI. Registry management tames Windows chaos. Machine-specific overrides make multi-machine setups painless

### Technical Success
- C# / .NET core engine — shared library between CLI and future MAUI UI
- Distributed as .NET tool (`dotnet tool install perch -g`)
- NUnit + NSubstitute test suite covering core engine logic
- GitHub Actions CI on Windows runners
- Engine/config separation is clean — no personal config in engine repo

### Measurable Outcomes
- Scope 1: new PC fully configured via Perch in a single session
- Scope 2: `perch deploy` is idempotent — running twice produces zero changes on second run
- Scope 2: CI pipeline green on every push
- Scope 3: machine-specific overrides work across 2-4 machines with shared base config
- Scope 3: a non-author user can onboard without reading source code

## Product Scope

### MVP — Scope 1: Switch Machines
- Core symlink/junction engine that reads co-located manifests
- Convention-over-config discovery (folder name = package name)
- Deploy command that links all managed configs into place
- Re-runnable: adding a new app module and running again creates only the new symlinks
- Manages: PowerShell profile, git config, program settings (JSON/YAML files via symlinks)
- Engine/config repo split (already started)
- Built in C# / .NET, distributed via `dotnet tool install perch -g`

### Growth — Scope 2: Rock Solid
- Idempotent bootstrap with drift reporting
- Dry-run / WhatIf mode
- Pre-deploy backup snapshots
- Package manifest (unified, replaces chocolatey.txt + boxstarter gist)
- Git clean filters for noisy configs
- Before/after diffing for settings discovery
- Installed app detection + missing config detection
- NUnit + NSubstitute test suite + GitHub Actions CI
- Lifecycle hooks per plugin

### Vision — Scope 3: Accessible & Complete
- **Registry management** — context menus, default programs, power settings, mouse settings, and more. Requires dedicated brainstorm session to map the full space
- **Machine-specific overrides** — layered config system: base defaults with per-machine overrides for both settings values and which programs are managed
- MAUI onboarding app (scan installed software, generate manifests) — shares core engine library with CLI
- MAUI drift dashboard (sync status, one-click fixes) — shares core engine library with CLI
- Interactive scaffold wizard for adding new apps
- Community config path database

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

### Journey Requirements Summary

| Capability | J1 | J2 | J3 | J4 | Scope |
|---|---|---|---|---|---|
| Symlink/junction creation engine | x | x | | | 1 |
| Manifest discovery (convention-over-config) | x | x | | | 1 |
| Deploy command | x | x | | | 1 |
| Engine/config repo split | x | x | x | | 1 |
| Re-runnable, additive deploy | | x | | | 1 |
| Manifest format with version-range paths | | | | x | 3 |
| AI config path lookup | | | | x | 3 |
| Windows Sandbox integration | | | | x | 3 |
| MAUI interactive explorer | | | | x | 3 |
| CLI onboarding fallback | | | | x | 3 |

## Domain-Specific Requirements

### Windows Filesystem Constraints

- **Dynamic config paths:** Some apps (e.g., Visual Studio Community 2019+) store settings in paths containing random/hash strings. The manifest format must support pattern-based or glob-style path resolution, not just static paths
- **Special folder path variables:** Support common ones out of the box (`%AppData%\Roaming`, `%AppData%\Local`, `%UserProfile%`, etc.). Additional special folders added on a need basis as new apps require them — the system should be extensible, not exhaustive upfront
- **Short root path recommendation:** Document that perch-config should be cloned near the filesystem root (e.g., `C:\tools\dotfiles`) to mitigate long path issues

### Git on Windows

- **Platform-specific gitconfig:** Already handled via `includeIf` with `.windows.gitconfig` / `.linux.gitconfig` — Perch doesn't need to manage this
- **Git identity bootstrap:** Username/email setup and initial `.gitconfig` copy to user folder is manual today. For scope 1-2: document the process in the git-config module. For scope 3: consider automating this as part of deploy
- **Symlink edge cases in third-party tools:** Some tools have historically had issues with symlinks (e.g., older Angular/Node dependencies). This is outside Perch's control — document as a known limitation, not a bug

### App Config Handling

- **File locking detection (Scope 2):** If a config file is locked by a running app during deploy, detect it and report it. At the end of the deploy run, offer the user a choice: close those programs and retry, or skip and handle manually
- **App config rewriting:** Not observed as a real issue with symlinks — no special handling needed
- **Sync discipline:** Don't have the target app open during sync. This is a user behavior expectation, not a technical enforcement


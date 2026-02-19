---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
lastStep: 8
status: 'complete'
completedAt: '2026-02-16'
inputDocuments:
  - '_bmad-output/planning-artifacts/prd.md'
  - '_bmad-output/planning-artifacts/prd-validation-report.md'
  - '_bmad-output/planning-artifacts/competitive-research.md'
  - '_bmad-output/planning-artifacts/chezmoi-comparison.md'
  - '_bmad-output/planning-artifacts/ux-design-specification.md'
  - '_bmad-output/brainstorming/brainstorming-session-2026-02-08.md'
  - '_bmad-output/brainstorming/brainstorming-session-2026-02-14.md'
  - '_bmad-output/brainstorming/brainstorming-session-2026-02-17.md'
workflowType: 'architecture'
project_name: 'Perch'
user_name: 'Wouter'
date: '2026-02-16'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**

75 FRs across 15 categories. Scope 1 (MVP) FRs are well-defined and tightly scoped:

- **Manifest & Module Management** (FR1-FR3, FR41-FR42): Convention-over-config discovery, co-located manifests with platform-aware target paths
- **Symlink Engine** (FR7-FR8): Create symlinks/junctions, backup existing files, re-runnable (additive only)
- **CLI Interface** (FR14-FR17): Deploy command, colored streaming output, clean exit codes, graceful Ctrl+C
- **Engine Configuration** (FR39-FR40): Config repo location via CLI argument, persisted for subsequent runs

Scope 2-3 FRs add surface area (cross-platform, system tweaks, gallery schema, import tooling, secrets, desktop UI, package management, lifecycle hooks) but don't change the core architectural shape — they extend it.

- **Machine Configuration** (FR31-FR34, FR59-FR62): Multi-mechanism system tweaks (registry + PowerShell + fonts), three-value model (default/captured/desired), app-owned tweaks, detect-first flow
- **Gallery Schema & Taxonomy** (FR63-FR69): Gallery as source of truth, unified tree taxonomy, dependency links, OS version awareness, auto-generated index
- **Import & Sourcing** (FR70-FR73): WinUtil/Sophia import tooling, deduplication, license checks, CI registry validation
- **Desktop UI** (FR35-FR37, FR49-FR53, FR74-FR75): WPF desktop app — wizard onboarding, drift dashboard with mechanism-aware smart status cards, unified tree browser, inline value display, Open Location deep links

**Non-Functional Requirements:**

- **Reliability:** Graceful shutdown (current module completes on Ctrl+C), fault isolation (one module failure doesn't block others), missing directories reported and skipped
- **Maintainability:** Platform-specific logic behind interfaces, core engine depends only on abstractions. TDD, KISS/YAGNI
- **Portability:** .NET 10 runtime, Windows 10+ (Scope 1), cross-platform (Scope 2), shell-agnostic, plain files + JSON manifests

**Scale & Complexity:**

- Primary domain: WPF Desktop app (wizard, dashboard, card-based gallery UI) + CLI tool (automation, cross-platform). Desktop is the primary interface
- Complexity level: Medium (Desktop), Low (Core + CLI)
- Architectural components: ~16 feature folders in Core, ~10 ViewModels + 6 pages + 7 controls in Desktop, 14 CLI commands. All implemented and working

### Technical Constraints & Dependencies

- **C# / .NET 10** — chosen stack, distributed as `dotnet tool`
- **Spectre.Console** — CLI output rendering
- **NUnit + NSubstitute** — testing (Scope 2 CI, but TDD from Scope 1)
- **Engine/config repo split** — engine is open-sourceable, config is personal. Engine locates config via CLI arg or persisted setting
- **No hard-coded Windows assumptions in core engine** — even in Scope 1 Windows-only MVP, platform logic goes behind interfaces
- **Symlink-first philosophy** — core differentiator. Architecture must never compromise the "change a setting, it's immediately in git" workflow
- **Manifest format: YAML** — co-located with config files, minimum fields: source path(s), target path(s) per platform, link type
- **WPF UI (lepoco/wpfui)** — Fluent Design component library for desktop app
- **HandyControl** — StepBar component for wizard step indicator (fills WPF UI gap)
- **CommunityToolkit.Mvvm** — MVVM framework (ObservableObject, [ObservableProperty], [RelayCommand])

### Cross-Cutting Concerns Identified

- **Platform abstraction:** Core engine depends on interfaces (e.g., `IFileSystemOperations`, `ISymlinkProvider`). Windows implementation in Scope 1, Linux/macOS in Scope 2. Platform-specific classes tested with real filesystem; everything else mocked
- **Error handling & fault isolation:** Per-module independence. Failed modules logged + displayed, others continue. No partial state on cancellation
- **Idempotency:** Every operation checks current state before acting. Re-run produces zero changes if nothing changed
- **Testability:** Interface-based design enables NSubstitute mocking. Real filesystem tests only for platform implementations
- **Graceful cancellation:** CancellationToken threaded through engine operations. Current module completes fully before halting

## Starter Template Evaluation

### Primary Technology Domain

WPF Desktop app + CLI tool — C# / .NET 10. Desktop is the primary interface; CLI for automation and cross-platform. No competing starter ecosystem; `dotnet new` provides all scaffolding.

### Verified Versions (Feb 2026)

| Package | Version | Notes |
|---|---|---|
| .NET 10 | GA (LTS) | Released Nov 2025, supported until Nov 2028 |
| Spectre.Console | 0.54.0 | CLI output rendering |
| Spectre.Console.Cli | 0.53.1 | Command/argument parsing |
| NUnit | 4.4.0 | NUnit 4 — modernized assertion model |
| NSubstitute | 5.3.0 | Interface mocking |
| WPF-UI | 4.2.0 | Fluent Design for WPF (NavigationView, Card, InfoBar, Snackbar) |
| HandyControl | 3.5.1 | StepBar for wizard step indicator |
| CommunityToolkit.Mvvm | 8.4.0 | MVVM source generators ([ObservableProperty], [RelayCommand]) |

### Solution Structure

```
Perch.sln
├── src/
│   ├── Perch.Core/          # Engine library (classlib) — all logic, interfaces, models
│   ├── Perch.Cli/           # Console app — Spectre.Console, references Core
│   └── Perch.Desktop/       # WPF app — WPF UI + HandyControl, references Core
└── tests/
    ├── Perch.Core.Tests/    # NUnit + NSubstitute
    └── Perch.Desktop.Tests/ # NUnit — ViewModel unit tests
```

**Perch.Core** (classlib): Manifest parsing, module discovery, symlink engine, platform interfaces + Windows implementation, configuration, backup. Zero UI/console dependencies.

**Perch.Cli** (console): Spectre.Console rendering, command definitions, DI wiring, references Perch.Core. Packed as `dotnet tool`.

**Perch.Desktop** (WPF app): WPF UI shell with NavigationView sidebar, wizard flow with HandyControl StepBar, drift dashboard, card-based views. CommunityToolkit.Mvvm for MVVM. References Perch.Core — same engine, different presentation. Windows-only.

**Perch.Core.Tests** (nunit): Unit tests against Core. Platform-specific code tested with real filesystem; everything else mocked via interfaces.

**Perch.Desktop.Tests** (nunit): ViewModel unit tests for Desktop. ViewModels are testable without WPF runtime — they depend on Core interfaces (mockable) and expose observable properties/commands.

**Why not more projects?** Platform implementations live in Core behind interfaces until Scope 2 adds Linux/macOS — extract then if needed. CLI and Desktop are both thin UI layers — logic lives in Core. No shared "Perch.UI.Common" project — the two UIs have completely different rendering stacks (Spectre.Console vs WPF/XAML) and share nothing at the view level.

### Initialization

```bash
# Core + CLI (Scope 1)
dotnet new sln -n Perch
dotnet new classlib -n Perch.Core -o src/Perch.Core -f net10.0
dotnet new console -n Perch.Cli -o src/Perch.Cli -f net10.0
dotnet new nunit -n Perch.Core.Tests -o tests/Perch.Core.Tests -f net10.0
dotnet sln add src/Perch.Core src/Perch.Cli tests/Perch.Core.Tests
dotnet add src/Perch.Cli reference src/Perch.Core
dotnet add tests/Perch.Core.Tests reference src/Perch.Core
dotnet add src/Perch.Cli package Spectre.Console --version 0.54.0
dotnet add src/Perch.Cli package Spectre.Console.Cli --version 0.53.1
dotnet add tests/Perch.Core.Tests package NSubstitute --version 5.3.0

# Desktop (when Desktop epic begins)
dotnet new wpf -n Perch.Desktop -o src/Perch.Desktop -f net10.0-windows
dotnet new nunit -n Perch.Desktop.Tests -o tests/Perch.Desktop.Tests -f net10.0
dotnet sln add src/Perch.Desktop tests/Perch.Desktop.Tests
dotnet add src/Perch.Desktop reference src/Perch.Core
dotnet add tests/Perch.Desktop.Tests reference src/Perch.Core
dotnet add src/Perch.Desktop package WPF-UI --version 4.2.0
dotnet add src/Perch.Desktop package HandyControl --version 3.5.1
dotnet add src/Perch.Desktop package CommunityToolkit.Mvvm --version 8.4.0
dotnet add src/Perch.Desktop package Microsoft.Extensions.Hosting --version 10.0.0
dotnet add tests/Perch.Desktop.Tests package NSubstitute --version 5.3.0
```

**Note:** All projects (Core, CLI, Desktop, tests) are initialized and working. The commands above are retained for reference only.

## Core Architectural Decisions

### Decision Summary

| # | Decision | Choice | Rationale |
|---|----------|--------|-----------|
| 1 | Manifest format | YAML | Human-editable, audience isn't necessarily .NET devs |
| 2 | Engine pipeline | Discover → validate → execute → report with live Spectre output | Simple linear pipeline |
| 3 | Platform abstraction | Minimal — interface only for symlink/junction creation | .NET handles most cross-platform natively |
| 4 | Error handling | Result entries with Level (Info/Warn/Error) + message | Filterable via Spectre tables; extendable for porcelain |
| 5 | Dependency injection | Microsoft.Extensions.DependencyInjection | Standard .NET |
| 6 | Settings persistence | YAML in platform config dir (`%APPDATA%/perch/`, `~/.config/perch/`) | Consistent with manifest format, universal location |
| 7 | CLI commands (Scope 1) | `perch deploy [--config-path <path>]` | Minimal MVP command surface |
| 8 | Desktop UI framework | WPF (not Avalonia/MAUI) | Richer component library ecosystem, better designer tooling. CLI handles cross-platform |
| 9 | Desktop component library | WPF UI (primary) + HandyControl (StepBar) | Fluent 2 design, NavigationView/Card/InfoBar built-in. HandyControl fills StepBar gap |
| 10 | Desktop MVVM | CommunityToolkit.Mvvm | Source-generated [ObservableProperty]/[RelayCommand]. Proven in prior Avalonia prototype |
| 11 | Desktop app modes | Wizard (first-run) + Dashboard (ongoing) sharing reusable views | Two distinct experiences backed by same card-based UserControls and Core engine |
| 12 | Desktop DI host | Microsoft.Extensions.Hosting via Generic Host | Consistent with CLI DI approach, WPF UI docs recommend this pattern |
| 13 | System tweak mechanisms | Registry YAML + PowerShell script/undo_script + font install. .reg retired | Covers ~95% of tweaks. Services/tasks/GPO deferred — add when needed |
| 14 | Registry value model | Three-value: default_value (Windows default), captured (pre-Perch machine state), desired | Enables drift detection, revert-to-default, revert-to-previous without prior deploy |
| 15 | Gallery role | Source of truth for defaults. Manifest stores only deviations + captured old values | Minimal configs, clean diffs. Gallery YAML is canonical |
| 16 | Content taxonomy | Unified tree with deep category paths (e.g., `Apps/Languages/.NET/Editors/Visual Studio`) | One navigable tree for everything Perch manages — apps, tweaks, fonts, dotfiles |
| 17 | Gallery entry dependencies | `suggests:` (soft) and `requires:` (hard) links. Replaces `priority:` field | Execution order derives from dependency graph, not manual numbers |
| 18 | App-owned tweaks | App gallery entries own their bad behavior (context menus, startup, telemetry) as sub-items | No artificial separation between installing an app and cleaning up after it |
| 19 | Import strategy | One-time scripts per source (WinUtil, Sophia). Not a framework | Pragmatic — sources change rarely. Human review required anyway |
| 20 | Gallery primary UX | WPF tree browser is the primary discovery UI. Astro website is marketing only | Desktop app is where users interact with the catalog |

### Manifest Schema (YAML)

Co-located with config files. Folder name = package/app name. Additional dependency: YamlDotNet.

**Minimum fields (Scope 1):** source path(s), target path(s) per-platform with env var syntax, link type (symlink default, junction for Windows dirs). Exact schema defined during implementation.

### Engine Pipeline

```
perch deploy [--config-path <path>]
  1. Load settings (config repo path from args or persisted)
  2. Discover modules (scan for */manifest.yaml)
  3. Parse & validate manifests
  4. Per module: check state → backup if needed → create symlink → record result
  5. Report summary via Spectre.Console
```

Each module independent. CancellationToken checked between modules.

### Platform Abstraction

**Behind interface:** symlink creation, junction creation, symlink detection.

**NOT behind interface (System.IO):** file/dir operations, path resolution, env var expansion.

### Error Handling & Reporting

```csharp
public record DeployResult(string ModuleName, string SourcePath, string TargetPath,
    ResultLevel Level, string Message);

public enum ResultLevel { Info, Warning, Error }
```

Engine returns `IReadOnlyList<DeployResult>`. CLI renders via Spectre. No exceptions for expected failures — those become Warning/Error results.

### Dependency Injection

MS DI in Perch.Cli `Program.cs`. Core exposes `AddPerchCore()` extension method.

### Settings Persistence

`settings.yaml` at `%APPDATA%/perch/` (Windows) / `~/.config/perch/` (Linux/macOS). Scope 1: just `configRepoPath`.

### CLI Commands (Scope 1)

`perch deploy` with `--config-path <path>` (persisted after first use). Exit codes: 0 success, 1 partial failure, 2 fatal.

### Desktop UI Framework (WPF)

WPF over Avalonia/MAUI. The component library ecosystem for WPF (WPF UI, HandyControl, MaterialDesign) is richer and more mature than Avalonia's. Windows-only is acceptable — the CLI remains cross-platform. The prior Avalonia prototype lacked a component library and suffered for it.

### Desktop Component Strategy

**WPF UI (lepoco/wpfui)** provides the Fluent 2 / Windows 11 design language: `NavigationView` (sidebar), `Card`/`CardExpander` (app cards), `InfoBar` (status messages), `Snackbar` (transient feedback), `ProgressRing`, `ToggleSwitch`, `AutoSuggestBox`, `SymbolIcon`. Dark Fluent theme as the base aesthetic.

**HandyControl** adds `StepBar` for wizard step indicators — the one critical control WPF UI lacks.

**Custom components:** StatusRibbon, ProfileCard, AppCard, DriftHeroBanner, DeployBar, TierSectionHeader. All compose WPF UI primitives — no from-scratch controls.

### Desktop MVVM Pattern

CommunityToolkit.Mvvm with `ObservableObject` base, `[ObservableProperty]` for bindable properties, `[RelayCommand]` for commands. Pages implement `INavigableView<TViewModel>` for WPF UI navigation integration. ViewModels depend on Core interfaces (injectable, mockable) — never on WPF types.

### Desktop App Modes

**Wizard (first-run):** Dedicated view with HandyControl `StepBar` header, replacing sidebar. Steps: Profile Selection → Dotfiles → Apps → System Tweaks → Review & Deploy. Steps are dynamic based on profile selection — unselected categories are omitted, not skipped.

**Dashboard (ongoing):** WPF UI `NavigationView` sidebar: Home (drift hero + attention cards), Dotfiles, Apps, System Tweaks, Settings. Card gallery views (`DotfilesView`, `AppsView`, `SystemTweaksView`) are `UserControl`s shared between wizard steps and dashboard pages.

**Startup routing:** App checks for existing deploy state. First run → wizard. Returning user → dashboard. User can re-run wizard from Settings.

### Desktop DI Host

`Microsoft.Extensions.Hosting` Generic Host in `App.xaml.cs`. Registers:
- WPF UI services: `INavigationService`, `IPageService`, `IThemeService`, `ISnackbarService`, `IContentDialogService`
- Core services via `AddPerchCore()` — same extension method used by CLI
- Pages + ViewModels: Singleton for cached pages (Dashboard, Settings), Transient for pages recreated per navigation (wizard steps)
- `INavigationWindow` → `MainWindow`

### System Tweaks Engine (Scope 3)

Multi-mechanism tweak application and drift detection. Lives in `Perch.Core/Tweaks/`.

**Mechanism types:**

| Mechanism | Apply | Revert | Drift Detection |
|-----------|-------|--------|-----------------|
| Registry YAML | Write registry key/value | Restore captured or default value | Compare current vs desired |
| PowerShell script | Execute `script:` | Execute `undo_script:` | Re-run detection script or check expected state |
| Font install | Copy to Fonts dir + register, or `winget install` | Uninstall font | Check font registry key exists |

**Three-value model per registry entry:**

```yaml
registry:
  - key: HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced
    name: HideFileExt
    value: 0              # desired
    default_value: 1      # Windows default (baked into gallery)
    type: dword
```

Captured machine state (pre-Perch value) stored per-machine in `machines/<hostname>/captured-state.yaml` in the config repo. Populated on first deploy.

**Detect-first flow:**

```
1. Scan machine: read current registry values, installed fonts, app state
2. Match against gallery: "you already have these applied"
3. Show diff: what matches desired, what doesn't, what's unknown
4. User selects what to manage → deploy only the delta
```

**App-owned tweaks:** An app gallery entry can include a `tweaks:` section with toggleable sub-items:

```yaml
name: Visual Studio
category: Apps/Languages/.NET/Editors/Visual Studio
install:
  winget: Microsoft.VisualStudio.2022.Community
config:
  links:
    - source: settings/CurrentSettings.vssettings
      target:
        windows: "%LocalAppData%/Microsoft/VisualStudio/..."
tweaks:
  - name: Disable telemetry
    registry:
      - key: HKCU\Software\Microsoft\VisualStudio\Telemetry
        name: TurnOffSwitch
        value: 1
        default_value: 0
        type: dword
  - name: Remove context menu entry
    registry:
      - key: HKCR\Directory\Background\shell\AnyCode
        name: ""
        value: null          # delete key
        default_value: exist  # key exists by default
```

### Gallery Schema Evolution (Scope 3)

Gallery YAML entries gain new fields. Backwards-compatible — new fields are optional.

**New fields:**

| Field | Type | Purpose |
|-------|------|---------|
| `type` | `app \| tweak \| font` | Unified type discriminator |
| `windows_versions` | `[10, 11]` | OS-aware filtering. Desktop hides non-matching entries |
| `restart_required` | `bool` | Shown on card. No risk levels — just restart info |
| `sort` | `int` | Category display order in tree |
| `suggests` | `[entry-id, ...]` | Soft dependency — "you might also want" |
| `requires` | `[entry-id, ...]` | Hard dependency — must be applied first |
| `script` | `string` | PowerShell command to apply (non-registry tweaks) |
| `undo_script` | `string` | PowerShell command to revert |
| `default_value` | per registry entry | Windows default for three-value model |

**Removed fields:** `priority` (replaced by `requires:` dependency graph)

**Gallery as source of truth:** User manifests in perch-config reference gallery entries by ID and store only deviations:

```yaml
# perch-config module manifest (minimal)
gallery: show-file-extensions
# no other fields needed — gallery has all defaults

# or with deviation:
gallery: dark-mode
overrides:
  enabled: false  # I prefer light mode on this machine
```

**Unified tree taxonomy:** Category paths define the tree structure. Depth matches content density:

```
Apps/Browsers/Firefox
Apps/Languages/.NET/SDKs
Apps/Languages/.NET/Editors/Visual Studio
Apps/Languages/Node/nvm
Windows Tweaks/Explorer
Windows Tweaks/Context Menus
Windows Tweaks/Startup Items
Windows Tweaks/Fonts/Nerd Fonts
```

**Auto-generated index.yaml:** CI or build step scans `catalog/` and regenerates `index.yaml`. Never manually edited.

### Smart Status Model (Scope 3)

Mechanism-aware status for Desktop cards and CLI reporting:

| Type | Statuses |
|------|----------|
| Registry tweaks | Applied, Drifted, Not Applied, Reverted, Error |
| Apps | Installed, Not Installed, Update Available |
| Dotfiles/Config | Linked, Broken, Not Linked, Modified |
| Fonts | Installed, Not Installed |
| Cross-cutting | Skipped (excluded per machine), hidden (wrong OS) |

Status drives card visuals: color accent, icon, action buttons. "Open Location" button is mechanism-aware — opens regedit, Explorer, services.msc, or Fonts folder as appropriate.

### Import Pipeline Architecture (Scope 3)

One-time scripts, not a framework. Each source gets a standalone script:

```
tools/
├── import-winutil.ps1      # Parses config/tweaks.json → gallery YAML stubs
├── import-sophia.ps1       # Parses Sophia functions → gallery YAML stubs
└── dedup-gallery.ps1       # Compares entries, flags overlaps for review
```

**WinUtil importer flow:**
1. Clone/download WinUtil `config/tweaks.json`
2. Parse each tweak: extract registry, service, scheduledTask, InvokeScript/UndoScript
3. Map to gallery YAML format: registry entries → `registry:`, InvokeScript → `script:`, UndoScript → `undo_script:`
4. Map WinUtil categories to Perch tree paths
5. Set `default_value` from WinUtil's `OriginalValue`
6. Output YAML stubs to `catalog/tweaks/` for human review

**Sophia importer:** Runs after WinUtil. `dedup-gallery.ps1` compares new Sophia entries against existing gallery entries, flags overlaps showing both definitions side-by-side.

**Quality gates:** License check per source repo. Human visual testing for each imported tweak. CI validates registry paths exist on clean Windows runners.

## Implementation Patterns & Consistency Rules

### Naming Patterns

- **Interfaces:** `I<Noun>Provider` for abstractions over external resources (e.g., `ISymlinkProvider`, `IFileBackupProvider`). `I<Noun>Service` for orchestration (e.g., `IDeployService`, `IModuleDiscoveryService`)
- **YAML manifest properties:** `kebab-case` (e.g., `target-path`, `link-type`, `source-files`)
- **Namespaces:** Match folder structure under `Perch.Core` (e.g., `Perch.Core.Modules`, `Perch.Core.Symlinks`)
- **Desktop ViewModels:** `<Page>ViewModel` (e.g., `DashboardViewModel`, `WizardProfileViewModel`). Partial classes with CommunityToolkit.Mvvm source generators
- **Desktop Views:** `<Page>Page` for navigable pages (e.g., `DashboardPage`, `AppsPage`). `<Name>View` for reusable UserControls (e.g., `AppsView`, `DotfilesView`)
- **Desktop custom controls:** `<Name>` as UserControl (e.g., `StatusRibbon`, `AppCard`, `ProfileCard`)

### Project Organization

Feature folders in Perch.Core — related code together:

```
Perch.Core/
├── Modules/        # Manifest parsing, module discovery, Module model
├── Symlinks/       # ISymlinkProvider, WindowsSymlinkProvider, symlink logic
├── Deploy/         # DeployService, DeployResult, pipeline orchestration
├── Config/         # Settings loading/persistence, path resolution
├── Backup/         # File backup before symlink creation
├── Tweaks/         # ITweakProvider, RegistryTweakProvider, ScriptTweakProvider, FontTweakProvider (Scope 3)
├── Gallery/        # Gallery schema models, gallery loader, manifest-gallery merging (Scope 3)
└── Detection/      # Machine state scanning, detect-first flow, drift comparison (Scope 3)
```

Interfaces live in the same feature folder as their implementations.

### Desktop Project Organization

```
Perch.Desktop/
├── App.xaml(.cs)           # Generic Host setup, DI, startup routing
├── MainWindow.xaml(.cs)    # Shell — NavigationView sidebar, content frame
├── Views/
│   ├── Pages/              # Navigable pages (INavigableView<T>)
│   │   ├── DashboardPage.xaml(.cs)
│   │   ├── AppsPage.xaml(.cs)
│   │   ├── DotfilesPage.xaml(.cs)
│   │   ├── SystemTweaksPage.xaml(.cs)
│   │   └── SettingsPage.xaml(.cs)
│   ├── Wizard/             # Wizard-specific views
│   │   ├── WizardShell.xaml(.cs)       # StepBar + content host
│   │   ├── WizardProfilePage.xaml(.cs)
│   │   ├── WizardReviewPage.xaml(.cs)
│   │   └── WizardCompletePage.xaml(.cs)
│   └── Controls/           # Reusable UserControls
│       ├── AppCard.xaml(.cs)
│       ├── ProfileCard.xaml(.cs)
│       ├── StatusRibbon.xaml(.cs)
│       ├── DriftHeroBanner.xaml(.cs)
│       ├── DeployBar.xaml(.cs)
│       ├── TierSectionHeader.xaml(.cs)
│       ├── AppsView.xaml(.cs)          # Shared card grid — wizard + dashboard
│       ├── DotfilesView.xaml(.cs)      # Shared card grid — wizard + dashboard
│       ├── SystemTweaksView.xaml(.cs)  # Shared card grid — wizard + dashboard
│       ├── GalleryTreeView.xaml(.cs)   # Unified tree browser — drillable category navigation
│       └── TweakDetailPanel.xaml(.cs)  # Inline current/desired/default values + Open Location
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   ├── DashboardViewModel.cs
│   ├── AppsViewModel.cs
│   ├── DotfilesViewModel.cs
│   ├── SystemTweaksViewModel.cs
│   ├── GalleryTreeViewModel.cs
│   ├── SettingsViewModel.cs
│   └── Wizard/
│       ├── WizardShellViewModel.cs
│       ├── WizardProfileViewModel.cs
│       ├── WizardReviewViewModel.cs
│       └── WizardCompleteViewModel.cs
├── Models/                 # Desktop-specific display models
│   └── AppCardModel.cs    # Wraps Core's AppModule + status for card binding
├── Services/               # Desktop-specific services
│   └── AppDetectionService.cs  # Filesystem-based app/config detection
└── Resources/
    ├── Styles.xaml         # Custom styles, color tokens, overrides
    └── Assets/             # Logo, profile hero images, app icons
```

### DI Lifetimes

**Core + CLI:**
- **Singleton:** Stateless services (manifest parser, symlink provider, settings loader)
- **Transient:** Stateful or per-operation objects (deploy context, result collectors)
- **No Scoped:** No request scope in a CLI tool

**Desktop:**
- **Singleton:** Core services (via `AddPerchCore()`), WPF UI services (`INavigationService`, `ISnackbarService`, etc.), `MainWindow` + `MainWindowViewModel`, cached pages (Dashboard, Settings)
- **Transient:** Wizard step pages (recreated per navigation), per-operation ViewModels if needed
- **No Scoped:** WPF has no request scope either

### Result Level Guidelines

- **Info:** Normal operation completed (symlink created, symlink already exists — skipped)
- **Warning:** Module completed but something notable happened (existing file backed up, target directory created)
- **Error:** Module could not complete its task (target path invalid, symlink creation failed, manifest parse error)

## Project Structure & Boundaries

### Complete Project Tree

```
Perch.sln
├── .github/
│   └── workflows/
│       └── ci.yml                    # GitHub Actions: build + test (Windows + Linux)
├── src/
│   ├── Perch.Core/
│   │   ├── Perch.Core.csproj
│   │   ├── Config/
│   │   │   ├── PerchSettings.cs      # Settings model (config repo path)
│   │   │   ├── ISettingsProvider.cs
│   │   │   └── YamlSettingsProvider.cs
│   │   ├── Modules/
│   │   │   ├── AppModule.cs           # Module model (parsed manifest)
│   │   │   ├── AppManifest.cs         # YAML manifest model
│   │   │   ├── IModuleDiscoveryService.cs
│   │   │   └── ModuleDiscoveryService.cs
│   │   ├── Symlinks/
│   │   │   ├── ISymlinkProvider.cs
│   │   │   └── WindowsSymlinkProvider.cs
│   │   ├── Backup/
│   │   │   ├── IFileBackupProvider.cs
│   │   │   └── FileBackupProvider.cs
│   │   ├── Deploy/
│   │   │   ├── DeployResult.cs        # Result record + ResultLevel enum
│   │   │   ├── IDeployService.cs
│   │   │   └── DeployService.cs       # Pipeline orchestration
│   │   ├── Tweaks/                    # Scope 3: multi-mechanism tweak engine
│   │   │   ├── ITweakProvider.cs      # Interface for apply/revert/detect per mechanism
│   │   │   ├── RegistryTweakProvider.cs
│   │   │   ├── ScriptTweakProvider.cs # PowerShell script execution
│   │   │   ├── FontTweakProvider.cs
│   │   │   └── TweakStateCapture.cs   # Three-value model: capture/compare machine state
│   │   ├── Gallery/                   # Scope 3: gallery schema & taxonomy
│   │   │   ├── GalleryEntry.cs        # Unified model (app/tweak/font)
│   │   │   ├── GalleryLoader.cs       # Load + parse gallery YAML catalog
│   │   │   ├── GalleryMerger.cs       # Merge manifest deviations over gallery defaults
│   │   │   └── DependencyResolver.cs  # suggests/requires graph resolution
│   │   ├── Detection/                 # Scope 3: detect-first flow
│   │   │   ├── IMachineStateScanner.cs
│   │   │   └── WindowsMachineStateScanner.cs  # Registry/font/app state detection
│   │   └── ServiceCollectionExtensions.cs  # AddPerchCore() DI registration
│   ├── Perch.Cli/
│   │   ├── Perch.Cli.csproj
│   │   ├── Program.cs                 # Entry point, DI setup
│   │   └── Commands/
│   │       └── DeployCommand.cs       # Spectre.Console.Cli command + rendering
│   └── Perch.Desktop/
│       ├── Perch.Desktop.csproj       # net10.0-windows, WPF-UI, HandyControl, CommunityToolkit.Mvvm
│       ├── App.xaml(.cs)              # Generic Host, DI, startup routing (wizard vs dashboard)
│       ├── MainWindow.xaml(.cs)       # Shell — NavigationView sidebar + content frame
│       ├── Views/
│       │   ├── Pages/                 # INavigableView<T> pages for NavigationView
│       │   │   ├── DashboardPage.xaml(.cs)
│       │   │   ├── AppsPage.xaml(.cs)
│       │   │   ├── DotfilesPage.xaml(.cs)
│       │   │   ├── SystemTweaksPage.xaml(.cs)
│       │   │   └── SettingsPage.xaml(.cs)
│       │   ├── Wizard/               # Wizard-mode views
│       │   │   ├── WizardShell.xaml(.cs)
│       │   │   ├── WizardProfilePage.xaml(.cs)
│       │   │   ├── WizardReviewPage.xaml(.cs)
│       │   │   └── WizardCompletePage.xaml(.cs)
│       │   └── Controls/             # Reusable UserControls (wizard + dashboard)
│       │       ├── AppCard.xaml(.cs)
│       │       ├── ProfileCard.xaml(.cs)
│       │       ├── StatusRibbon.xaml(.cs)
│       │       ├── DriftHeroBanner.xaml(.cs)
│       │       ├── DeployBar.xaml(.cs)
│       │       ├── TierSectionHeader.xaml(.cs)
│       │       ├── AppsView.xaml(.cs)
│       │       ├── DotfilesView.xaml(.cs)
│       │       ├── SystemTweaksView.xaml(.cs)
│       │       ├── GalleryTreeView.xaml(.cs)    # Unified tree browser — drillable categories
│       │       └── TweakDetailPanel.xaml(.cs)   # Inline values + Open Location
│       ├── ViewModels/
│       │   ├── MainWindowViewModel.cs
│       │   ├── DashboardViewModel.cs
│       │   ├── AppsViewModel.cs
│       │   ├── DotfilesViewModel.cs
│       │   ├── SystemTweaksViewModel.cs
│       │   ├── GalleryTreeViewModel.cs
│       │   ├── SettingsViewModel.cs
│       │   └── Wizard/
│       │       ├── WizardShellViewModel.cs
│       │       ├── WizardProfileViewModel.cs
│       │       ├── WizardReviewViewModel.cs
│       │       └── WizardCompleteViewModel.cs
│       ├── Models/
│       │   └── AppCardModel.cs        # Display model wrapping Core's AppModule + status
│       ├── Services/
│       │   └── AppDetectionService.cs # Filesystem-based app/config detection for cards
│       └── Resources/
│           ├── Styles.xaml            # Color tokens, custom styles, Fluent overrides
│           └── Assets/                # Logo, Midjourney profile images, app icons
└── tests/
    ├── Perch.Core.Tests/
    │   ├── Perch.Core.Tests.csproj
    │   ├── Modules/
    │   │   └── ModuleDiscoveryServiceTests.cs
    │   ├── Deploy/
    │   │   └── DeployServiceTests.cs
    │   ├── Config/
    │   │   └── YamlSettingsProviderTests.cs
    │   ├── Symlinks/
    │   │   └── WindowsSymlinkProviderTests.cs  # Real filesystem tests
    │   ├── Tweaks/                              # Scope 3
    │   │   ├── RegistryTweakProviderTests.cs
    │   │   └── TweakStateCaptureTests.cs
    │   └── Gallery/                             # Scope 3
    │       ├── GalleryLoaderTests.cs
    │       ├── GalleryMergerTests.cs
    │       └── DependencyResolverTests.cs
    └── Perch.Desktop.Tests/
        ├── Perch.Desktop.Tests.csproj
        └── ViewModels/
            ├── DashboardViewModelTests.cs
            ├── AppsViewModelTests.cs
            └── Wizard/
                └── WizardShellViewModelTests.cs
├── tools/                                       # Scope 3: one-time import scripts
│   ├── import-winutil.ps1                       # WinUtil tweaks.json → gallery YAML stubs
│   ├── import-sophia.ps1                        # Sophia Script functions → gallery YAML stubs
│   └── dedup-gallery.ps1                        # Compare entries, flag overlaps for review
```

### FR to Structure Mapping

| FR Category | Location |
|---|---|
| FR1-FR3 Manifest & Module Management | `Perch.Core/Modules/` |
| FR7-FR8 Symlink Engine | `Perch.Core/Symlinks/` + `Perch.Core/Backup/` |
| FR14-FR17 CLI Interface | `Perch.Cli/Commands/` |
| FR33-FR34 Multi-mechanism tweaks | `Perch.Core/Tweaks/` (RegistryTweakProvider, ScriptTweakProvider, FontTweakProvider) |
| FR35 Sync status dashboard | `Perch.Desktop/Views/Pages/DashboardPage` + `DriftHeroBanner` control |
| FR36 Filesystem explorer for config discovery | `Perch.Desktop/Views/Pages/` (future) |
| FR37 Visual manifest editor | `Perch.Desktop/Views/Pages/` (future) |
| FR39-FR40 Engine Configuration | `Perch.Core/Config/` |
| FR41-FR42 Platform-aware manifests | `Perch.Core/Modules/AppManifest.cs` (Scope 2 paths) |
| FR59-FR62 Tweak dependencies, app-owned tweaks, detect-first | `Perch.Core/Tweaks/` + `Perch.Core/Detection/` |
| FR63-FR69 Gallery schema & taxonomy | `Perch.Core/Gallery/` |
| FR70-FR73 Import & sourcing | `tools/import-winutil.ps1`, `tools/import-sophia.ps1`, `tools/dedup-gallery.ps1` |
| FR74 Tweak detail panel + Open Location | `Perch.Desktop/Views/Controls/TweakDetailPanel` |
| FR75 Gallery tree browser | `Perch.Desktop/Views/Controls/GalleryTreeView` + `GalleryTreeViewModel` |

### Architectural Boundaries

**Perch.Core → Perch.Cli:** One-way dependency. Core has zero knowledge of CLI. Core exposes services and result types; CLI renders them via Spectre.Console.

**Perch.Core → Perch.Desktop:** One-way dependency. Core has zero knowledge of Desktop/WPF. Core exposes services and result types; Desktop renders them via WPF/XAML. Same `AddPerchCore()` DI registration used by both CLI and Desktop.

**Perch.Cli ↔ Perch.Desktop:** No dependency. These are independent UI hosts. They share Core but never reference each other. No shared UI abstractions — Spectre.Console and WPF/XAML are completely different rendering models.

**Strict rendering boundaries:**
- Spectre.Console types appear **only in Perch.Cli** — never in Core or Desktop
- WPF/XAML types appear **only in Perch.Desktop** — never in Core or CLI
- Core returns plain C# objects (records, lists, enums) that any UI can consume

**CLI deploy data flow:**
```
CLI parses args → resolves settings → calls IDeployService.DeployAsync()
  → IModuleDiscoveryService discovers modules
  → per module: ISymlinkProvider checks state → IFileBackupProvider backs up → ISymlinkProvider creates link
  → returns IReadOnlyList<DeployResult>
CLI renders results via Spectre.Console
```

**Desktop deploy data flow:**
```
ViewModel calls IDeployService.DeployAsync() with IProgress<DeployResult> callback
  → same Core pipeline as CLI
  → each DeployResult fires IProgress callback
ViewModel updates ObservableCollection → card StatusRibbon updates via binding
DeployBar shows aggregate progress
```

**Desktop wizard data flow:**
```
WizardShellViewModel manages step navigation (StepBar binding)
  → each step page hosts a shared view (AppsView, DotfilesView)
  → shared views bind to same ViewModels used in dashboard pages
  → WizardReviewViewModel aggregates selections across steps
  → calls IDeployService.DeployAsync() on deploy
```

**External boundary:** Filesystem only (Scope 1). Desktop adds no network dependency — gallery/detection is filesystem-based. Network only for gallery download (future).

## Architecture Validation

**Coherence:** Pass — all technology choices, patterns, and structure aligned. Core engine remains UI-agnostic. CLI and Desktop are independent UI hosts sharing the same `AddPerchCore()` registration. New Scope 3 modules (Tweaks, Gallery, Detection) follow the same interface-first pattern.

**Scope 1 FR Coverage:** All 11 Scope 1 FRs mapped to specific architectural components (CLI path).

**Scope 3 Windows Tweaks Coverage:** FR33-34 (multi-mechanism tweaks), FR59-62 (dependencies, app-owned tweaks, state capture, detect-first), FR63-69 (gallery schema evolution), FR70-73 (import tooling, CI validation), FR74-75 (desktop tweak detail, tree browser) — all mapped to `Perch.Core/Tweaks/`, `Perch.Core/Gallery/`, `Perch.Core/Detection/`, `Perch.Desktop/Views/Controls/`, and `tools/`.

**Desktop FR Coverage:** FR35-FR37, FR49-FR53, FR74-FR75 mapped to WPF Desktop project. Smart status model (Applied/Drifted/Linked/Broken/Installed per mechanism type) integrated into card system. GalleryTreeView provides unified tree browser. TweakDetailPanel shows three-value inline display + Open Location.

**NFR Coverage:** Graceful shutdown (CancellationToken), fault isolation (per-module results), platform abstraction (ISymlinkProvider, ITweakProvider), testability (interfaces + mocks) — all addressed. Desktop adds ViewModel testability via CommunityToolkit.Mvvm + NSubstitute against Core interfaces. Gallery schema validated by CI.

**Gaps:** None critical. Path resolution (`%AppData%` expansion) starts as a simple function — no premature abstraction. Desktop `IProgress<DeployResult>` callback pattern for live card updates — straightforward, no complex async needed. Import tooling is standalone PowerShell scripts outside the solution — no architectural coupling.

**Key architectural invariants:**
- Core never references any UI framework. Both CLI and Desktop are thin presentation layers
- Gallery is the source of truth. Manifests store deviations only
- ITweakProvider interface abstracts mechanism types — new mechanisms (services, scheduled tasks) slot in without changing consumers
- Three-value model (default/captured/desired) is the foundation for drift detection, revert, and status display across all mechanism types

**Status:** IMPLEMENTED — Core, CLI, and Desktop all built and working. Desktop-first pivot acknowledged (2026-02-19).

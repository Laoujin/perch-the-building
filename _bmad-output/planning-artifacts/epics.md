---
stepsCompleted: [step-01-validate-prerequisites, step-02-design-epics, step-03-create-stories, step-04-final-validation, implementation-readiness-check]
inputDocuments:
  - '_bmad-output/planning-artifacts/prd.md'
  - '_bmad-output/planning-artifacts/architecture.md'
  - '_bmad-output/planning-artifacts/ux-design-specification.md'
  - '_bmad-output/brainstorming/brainstorming-session-2026-02-17.md'
---

# Perch - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Perch, decomposing the requirements from the PRD and Architecture into implementable stories.

## Requirements Inventory

### Functional Requirements

**Manifest & Module Management**
- FR1: User can define an app module by creating a named folder containing a manifest file and config files [Scope 1]
- FR2: System discovers all app modules automatically by scanning for manifest files in the config repo (no central registration) [Scope 1]
- FR3: User can specify in a manifest where config files should be symlinked to, using platform-appropriate path variables (`%AppData%`, `$HOME`, `$XDG_CONFIG_HOME`, etc.) [Scope 1]
- FR4: System resolves pattern-based/glob config paths for apps with dynamic settings locations [Scope 2]
- FR5: User can specify version-range-aware symlink paths in a manifest [Scope 3]
- FR6: User can pull manifest templates from an external repository/gallery [Scope 3]
- FR41: System supports platform-aware target paths in manifests — different target locations per OS from a single module [Scope 2]
- FR42: User can mark modules as platform-specific — system only processes them on matching OS [Scope 2]

**Symlink Engine**
- FR7: System creates symlinks (and junctions on Windows) from config repo files to target locations on the filesystem. If a target file already exists, it is moved to `.backup` before creating the symlink [Scope 1]
- FR8: System re-runs deploy without affecting existing symlinks — only modules without an existing symlink are processed [Scope 1]
- FR9: System detects locked target files and reports them [Scope 2]
- FR10: System detects drift between expected and actual symlink state [Scope 2]
- FR11: System performs dry-run showing what would change without modifying the filesystem [Scope 2]
- FR12: System creates full pre-deploy backup snapshots of all target files [Scope 2]
- FR13: User can restore files from a backup (conflict `.backup` or full snapshot) [Scope 3]

**CLI Interface**
- FR14: User can run a deploy command that processes all discovered modules [Scope 1]
- FR15: System streams each action to the console in real-time with colored status indicators [Scope 1]
- FR16: System returns clean exit codes indicating success or specific failure types [Scope 1]
- FR17: User can abort execution mid-deploy via Ctrl+C (graceful shutdown — current module completes, then deploy halts) [Scope 1]
- FR18: System outputs structured JSON results for machine consumption [Scope 2]
- FR19: System displays a live-updating progress table alongside action streaming [Scope 2]
- FR20: User can run deploy in interactive mode with step-level and command-level confirmation [Scope 3]
- FR21: User can tab-complete Perch commands in the shell [Scope 3]

**Package Management**
- FR22: User can define all managed packages in a single manifest file, supporting chocolatey and winget with per-package manager specification [Scope 2]
- FR23: System detects installed apps and cross-references against managed modules [Scope 2]
- FR24: System reports apps installed but without a config module [Scope 2]
- FR48: User can define packages for cross-platform package managers (apt, brew, and others such as VS Code extensions, npm/bun global packages) using the same manifest format [Scope 3]

**Scoop Integration**
- FR54: User can define a Scoop app manifest (`scoop.yaml`) in the config repo listing buckets and apps to install [Scope 5]
- FR55: System manages Scoop buckets declaratively — adds listed buckets, skips already-added ones [Scope 5]
- FR56: System installs Scoop apps declaratively — installs listed apps, skips already-installed ones (idempotent) [Scope 5]
- FR57: User can export currently installed Scoop apps and diff against the manifest to discover untracked apps [Scope 5]
- FR58: System leverages Scoop's predictable install paths (`~/scoop/apps/<name>/current/`) to assist config module discovery [Scope 5]

**Git Integration**
- FR25: System registers per-app git clean filters to suppress noisy config diffs [Scope 2]
- FR26: System performs before/after filesystem diffing to discover config file changes [Scope 2]

**App Discovery & Onboarding**
- FR27: User can scan the system for installed apps and see which have config modules [Scope 2]
- FR28: System looks up known config file locations for popular apps [Scope 3]
- FR29: System launches an app in Windows Sandbox to discover its config locations [Scope 3]
- FR30: User can generate a new module manifest via interactive onboarding workflow (CLI or MAUI) [Scope 3]

**Machine Configuration**
- FR31: User can define base config values with per-machine overrides [Scope 3]
- FR32: User can specify which modules apply to which machines [Scope 3]
- FR33: User can manage Windows registry settings declaratively via multi-mechanism definitions (registry YAML + PowerShell script + undo_script) [Scope 3]
- FR34: System applies and reports on registry state using three-value model (default_value / captured machine state / desired value) across all mechanism types [Scope 3]
- FR59: Gallery entries can declare `suggests:` (soft) and `requires:` (hard) dependency links. Execution order derives from the dependency graph, replacing the `priority` field [Scope 3]
- FR60: App gallery entries own their tweaks — bad behavior (context menus, startup entries, telemetry) as toggleable sub-items within the app entry [Scope 3]
- FR61: On first deploy, system captures current value of each managed registry key per machine before applying. User can revert to "restore my previous" or "restore Windows default" [Scope 3]
- FR62: System detects current machine state first (detect-first flow) — scans existing registry/app/font state and matches against gallery before configuring [Scope 3]

**Secrets Management**
- FR43: System can inject secrets from a supported password manager into config files at deploy time, producing generated (non-symlinked) files [Scope 3]
- FR44: User can define secret placeholders in config templates that are resolved from a configured password manager during deploy [Scope 3]
- FR45: System manages any config file containing secret placeholders as a generated (non-symlinked) file [Scope 3]

**Gallery Schema & Taxonomy**
- FR63: Gallery is the source of truth for defaults. User manifests store only deviations plus captured old values [Scope 3]
- FR64: Gallery uses a unified tree taxonomy with deep category paths. Tree depth matches content density [Scope 3]
- FR65: Gallery entries declare a `type:` field (app, tweak, font) with shared base schema [Scope 3]
- FR66: Gallery entries declare `windows_versions: [10, 11]` for OS-aware filtering. Desktop hides entries for wrong OS [Scope 3]
- FR67: Gallery categories declare a `sort:` value controlling display order in the tree [Scope 3]
- FR68: Gallery entries can declare `restart_required: true` [Scope 3]
- FR69: `index.yaml` is auto-generated from catalog folder contents [Scope 3]

**Import & Sourcing**
- FR70: One-time WinUtil importer — parse `tweaks.json`, generate gallery YAML stubs for human review (~65 tweaks) [Scope 3]
- FR71: One-time Sophia Script importer (~270 functions) with deduplication against existing entries [Scope 3]
- FR72: Import pipeline checks LICENSE files of source repos for compliance [Scope 3]
- FR73: CI GitHub Action validates every registry path in the gallery on a matrix of Windows versions [Scope 3]

**Desktop UI (WPF)**
- FR35: User can view sync status of all managed modules in a visual dashboard with mechanism-aware smart status [Scope 3]
- FR36: User can interactively explore an app's filesystem to find config locations [Scope 3]
- FR37: User can generate and edit module manifests via a visual interface [Scope 3]
- FR74: Desktop tweak cards show inline current/desired/default values and universal "Open Location" button (regedit, Explorer, services.msc, Fonts folder) [Scope 3]
- FR75: Desktop gallery/tree browser is the primary discovery UX. Website is marketing only [Scope 3]

**Plugin Lifecycle**
- FR38: User can define pre-deploy and post-deploy hooks per module [Scope 2]

**Engine Configuration**
- FR39: User can specify the config repo location as a CLI argument [Scope 1]
- FR40: System persists the config repo location (settings file alongside engine) so it doesn't need to be specified on subsequent runs [Scope 1]

**Migration & Compatibility**
- FR46: System can import/convert a chezmoi-managed dotfiles repo into Perch format (manifests + plain config files) [Scope 4]
- FR47: System can import/convert Dotbot and Dotter repos into Perch format, and export Perch format to those tools (two-way migration) [Scope 4]

### NonFunctional Requirements

**Reliability**
- NFR1: On Ctrl+C, the in-progress module completes fully (backup + symlink creation), then deploy halts. No module is ever left in a partial state
- NFR2: Failed symlink operations for one module generate an error (logged and displayed), but do not prevent other modules from processing
- NFR3: Missing target directories are logged to the deploy context and displayed to the user. The affected module is skipped, deploy continues

**Maintainability**
- NFR4: Human-readable codebase following KISS and YAGNI principles. Max cyclomatic complexity enforced via analyzers. TDD development approach
- NFR5: Platform-specific logic abstracted behind interfaces with separate implementations per platform. Core engine depends only on interfaces, never on platform implementations directly
- NFR6: 100% unit test coverage on core engine logic (symlink creation, manifest parsing, module discovery). All branches and flows tested [Scope 2]
- NFR7: CI pipeline fails on any failing test, any analyzer warning, or any compiler warning. Static analysis via Roslynator and Microsoft.CodeAnalysis.Analyzers with warnings treated as errors. CI runs on Windows and Linux runners [Scope 2]

**Portability**
- NFR8: Scope 1: runs on Windows 10+ with .NET 10 runtime
- NFR9: Scope 2: runs on Windows 10+, Linux (major distros), and macOS with .NET 10 runtime
- NFR10: No dependency on specific shell (PowerShell, cmd, bash, zsh all work)
- NFR11: Config repo format: plain files + YAML manifests — no binary formats, no database, no proprietary encoding
- NFR12: `dotnet tool install perch -g` works on all supported platforms [Scope 2]
- NFR13: WPF Desktop app is Windows-only; CLI remains cross-platform [Scope 3]

### Additional Requirements

**From Architecture:**
- Starter template: Solution structure defined with `dotnet new` commands — project initialization should be the first implementation story
- Manifest format changed from JSON (PRD) to YAML (Architecture Decision #1) — requires YamlDotNet dependency
- Engine pipeline pattern: Discover → Validate → Execute → Report with live Spectre output
- Platform abstraction: Interface only for symlink/junction creation + detection; .NET handles most cross-platform natively via System.IO
- Error handling via result records: `DeployResult(ModuleName, SourcePath, TargetPath, ResultLevel, Message)` with `ResultLevel { Info, Warning, Error }`
- Dependency injection: Microsoft.Extensions.DependencyInjection with `AddPerchCore()` extension method
- Settings persistence: YAML file at `%APPDATA%/perch/settings.yaml` (Windows) / `~/.config/perch/settings.yaml` (Linux/macOS)
- Feature folder organization: Modules/, Symlinks/, Deploy/, Config/, Backup/
- Naming conventions: `I<Noun>Provider` for abstractions over external resources, `I<Noun>Service` for orchestration, `kebab-case` for YAML properties
- DI lifetime rules: Singleton for stateless services, Transient for stateful/per-operation objects
- Exit codes: 0 success, 1 partial failure, 2 fatal
- Result level semantics: Info (normal), Warning (notable but completed), Error (could not complete)
- Desktop UI: WPF (not MAUI) with WPF UI 4.2.0, HandyControl 3.5.1, CommunityToolkit.Mvvm 8.4.0
- Desktop MVVM: `ObservableObject` base, `[ObservableProperty]`, `[RelayCommand]`, `INavigableView<T>` for pages
- Desktop DI: Generic Host in App.xaml.cs, same `AddPerchCore()` registration as CLI
- Desktop modes: Wizard (first-run, StepBar) + Dashboard (ongoing, NavigationView sidebar), shared card-based UserControls
- Desktop rendering boundary: WPF/XAML only in Perch.Desktop, never in Core. Core returns data; Desktop renders via bindings

**From UX Design Specification:**
- Custom components: StatusRibbon (mechanism-aware), ProfileCard, AppCard (with app-owned tweaks), DriftHeroBanner, DeployBar, TierSectionHeader, GalleryTreeView, TweakDetailPanel
- Design system: WPF UI Fluent 2 dark theme, forest green (#10B981) accent, status colors (green/yellow/red/blue)
- Card-based interaction model: detection-first, three-tier layout (detected/suggested/other), card toggle + expand
- Wizard steps: Profile Selection → Dotfiles → Apps → System Tweaks (detect-first scan) → Review & Deploy (dynamic based on profile)
- Dashboard: Drift hero banner + mechanism-aware smart status cards on Home, GalleryTreeView for unified tree browsing, TweakDetailPanel with three-value display + Open Location
- Unified tree taxonomy: apps, tweaks, fonts, dotfiles in one navigable tree with deep category paths
- OS-version-aware: entries for wrong Windows version are hidden (not greyed out)

### FR Coverage Map

| FR | Epic | Description |
|----|------|-------------|
| FR1 | 1 | Module definition via folder + manifest |
| FR2 | 1 | Auto-discovery by scanning for manifests |
| FR3 | 1 | Platform path variables in manifests |
| FR4 | 2 | Glob/pattern-based dynamic paths |
| FR5 | 9 | Version-range-aware manifest paths |
| FR6 | 9 | Manifest templates from gallery |
| FR7 | 1 | Symlink/junction creation with backup |
| FR8 | 1 | Re-runnable additive deploy |
| FR9 | 3 | Locked file detection |
| FR10 | 3 | Drift detection |
| FR11 | 3 | Dry-run mode |
| FR12 | 3 | Pre-deploy backup snapshots |
| FR13 | 8 | Restore from backup |
| FR14 | 1 | Deploy command |
| FR15 | 1 | Real-time colored output |
| FR16 | 1 | Clean exit codes |
| FR17 | 1 | Graceful Ctrl+C shutdown |
| FR18 | 3 | Structured JSON output |
| FR19 | 3 | Live progress table |
| FR20 | 8 | Interactive deploy mode |
| FR21 | 8 | Shell tab-completion |
| FR22 | 4 | Package manifest (choco/winget) |
| FR23 | 4 | Installed app cross-reference |
| FR24 | 4 | Missing config module reporting |
| FR25 | 5 | Per-app git clean filters |
| FR26 | 5 | Before/after filesystem diffing |
| FR27 | 4 | Installed app scanning |
| FR28 | 9 | AI config path lookup |
| FR29 | 9 | Windows Sandbox discovery |
| FR30 | 9 | Interactive manifest generation |
| FR31 | 6 | Per-machine overrides |
| FR32 | 6 | Module-to-machine filtering |
| FR33 | 6 | Declarative registry management |
| FR34 | 6 | Registry state reporting (three-value model, all mechanism types) |
| FR35 | 11 | Desktop drift dashboard with hero banner, smart status cards, one-click fix |
| FR36 | 11 | Desktop filesystem explorer (future) |
| FR37 | 11 | Desktop manifest editor (future) |
| FR59 | 6 | Gallery dependency links (suggests/requires) |
| FR60 | 6 | App-owned tweaks (bad behavior as sub-items) |
| FR61 | 6 | State capture per machine on first deploy |
| FR62 | 6+10 | Detect-first flow (scan before configure) |
| FR63 | 14 | Gallery as source of truth, manifests store deviations |
| FR64 | 14 | Unified tree taxonomy with deep category paths |
| FR65 | 14 | Gallery type field (app, tweak, font) |
| FR66 | 14 | OS-version-aware gallery filtering |
| FR67 | 14 | Category sort order field |
| FR68 | 14 | Restart required field |
| FR69 | 14 | Auto-generated index.yaml |
| FR70 | 15 | WinUtil importer |
| FR71 | 15 | Sophia Script importer with dedup |
| FR72 | 15 | License compliance check |
| FR73 | 15 | CI registry path validation matrix |
| FR74 | 11 | Inline tweak values + Open Location button |
| FR75 | 10+11 | Gallery tree browser as primary discovery UX |
| FR38 | 5 | Pre/post-deploy lifecycle hooks |
| FR39 | 1 | Config repo path via CLI argument |
| FR40 | 1 | Persisted config repo path |
| FR41 | 2 | Platform-aware target paths |
| FR42 | 2 | Platform-specific module filtering |
| FR43 | 7 | Secret injection from password manager |
| FR44 | 7 | Secret placeholder syntax |
| FR45 | 7 | Generated files for secret configs |
| FR46 | 12 | Chezmoi import/conversion |
| FR47 | 12 | Dotbot/Dotter import/export |
| FR48 | 8 | Cross-platform package managers |
| FR49 | 10 | Desktop wizard with profile selection and dynamic steps |
| FR50 | 10 | Desktop detection-first three-tier card layout |
| FR51 | 10+11 | Shared card-based view components (wizard + dashboard) |
| FR52 | 10+11 | Desktop deploy with per-card progress and deploy bar |
| FR53 | 10+11 | Desktop card grid and compact list density toggle |
| FR54 | 13 | Scoop app manifest in config repo |
| FR55 | 13 | Declarative Scoop bucket management |
| FR56 | 13 | Declarative Scoop app installation |
| FR57 | 13 | Scoop app export/diff |
| FR58 | 13 | Scoop path-based config discovery |

## Epic List

### Epic 1: Deploy Managed Configs (Scope 1 — MVP)
Developer clones config repo, runs `perch deploy`, and all managed configs are symlinked to their target locations on Windows. Existing files backed up. Re-runnable. Graceful Ctrl+C shutdown.
**FRs covered:** FR1, FR2, FR3, FR7, FR8, FR14, FR15, FR16, FR17, FR39, FR40
**NFRs addressed:** NFR1-5, NFR8, NFR10-11

### Epic 2: Cross-Platform Deploy (Scope 2)
Developer can deploy configs on Linux and macOS. Platform-aware manifests specify per-OS target paths. Windows-only modules skip automatically on other platforms. Dynamic path resolution for complex apps. Distributed as a dotnet tool.
**FRs covered:** FR4, FR41, FR42
**NFRs addressed:** NFR9, NFR12

### Epic 3: Deploy Safety & Observability (Scope 2)
Developer can preview changes with dry-run, detect drift between expected and actual state, see locked files reported, get full pre-deploy backups, consume structured JSON output, see live progress tables, and have CI validation on every push.
**FRs covered:** FR9, FR10, FR11, FR12, FR18, FR19
**NFRs addressed:** NFR6, NFR7

### Epic 4: Package & App Awareness (Scope 2)
Developer tracks installed packages across chocolatey/winget, discovers which installed apps lack config modules, and scans for managed vs unmanaged apps.
**FRs covered:** FR22, FR23, FR24, FR27

### Epic 5: Git Integration & Deploy Hooks (Scope 2)
Developer can suppress noisy config diffs via per-app git clean filters, diff filesystem changes before/after app tweaks, and run custom pre/post-deploy scripts per module.
**FRs covered:** FR25, FR26, FR38

### Epic 6: Multi-Machine & System Tweaks Engine (Scope 3)
Developer can define per-machine overrides and declaratively manage system tweaks via multi-mechanism definitions (registry YAML + PowerShell scripts + fonts). Uses a three-value model (default / captured / desired) for drift detection and revert. App gallery entries own their tweaks (context menus, startup entries, telemetry). Detect-first flow scans current machine state before configuring. Dependency graph replaces priority field.
**FRs covered:** FR31, FR32, FR33, FR34, FR59, FR60, FR61, FR62

### Epic 7: Secrets Management (Scope 3)
Developer can manage configs containing secrets (NuGet tokens, npm tokens, SSH config, API keys) via template placeholders resolved from 1Password at deploy time. Secret-containing files are generated (not symlinked) and git-ignored.
**FRs covered:** FR43, FR44, FR45

### Epic 8: Advanced Usability (Scope 3)
Developer can restore files from backups, run deploy interactively with step-level confirmation, use shell tab-completion, and define cross-platform packages (apt, brew, VS Code extensions, npm globals).
**FRs covered:** FR13, FR20, FR21, FR48

### Epic 9: App Onboarding & Discovery (Scope 3)
Developer can discover config locations for new apps via AI lookup and Windows Sandbox isolation, generate manifests interactively, use version-range-aware paths, and pull templates from a community gallery.
**FRs covered:** FR5, FR6, FR28, FR29, FR30

### Epic 10: Desktop Wizard & Onboarding (Scope 3)
User launches Perch Desktop for the first time and is guided through a wizard: profile selection, detect-first scan of current machine state, card-based browsing and toggling in a unified tree (apps, tweaks, fonts), and deploy. System Tweaks step shows mechanism-aware smart status. The wizard is a complete standalone experience. Built with WPF UI + HandyControl on the shared Perch.Core engine.
**FRs covered:** FR35 (partial — wizard deploy + status), FR49, FR50, FR51 (partial), FR52 (partial), FR53 (partial), FR62 (detect-first), FR75 (partial — gallery tree as primary UX)
**NFRs addressed:** NFR4-5 (Core interfaces, MVVM testability), NFR13

### Epic 11: Desktop Dashboard & Drift (Scope 3)
Returning user opens Perch Desktop and sees a drift-focused dashboard: hero banner with config health summary, mechanism-aware smart status cards for all types (dotfiles, apps, tweaks, fonts), one-click fix actions. Sidebar navigation into GalleryTreeView for unified tree browsing. TweakDetailPanel shows three-value inline display and Open Location button. App-owned tweak sub-items are toggleable within app cards. Same shared card views used in wizard.
**Requires:** Epic 3 (drift detection via FR10), Epic 6 (three-value model, app-owned tweaks).
**FRs covered:** FR35, FR36 (future), FR37 (future), FR51 (partial), FR52 (partial), FR53 (partial), FR74, FR75 (partial)

### Epic 12: Migration Tools (Scope 4)
Users of chezmoi, Dotbot, or Dotter can import their dotfiles repo into Perch format, and Perch users can export back — enabling two-way migration.
**FRs covered:** FR46, FR47

### Epic 13: Scoop Integration (Scope 5)
Developer manages dev tools via Scoop from the config repo. Buckets and apps are declared in `scoop.yaml`, installed idempotently, and exportable. Scoop's predictable `~/scoop/apps/<name>/current/` paths integrate with config module discovery.
**FRs covered:** FR54, FR55, FR56, FR57, FR58

### Epic 14: Gallery Schema Evolution (Scope 3)
Gallery YAML format evolves to support the unified tree taxonomy, type system (`app | tweak | font`), OS-aware filtering, dependency graph (`suggests`/`requires`), sort order, restart tracking, and auto-generated index. Gallery becomes the source of truth — user manifests store only deviations from gallery defaults.
**FRs covered:** FR63, FR64, FR65, FR66, FR67, FR68, FR69

### Epic 15: Gallery Import & Sourcing (Scope 3)
One-time import tooling to populate the gallery from external sources. WinUtil importer (~65 tweaks from `tweaks.json`), Sophia Script importer (~270 functions with dedup). License compliance checks. CI GitHub Action validates registry paths on a matrix of Windows versions.
**FRs covered:** FR70, FR71, FR72, FR73

---

## Epic 1: Deploy Managed Configs

Developer clones config repo, runs `perch deploy`, and all managed configs are symlinked to their target locations on Windows. Existing files backed up. Re-runnable. Graceful Ctrl+C shutdown.

### Story 1.1: Initialize Project Structure

As a developer,
I want the Perch solution scaffolded with all projects, dependencies, and analyzers configured,
So that I have a buildable, testable foundation to implement features against.

**Acceptance Criteria:**

**Given** a clean repository
**When** the solution is initialized
**Then** the solution contains `Perch.Core` (classlib), `Perch.Cli` (console), and `Perch.Core.Tests` (nunit) projects targeting net10.0
**And** `Perch.Cli` references `Perch.Core`
**And** `Perch.Core.Tests` references `Perch.Core`
**And** `Perch.Cli` has Spectre.Console (0.54.0) and Spectre.Console.Cli (0.53.1) packages
**And** `Perch.Core` has YamlDotNet package
**And** `Perch.Core.Tests` has NSubstitute (5.3.0) package
**And** Roslynator and Microsoft.CodeAnalysis.Analyzers are configured with warnings as errors
**And** `dotnet build` succeeds with zero warnings
**And** `dotnet test` runs and passes
**And** `Perch.Core` has feature folders: Modules/, Symlinks/, Deploy/, Config/, Backup/
**And** `Perch.Cli` has Commands/ folder
**And** `Perch.Core` exposes an `AddPerchCore()` DI extension method (can be empty registration initially)

### Story 1.2: Define and Parse App Manifests

As a developer,
I want to define an app module as a named folder with a `manifest.yaml` containing source paths, target paths with environment variable support, and link type,
So that Perch knows which config files to symlink and where.

**Acceptance Criteria:**

**Given** a folder named `git` containing a `manifest.yaml` with source file(s), target path(s) using `%UserProfile%` syntax, and link type `symlink`
**When** the manifest parser reads the file
**Then** it returns an `AppManifest` model with resolved source paths, raw target path expressions, and link type
**And** the folder name is used as the module/app name

**Given** a manifest with environment variable syntax (`%AppData%`, `%UserProfile%`, `$HOME`, `$XDG_CONFIG_HOME`)
**When** the path resolver expands the variables
**Then** each variable is replaced with the actual platform value

**Given** a manifest with invalid YAML or missing required fields
**When** the parser attempts to read it
**Then** it returns a parse error with the module name and reason (no exception thrown)

**Given** a manifest with `link-type: junction`
**When** parsed
**Then** the model reflects junction as the link type (used for directories on Windows)

### Story 1.3: Discover App Modules

As a developer,
I want Perch to automatically find all app modules in the config repo by scanning for `manifest.yaml` files,
So that I don't need to register modules in a central file.

**Acceptance Criteria:**

**Given** a config repo with folders `git/`, `vscode/`, `windows-terminal/` each containing a `manifest.yaml`
**When** the `ModuleDiscoveryService` scans the repo root
**Then** it returns three `AppModule` objects with names matching the folder names

**Given** a config repo with a subfolder that has no `manifest.yaml`
**When** the discovery service scans
**Then** that folder is ignored

**Given** a config repo with a module whose `manifest.yaml` fails parsing
**When** the discovery service scans
**Then** it includes a result with an error for that module and continues discovering other modules

**Given** an empty config repo directory
**When** the discovery service scans
**Then** it returns an empty list (no error)

### Story 1.4: Create Symlinks with Backup

As a developer,
I want Perch to create symlinks from the config repo to target locations, backing up existing files and skipping already-linked files,
So that my config files are linked in place and re-running is safe.

**Acceptance Criteria:**

**Given** a module with source file `gitconfig` and target path `C:\Users\me\.gitconfig` where no file exists at the target
**When** the symlink engine processes the module
**Then** a symlink is created at `C:\Users\me\.gitconfig` pointing to the source file in the config repo
**And** the result level is Info with a "created" message

**Given** a target path where a regular file already exists
**When** the symlink engine processes the module
**Then** the existing file is renamed to `<filename>.backup`
**And** a symlink is created at the original target path
**And** the result level is Warning with a "backed up and linked" message

**Given** a target path where a valid symlink already points to the correct source
**When** the symlink engine processes the module
**Then** no action is taken (idempotent)
**And** the result level is Info with a "skipped — already linked" message

**Given** a module with `link-type: junction` and a source directory
**When** the symlink engine processes it on Windows
**Then** a junction is created at the target path pointing to the source directory

**Given** a target path whose parent directory does not exist
**When** the symlink engine processes the module
**Then** the module is skipped with an Error result and a message indicating the missing directory (NFR3)

**Given** the symlink creation fails (permissions, etc.)
**When** the engine processes the module
**Then** an Error result is recorded for that module and processing continues to the next module (NFR2)

### Story 1.5: Persist Engine Configuration

As a developer,
I want to specify my config repo path once and have Perch remember it for future runs,
So that I don't need to pass `--config-path` every time.

**Acceptance Criteria:**

**Given** no settings file exists at `%APPDATA%/perch/settings.yaml`
**When** the user runs `perch deploy --config-path C:\tools\dotfiles`
**Then** the config repo path is persisted to `%APPDATA%/perch/settings.yaml`
**And** deploy proceeds using that path

**Given** a settings file exists with a persisted `config-repo-path`
**When** the user runs `perch deploy` without `--config-path`
**Then** the persisted path is used

**Given** a settings file exists with a persisted path
**When** the user runs `perch deploy --config-path <new-path>`
**Then** the new path overrides and is persisted (updated in settings file)

**Given** no settings file exists and no `--config-path` is provided
**When** the user runs `perch deploy`
**Then** an error is displayed explaining that `--config-path` is required on first run
**And** exit code is 2

### Story 1.6: Deploy Command with Console Output

As a developer,
I want to run `perch deploy` and see each action streamed to the console with colored status indicators, with proper exit codes and graceful Ctrl+C support,
So that I can deploy all my configs and see exactly what happened.

**Acceptance Criteria:**

**Given** a config repo with multiple valid modules
**When** the user runs `perch deploy --config-path <path>`
**Then** the deploy pipeline executes: discover modules -> parse manifests -> per module (check state -> backup if needed -> create symlink -> record result) -> report summary
**And** each action is streamed to the console in real-time via Spectre.Console with colored indicators (green=success, yellow=warning/backup, red=error, gray=skipped)

**Given** all modules deploy successfully
**When** the deploy completes
**Then** exit code is 0

**Given** some modules succeed and some fail
**When** the deploy completes
**Then** exit code is 1 (partial failure)
**And** all successful modules are still linked (fault isolation per NFR2)

**Given** no modules could be processed (e.g., config repo empty or path invalid)
**When** the deploy completes
**Then** exit code is 2 (fatal)

**Given** the user presses Ctrl+C during deploy
**When** a module is currently being processed
**Then** the current module completes fully (backup + symlink) before halting (NFR1)
**And** no module is left in a partial state
**And** the console shows which modules completed and that deploy was cancelled

### Story 1.7: PowerShell Module Auto-Loader

As a developer,
I want `perch deploy` to generate a loader script that automatically imports all modules listed in `ps-modules`,
So that adding or removing a PS module in `manifest.yaml` is the only change needed -- no manual `Import-Module` calls scattered across profile scripts.

**Context:**

The git module uses git's native `[include]` mechanism: one `.gitconfig` entry point chains in platform-specific configs. The PowerShell module should follow a similar pattern -- one generated loader script acts as the single entry point for module imports. Per-module config scripts (PSReadLine keybindings, Posh-Git prompt settings) handle setup but don't call `Import-Module` themselves.

Modules requiring special initialization beyond `Import-Module` (zoxide CLI init, PSFzf keybindings, CompletionPredictor prediction source) need per-module init blocks in the loader.

**Acceptance Criteria:**

**Given** a PowerShell manifest with `ps-modules: [Posh-Git, PSReadLine, zoxide, powershell-yaml, Git-NumberedAdd, Terminal-Icons, PSFzf, CompletionPredictor]`
**When** the user runs `perch deploy`
**Then** a `module-loader.ps1` is generated in the PowerShell scripts directory that imports each listed module
**And** the generated script is idempotent (safe to source multiple times)

**Given** a module in the `ps-modules` list is not installed on the machine
**When** the loader runs at shell startup
**Then** that module is silently skipped (no error, no broken prompt)

**Given** a module requires special initialization (e.g., zoxide needs `zoxide init powershell`, PSFzf needs keybinding config, CompletionPredictor needs `PredictionSource` set)
**When** the loader imports that module
**Then** the module-specific init block runs after the import

**Given** the profile currently has explicit `Import-Module PSReadline` in `prompt_readline.ps1` and `Import-Module Posh-Git` in `git-posh-git.ps1`
**When** the loader is in place
**Then** those explicit imports are removed -- the loader handles all imports, config scripts only handle post-import setup

**Given** the loader is generated by `perch deploy`
**When** the user's PowerShell profile sources the scripts directory
**Then** the loader runs before module-specific config scripts (load order: import first, configure second)

**Given** the loader must work without external dependencies at parse time
**When** the loader is generated
**Then** it is plain PowerShell (no YAML parsing at shell startup) -- Perch generates it from the manifest at deploy time

**Open Questions:**

1. Should the loader be a generated file (written by Perch at deploy time) or a static script that reads the manifest at shell startup?
2. How should per-module init blocks be defined -- hardcoded in the loader template, or configurable via a `post-import` hook in the manifest?
3. Should this be a new manifest property (e.g., `ps-module-loader: true`) or always generated when `ps-modules` is present?

## Epic 2: Cross-Platform Deploy

Developer can deploy configs on Linux and macOS. Platform-aware manifests specify per-OS target paths. Windows-only modules skip automatically on other platforms. Dynamic path resolution for complex apps.

### Story 2.1: Platform-Aware Manifest Paths

As a developer,
I want to specify different target paths per OS in a single manifest,
So that one module can deploy to the correct location on Windows, Linux, and macOS.

**Acceptance Criteria:**

**Given** a manifest with per-platform target paths (e.g., `windows: %AppData%\Code\User\settings.json`, `linux: ~/.config/Code/User/settings.json`, `macos: ~/Library/Application Support/Code/User/settings.json`)
**When** the manifest parser reads it on Linux
**Then** only the `linux` target path is resolved and used

**Given** a manifest with per-platform target paths
**When** running on an OS that has no matching platform entry
**Then** the module is skipped with an Info result indicating no target path for the current platform

**Given** a manifest with a single target path (no per-platform section)
**When** the engine processes it
**Then** the single path is used on all platforms (backward compatible with Scope 1 manifests)

**Given** a manifest using Linux/macOS environment variables (`$HOME`, `$XDG_CONFIG_HOME`)
**When** the path resolver expands them on Linux or macOS
**Then** each variable is replaced with the actual platform value

### Story 2.2: Platform-Specific Module Filtering

As a developer,
I want to mark modules as applying to specific platforms only,
So that Windows-only modules (like Windows Terminal) are automatically skipped on Linux.

**Acceptance Criteria:**

**Given** a manifest with `platforms: [windows]` and the engine running on Linux
**When** the discovery service processes this module
**Then** the module is skipped with an Info result ("skipped — platform mismatch")

**Given** a manifest with `platforms: [windows, linux]` and the engine running on Linux
**When** the discovery service processes this module
**Then** the module is included for processing

**Given** a manifest with no `platforms` field
**When** the discovery service processes it
**Then** the module is included on all platforms (default: all)

### Story 2.3: Dynamic Path Resolution

As a developer,
I want to use glob/pattern-based target paths in manifests for apps that store configs in dynamic locations,
So that I can manage apps like Visual Studio that include hash strings or version numbers in their config paths.

**Acceptance Criteria:**

**Given** a manifest with a target path containing a glob pattern (e.g., `%LocalAppData%\Microsoft\VisualStudio\17.0_*/Settings/CurrentSettings.vssettings`)
**When** the path resolver evaluates it
**Then** it resolves the glob to the actual matching path(s) on the filesystem

**Given** a glob pattern that matches multiple directories
**When** the path resolver evaluates it
**Then** all matches are returned and symlinks are created for each

**Given** a glob pattern that matches nothing
**When** the path resolver evaluates it
**Then** the module is skipped with a Warning result indicating no matching paths found

### Story 2.4: Dotnet Tool Packaging

As a developer,
I want Perch distributed as a `dotnet tool` so I can install it globally on any platform,
So that setup on a new machine is just `dotnet tool install perch -g`.

**Acceptance Criteria:**

**Given** the Perch.Cli project
**When** the project is packed with `dotnet pack`
**Then** a NuGet package is produced with the correct tool manifest metadata (PackAsTool, ToolCommandName = `perch`)

**Given** the produced NuGet package
**When** a user runs `dotnet tool install --global --add-source <local-path> Perch.Cli`
**Then** the `perch` command is available globally and `perch deploy --help` shows usage

**Given** the tool is installed on Windows, Linux, or macOS with .NET 10 runtime
**When** the user runs `perch deploy`
**Then** the tool executes correctly on each platform

## Epic 3: Deploy Safety & Observability

Developer can preview changes with dry-run, detect drift between expected and actual state, see locked files reported, get full pre-deploy backups, consume structured JSON output, and see live progress tables.

### Story 3.1: Dry-Run Mode

As a developer,
I want to preview what `perch deploy` would do without modifying the filesystem,
So that I can verify the plan before committing to changes.

**Acceptance Criteria:**

**Given** a config repo with modules that would create new symlinks, skip existing ones, and back up conflicting files
**When** the user runs `perch deploy --dry-run`
**Then** all actions are reported (would create, would skip, would backup) but no filesystem changes are made

**Given** a dry-run completes
**When** the user inspects the filesystem
**Then** no symlinks were created, no files were renamed, no backups were made

**Given** a dry-run with `--output json` flag
**When** the run completes
**Then** the JSON output includes the same results as a real deploy, with a flag indicating dry-run mode

### Story 3.2: Drift Detection

As a developer,
I want to detect when symlinks are broken, missing, or pointing to the wrong location,
So that I can identify and fix configuration drift across machines.

**Acceptance Criteria:**

**Given** a previously deployed module where the symlink has been deleted
**When** the user runs `perch status`
**Then** the module is reported as "missing — symlink removed"

**Given** a previously deployed module where the symlink target was replaced with a regular file
**When** the user runs `perch status`
**Then** the module is reported as "drift — expected symlink, found regular file"

**Given** a previously deployed module where the symlink points to a different source than expected
**When** the user runs `perch status`
**Then** the module is reported as "drift — symlink target mismatch"

**Given** all deployed modules have correct symlinks in place
**When** the user runs `perch status`
**Then** all modules are reported as "ok" and exit code is 0

### Story 3.3: File Locking Detection

As a developer,
I want Perch to detect when a target config file is locked by a running application,
So that I know which apps to close before deploy can complete.

**Acceptance Criteria:**

**Given** a target file is locked by a running application (e.g., an editor has the config open)
**When** the symlink engine attempts to process that module
**Then** the module is skipped with an Error result identifying the locked file
**And** processing continues to the next module

**Given** one or more modules were skipped due to locked files
**When** the deploy completes
**Then** a summary lists all locked files and suggests closing the relevant applications

### Story 3.4: Pre-Deploy Backup Snapshots

As a developer,
I want a full backup of all target files taken before deploy begins,
So that I can restore my previous state if something goes wrong.

**Acceptance Criteria:**

**Given** a config repo with modules targeting files that already exist
**When** the user runs `perch deploy` (not dry-run)
**Then** a timestamped snapshot directory is created (e.g., `%APPDATA%/perch/backups/2026-02-15T1030/`)
**And** all existing target files are copied into the snapshot before any symlinks are created

**Given** a deploy where no target files exist yet (fresh machine)
**When** the deploy runs
**Then** no snapshot is created (nothing to back up)

**Given** multiple deploys over time
**When** the user inspects the backup location
**Then** each deploy has its own timestamped snapshot directory

### Story 3.5: Structured JSON Output

As a developer,
I want to get deploy results as structured JSON,
So that I can pipe output to other tools or parse results in CI scripts.

**Acceptance Criteria:**

**Given** the user runs `perch deploy --output json`
**When** the deploy completes
**Then** the output is a valid JSON array of result objects with fields: moduleName, sourcePath, targetPath, level (info/warning/error), and message
**And** no Spectre.Console formatting or color codes are present in the output

**Given** the user runs `perch status --output json`
**When** the status check completes
**Then** the output is structured JSON with drift information per module

**Given** JSON output mode and exit code is non-zero
**When** the output is parsed
**Then** error details are included in the JSON results (not just on stderr)

### Story 3.6: Live Progress Table

As a developer,
I want to see a live-updating progress table during deploy showing per-module status,
So that I can track progress at a glance during large deploys.

**Acceptance Criteria:**

**Given** a config repo with many modules
**When** the user runs `perch deploy` (default pretty output)
**Then** a Spectre.Console live table displays showing each module's status (pending/in-progress/done/failed) updating in real-time

**Given** the deploy is in progress
**When** a module completes
**Then** the table row for that module updates with the result (success/skipped/error) and the next module shows as in-progress

**Given** the user runs with `--output json`
**When** the deploy runs
**Then** no live table is rendered (JSON mode suppresses interactive UI)

### Story 3.7: CI Pipeline Setup

As a developer,
I want a GitHub Actions CI pipeline that builds and tests on Windows and Linux,
So that every push is validated against both platforms with zero tolerance for warnings.

**Acceptance Criteria:**

**Given** a `.github/workflows/ci.yml` workflow
**When** a push or pull request targets the main branch
**Then** the pipeline runs `dotnet build` and `dotnet test` on both Windows and Linux runners

**Given** a build that produces analyzer warnings or compiler warnings
**When** the CI pipeline runs
**Then** the build fails (warnings treated as errors via project configuration)

**Given** any test fails
**When** the CI pipeline runs
**Then** the pipeline fails and reports which tests failed

## Epic 4: Package & App Awareness

Developer tracks installed packages across chocolatey/winget, discovers which installed apps lack config modules, and scans for managed vs unmanaged apps.

### Story 4.1: Package Manifest Definition

As a developer,
I want to define all my managed packages in a single manifest file specifying package name and manager,
So that Perch knows what should be installed on my machines.

**Acceptance Criteria:**

**Given** a `packages.yaml` file in the config repo listing packages with their manager (chocolatey/winget)
**When** the package manifest parser reads it
**Then** it returns a list of package definitions with name and manager type

**Given** a package manifest with invalid entries (missing name or unknown manager)
**When** the parser reads it
**Then** invalid entries are reported as errors and valid entries are still returned

### Story 4.2: Installed App Detection

As a developer,
I want Perch to scan installed apps via chocolatey and winget and cross-reference against my managed modules,
So that I can see which apps are managed, unmanaged, or missing.

**Acceptance Criteria:**

**Given** apps installed via chocolatey and winget
**When** the user runs `perch apps`
**Then** the system queries both package managers for installed packages
**And** cross-references installed packages against config repo modules
**And** displays a categorized list: managed (has module), installed-no-module, defined-not-installed

**Given** a package manager is not available on the system (e.g., no chocolatey on Linux)
**When** the scan runs
**Then** that manager is skipped with an Info message and available managers are still queried

### Story 4.3: Missing Config Module Reporting

As a developer,
I want to see which installed apps have no config module in my repo,
So that I know which apps to onboard next.

**Acceptance Criteria:**

**Given** 10 apps installed and only 6 have config modules
**When** the user runs `perch apps --unmanaged`
**Then** the 4 unmanaged apps are listed with their install source (chocolatey/winget)

**Given** all installed apps have config modules
**When** the user runs `perch apps --unmanaged`
**Then** a message confirms all installed apps are managed

## Epic 5: Git Integration & Deploy Hooks

Developer can suppress noisy config diffs via per-app git clean filters, diff filesystem changes before/after app tweaks, and run custom pre/post-deploy scripts per module.

### Story 5.1: Per-App Git Clean Filters

As a developer,
I want to register git clean filters that strip noisy fields from config files before git sees them,
So that my diffs only show meaningful changes.

**Acceptance Criteria:**

**Given** a module manifest with a `clean-filters` section specifying JSON paths or regex patterns to strip (e.g., `lastOpenedTimestamp`, `window.zoomLevel`)
**When** the user runs `perch git setup` (or deploy registers them automatically)
**Then** git clean filter entries are added to `.git/config` and `.gitattributes` for the relevant files

**Given** a clean filter is already registered for a file
**When** the setup runs again
**Then** no duplicate filter is created (idempotent)

**Given** a file tracked by a clean filter is staged
**When** git processes the file
**Then** the noisy fields are stripped from the staged version while the working copy remains unchanged

### Story 5.2: Before/After Filesystem Diffing

As a developer,
I want to capture filesystem state before and after tweaking an app's settings,
So that I can discover which files the app modified and where it stores config.

**Acceptance Criteria:**

**Given** the user runs `perch diff start` targeting a directory (e.g., `%AppData%\SomeApp`)
**When** a filesystem snapshot is captured
**Then** file paths, sizes, and hashes are recorded for all files in the target directory

**Given** a snapshot was previously captured with `perch diff start`
**When** the user runs `perch diff stop`
**Then** the system compares current state against the snapshot
**And** reports new files, modified files, and deleted files with their paths

### Story 5.3: Pre/Post-Deploy Lifecycle Hooks

As a developer,
I want to define scripts that run before and/or after a module is deployed,
So that I can automate setup tasks like importing plugin lists or running app-specific configuration.

**Acceptance Criteria:**

**Given** a module manifest with `hooks: { pre-deploy: "./setup.ps1" }` defined
**When** the deploy engine processes that module
**Then** the pre-deploy script runs before symlink creation
**And** if the script exits non-zero, the module is skipped with an Error result

**Given** a module manifest with `hooks: { post-deploy: "./import-plugins.ps1" }` defined
**When** the deploy engine completes symlink creation for that module
**Then** the post-deploy script runs after successful symlink creation

**Given** a module with no hooks defined
**When** the deploy engine processes it
**Then** no hook scripts are executed (hooks are optional)

**Given** a hook script path that does not exist
**When** the deploy engine attempts to run it
**Then** the module reports an Error result identifying the missing script

## Epic 6: Multi-Machine & System Tweaks Engine

Developer can define per-machine overrides, declaratively manage system tweaks via multi-mechanism definitions (registry YAML + PowerShell scripts + fonts), use the three-value model for drift detection and revert, and have app gallery entries own their tweaks. Detect-first flow scans current machine state before configuring.

### Story 6.1: Per-Machine Overrides

As a developer,
I want to define base config values with per-machine overrides keyed by hostname,
So that my laptop uses different fonts, git email, or settings than my desktop.

**Acceptance Criteria:**

**Given** a `machines/` directory with `base.yaml` and `laptop.yaml` (matching hostname)
**When** the user runs `perch deploy` on the laptop
**Then** the system identifies the machine by hostname
**And** merges `laptop.yaml` overrides on top of `base.yaml` values

**Given** a machine profile that overrides a manifest variable (e.g., `git-email: work@example.com`)
**When** a manifest references that variable
**Then** the override value is used instead of the base value

**Given** no machine profile matches the current hostname
**When** the user runs `perch deploy`
**Then** base values are used with an Info message that no machine-specific overrides were found

### Story 6.2: Module-to-Machine Filtering

As a developer,
I want to specify which modules deploy to which machines,
So that my work laptop skips gaming tools and my desktop skips work-only modules.

**Acceptance Criteria:**

**Given** a machine profile with `include-modules: [git, vscode, powershell]`
**When** deploy runs on that machine
**Then** only the listed modules are processed, all others are skipped

**Given** a machine profile with `exclude-modules: [steam, gaming-tools]`
**When** deploy runs on that machine
**Then** all modules except the excluded ones are processed

**Given** a machine profile with no include/exclude filters
**When** deploy runs
**Then** all modules are processed (default behavior)

### Story 6.3: Multi-Mechanism Tweak Definitions

As a developer,
I want to define system tweaks using registry YAML, PowerShell scripts, or both in a single entry,
So that tweaks like context menus (registry) and telemetry (PowerShell) use the same config model.

**Acceptance Criteria:**

**Given** a gallery entry with `registry:` keys defining HKCU/HKLM paths, names, values, and types
**When** the tweak engine processes the entry on Windows
**Then** the specified registry values are applied

**Given** a gallery entry with `script:` and `undo_script:` fields (PowerShell)
**When** the tweak engine processes the entry
**Then** the script runs to apply the tweak, and undo_script is stored for revert

**Given** a gallery entry with both `registry:` and `script:` sections
**When** the tweak engine processes the entry
**Then** both mechanisms are applied in order (registry first, then script)

**Given** a registry key already has the desired value
**When** deploy runs
**Then** no change is made (idempotent) and status is "Applied"

**Given** the engine is running on Linux or macOS
**When** registry/Windows tweak entries are encountered
**Then** they are skipped with an Info result ("Windows only")

### Story 6.4: Three-Value Model & State Capture

As a developer,
I want each managed registry key to track three values: Windows default, captured machine state (pre-Perch), and desired value,
So that I can detect drift and revert to either "my previous" or "Windows default".

**Acceptance Criteria:**

**Given** a gallery entry with `default_value:` (Windows default) and desired value defined
**When** Perch deploys to a machine for the first time
**Then** the current machine value is captured and stored in the per-machine manifest as the captured state before applying the desired value

**Given** a previously deployed tweak where the current registry value differs from desired
**When** the user runs `perch status`
**Then** the entry shows status "Drifted" with current, desired, and default values

**Given** a drifted or applied tweak
**When** the user reverts it
**Then** they can choose "restore my previous" (captured value) or "restore Windows default" (default_value)

**Given** all managed tweaks match desired state
**When** status runs
**Then** all entries show status "Applied"

### Story 6.5: Dependency Graph

As a developer,
I want gallery entries to declare `suggests:` (soft) and `requires:` (hard) dependency links,
So that Perch applies tweaks in the correct order and surfaces related items.

**Acceptance Criteria:**

**Given** a gallery entry with `requires: [other-entry-id]`
**When** the deploy engine builds the execution plan
**Then** the required entry is applied before the dependent entry

**Given** a gallery entry with `suggests: [other-entry-id]`
**When** the entry is displayed in the UI
**Then** suggested entries are shown as related recommendations (not enforced at deploy time)

**Given** a circular dependency in `requires:` chains
**When** the dependency resolver builds the graph
**Then** a validation error is reported identifying the cycle

### Story 6.6: App-Owned Tweaks

As a developer,
I want app gallery entries to include their bad behavior (context menu additions, startup entries, telemetry) as toggleable sub-items,
So that installing an app and cleaning up after it are managed together.

**Acceptance Criteria:**

**Given** an app gallery entry with a `tweaks:` section listing sub-items (e.g., "Disable context menu", "Remove startup entry")
**When** the entry is displayed in the UI
**Then** each sub-item appears as a toggleable child item under the app

**Given** the user enables an app-owned tweak sub-item
**When** deploy runs
**Then** the tweak is applied using its defined mechanism (registry or script)

**Given** the user disables an app-owned tweak sub-item
**When** the undo runs
**Then** the tweak is reverted using undo_script or by restoring the captured/default value

### Story 6.7: Detect-First Flow

As a developer,
I want Perch to scan my machine's current state before configuring,
So that on a new machine I can see what's already in place and only change what needs changing.

**Acceptance Criteria:**

**Given** a new machine with no prior Perch deploy
**When** the user runs `perch status` or launches the desktop wizard
**Then** the system scans installed apps, current registry values, existing symlinks, and installed fonts
**And** matches findings against gallery entries to show what's already in the desired state

**Given** an existing registry value matches a gallery entry's desired value
**When** detect-first scan runs
**Then** the entry shows status "Applied" (already correct, no action needed)

**Given** detect-first scan completes
**When** results are displayed
**Then** the user sees a clear summary: N already applied, M need attention, K not present

## Epic 7: Secrets Management

Developer can manage configs containing secrets via template placeholders resolved from 1Password at deploy time. Secret-containing files are generated (not symlinked) and git-ignored.

### Story 7.1: Secret Placeholder Syntax

As a developer,
I want to define secret placeholders in config template files using a clear syntax,
So that I can check in config structure without exposing actual secrets.

**Acceptance Criteria:**

**Given** a config template file containing `{{secret:op://Personal/nuget-token/password}}`
**When** the manifest parser identifies the file
**Then** it is flagged as a template requiring secret resolution (not a direct symlink target)

**Given** a template with multiple secret placeholders
**When** parsed
**Then** all placeholders are identified and listed for resolution

**Given** a config file with no secret placeholders
**When** parsed
**Then** it is treated as a normal symlink target (no template processing)

### Story 7.2: Password Manager Integration

As a developer,
I want Perch to resolve secret placeholders from 1Password CLI at deploy time,
So that credentials are injected into config files without ever being stored in git.

**Acceptance Criteria:**

**Given** a template with `{{secret:op://Personal/nuget-token/password}}` and 1Password CLI (`op`) is authenticated
**When** the deploy engine processes the template
**Then** the placeholder is replaced with the actual secret value from 1Password
**And** the result file is written to the target location as a regular file (not a symlink)

**Given** 1Password CLI is not installed or not authenticated
**When** the deploy engine encounters a template with secret placeholders
**Then** the module is skipped with an Error result explaining the prerequisite

**Given** a secret reference that does not exist in the vault
**When** resolution is attempted
**Then** the module fails with an Error result identifying the missing secret reference

### Story 7.3: Generated File Management

As a developer,
I want secret-containing config files to be generated (not symlinked) and automatically git-ignored,
So that secrets never accidentally end up in version control.

**Acceptance Criteria:**

**Given** a template is resolved and a generated file is written to the target location
**When** the file is created
**Then** it is a regular file (not a symlink) containing the resolved content

**Given** a generated file's target path
**When** the deploy completes
**Then** the target path is added to the config repo's `.gitignore` if not already present

**Given** a generated file already exists at the target
**When** deploy re-runs with updated template or secrets
**Then** the file is overwritten with the newly resolved content

## Epic 8: Advanced Usability

Developer can restore files from backups, run deploy interactively with step-level confirmation, use shell tab-completion, and define cross-platform packages.

### Story 8.1: Restore from Backup

As a developer,
I want to restore files from a previous backup snapshot or from `.backup` files,
So that I can undo a deploy if something went wrong.

**Dependencies:** Snapshot restore (`--snapshot`, `--list`) requires Epic 3 Story 3.4 (pre-deploy backup snapshots). Per-module `.backup` restore requires only Epic 1.

**Acceptance Criteria:**

**Given** backup snapshots exist from previous deploys
**When** the user runs `perch restore --list`
**Then** available snapshots are listed with timestamps and module counts

**Given** the user runs `perch restore --snapshot 2026-02-15T1030`
**When** the restore executes
**Then** symlinks are removed and original files are copied back from the snapshot
**And** results are streamed showing each restored file

**Given** a module has a `.backup` file but no snapshot
**When** the user runs `perch restore --module git`
**Then** the `.backup` file is restored to its original name and the symlink is removed

### Story 8.2: Interactive Deploy Mode

As a developer,
I want to run deploy with step-level confirmation prompts,
So that I can review and approve each action before it happens.

**Acceptance Criteria:**

**Given** the user runs `perch deploy --interactive`
**When** each module is about to be processed
**Then** the system displays what will happen (create symlink, backup file, skip) and prompts: [Y]es / [N]o / [A]ll / [Q]uit

**Given** the user selects "N" for a module
**When** processing continues
**Then** that module is skipped and the next module is presented

**Given** the user selects "A"
**When** processing continues
**Then** all remaining modules proceed without further prompts

**Given** the user selects "Q"
**When** processing stops
**Then** deploy halts gracefully (same as Ctrl+C behavior)

### Story 8.3: Shell Tab-Completion

As a developer,
I want tab-completion for Perch commands and options in my shell,
So that I can discover and type commands faster.

**Acceptance Criteria:**

**Given** the user runs `perch completion install` (or equivalent setup command)
**When** the shell completion script is installed
**Then** tab-completing `perch ` shows available commands (deploy, status, apps, restore, etc.)

**Given** tab-completion is installed
**When** the user types `perch deploy --` and presses tab
**Then** available flags are shown (--config-path, --dry-run, --output, --interactive)

**Given** completion is requested for an unsupported shell
**When** the install command runs
**Then** an error indicates which shells are supported

### Story 8.4: Cross-Platform Package Definitions

As a developer,
I want to define packages for apt, brew, VS Code extensions, npm globals, and other cross-platform managers in the same manifest format,
So that my package list works across all my machines regardless of OS.

**Acceptance Criteria:**

**Given** a `packages.yaml` with entries for apt, brew, vscode-extensions, and npm-global managers
**When** the package manifest parser reads it
**Then** all manager types are recognized and their packages are listed

**Given** the engine runs on macOS
**When** `perch apps` processes the manifest
**Then** brew packages are checked, apt packages are skipped (wrong platform), and cross-platform managers (vscode, npm) are checked

**Given** VS Code extensions listed in the manifest
**When** the user runs `perch apps`
**Then** installed extensions are cross-referenced against the manifest list

## Epic 9: App Onboarding & Discovery

Developer can discover config locations for new apps via AI lookup and Windows Sandbox isolation, generate manifests interactively, use version-range-aware paths, and pull templates from a community gallery.

### Story 9.1: Version-Range-Aware Manifest Paths

As a developer,
I want to specify version-range-aware target paths in a manifest,
So that I can manage apps that change config locations between major versions.

**Acceptance Criteria:**

**Given** a manifest with version-range paths (e.g., `v3.x: %AppData%\App\v3\config`, `v4.x: %AppData%\App\v4\config`)
**When** the engine detects the installed version of the app
**Then** the matching version-range path is used

**Given** no installed version can be detected
**When** the engine processes the module
**Then** a Warning result is returned suggesting manual version specification

### Story 9.2: AI Config Path Lookup

As a developer,
I want Perch to look up known config file locations for popular apps,
So that I don't have to manually hunt for where each app stores its settings.

**Acceptance Criteria:**

**Given** the user runs `perch discover <app-name>` for a well-known app
**When** the lookup service queries known config locations
**Then** it returns a list of known config file paths for that app on the current platform

**Given** the app is not in the known database
**When** the lookup runs
**Then** a message indicates no known locations and suggests using `perch diff` or sandbox discovery

### Story 9.3: Windows Sandbox Discovery

As a developer,
I want to launch an app in Windows Sandbox to discover its config file locations,
So that I can safely identify where an unfamiliar app stores settings without polluting my system.

**Acceptance Criteria:**

**Given** Windows Sandbox is available and the user runs `perch discover --sandbox <installer-path>`
**When** the sandbox session runs
**Then** the app is installed in the sandbox, a filesystem snapshot is taken before/after first launch, and discovered config paths are reported

**Given** Windows Sandbox is not available
**When** the user attempts sandbox discovery
**Then** an error explains that Windows Sandbox must be enabled and suggests `perch diff` as an alternative

### Story 9.4: Interactive Manifest Generation

As a developer,
I want to generate a new module manifest via an interactive CLI workflow,
So that I can quickly onboard a new app without writing YAML by hand.

**Acceptance Criteria:**

**Given** the user runs `perch init <app-name>`
**When** the interactive workflow starts
**Then** the system prompts for: source file(s) in config repo, target path(s), link type, platform(s), and optional hooks

**Given** the user completes all prompts
**When** the workflow finishes
**Then** a `manifest.yaml` is generated in a new module folder with the provided values
**And** the generated manifest is valid and parseable by the manifest parser

**Given** discovery results are available (from `perch discover`)
**When** the init workflow starts
**Then** discovered paths are pre-populated as defaults

### Story 9.5: Manifest Template Gallery

As a developer,
I want to pull pre-made manifest templates from a community gallery,
So that I can onboard popular apps without configuring paths manually.

**Acceptance Criteria:**

**Given** the user runs `perch template list`
**When** the gallery is queried
**Then** available templates are displayed with app name, platform support, and description

**Given** the user runs `perch template pull <app-name>`
**When** the template is fetched
**Then** a module folder is created with the template manifest and placeholder config files
**And** the user is prompted to review and customize before first deploy

**Given** the gallery is unreachable
**When** the user attempts to list or pull templates
**Then** an error indicates the gallery is unavailable and suggests manual manifest creation

## Epic 10: Desktop Wizard & Onboarding

User launches Perch Desktop for the first time and is guided through a wizard: profile selection, system detection of installed apps and dotfiles, card-based browsing and toggling, and deploy. The wizard is a complete standalone experience. Built with WPF UI + HandyControl on the shared Perch.Core engine.

### Story 10.1: Initialize Desktop Project & Shell

As a developer,
I want the Perch.Desktop WPF project scaffolded with all dependencies, DI host, and main window shell,
So that I have a buildable foundation for the desktop app with navigation infrastructure in place.

**Acceptance Criteria:**

**Given** the existing Perch solution
**When** the Desktop project is initialized
**Then** `Perch.Desktop` (WPF, net10.0-windows) and `Perch.Desktop.Tests` (NUnit) are added to the solution
**And** `Perch.Desktop` references `Perch.Core`
**And** `Perch.Desktop` has WPF-UI (4.2.0), HandyControl (3.5.1), CommunityToolkit.Mvvm (8.4.0), and Microsoft.Extensions.Hosting packages
**And** `App.xaml.cs` sets up Generic Host with DI: WPF UI services (`INavigationService`, `ISnackbarService`, `IContentDialogService`, `IThemeService`), Core services via `AddPerchCore()`, all pages + ViewModels
**And** `MainWindow.xaml` contains a WPF UI `NavigationView` sidebar with placeholder items (Home, Dotfiles, Apps, System Tweaks, Settings) and a content frame
**And** Dark Fluent theme is applied with forest green (#10B981) accent color
**And** `dotnet build` succeeds with zero warnings
**And** Startup routing logic checks for existing deploy state: first run → wizard, returning user → dashboard (placeholder pages for now)

### Story 10.2: Profile Selection

As a user,
I want to select my user profile(s) on first launch to personalize which wizard steps and content I see,
So that the experience is relevant to me (developer, power user, gamer, casual).

**Acceptance Criteria:**

**Given** the wizard launches on first run
**When** the Profile Selection step loads
**Then** profile cards are displayed in a grid: Developer, Power User, Gamer, Casual
**And** each `ProfileCard` shows a Midjourney hero image background, profile name, and brief tagline
**And** cards support multi-select (user can pick multiple profiles)

**Given** the user selects Developer + Gamer profiles
**When** they click Next
**Then** the wizard shows steps for Dotfiles, Apps, and System Tweaks (union of selected profiles)
**And** the StepBar header reflects only the included steps

**Given** the user selects only Casual
**When** they click Next
**Then** the Dotfiles step is skipped (not shown in StepBar) — wizard goes directly to Apps

**Given** no profiles are selected
**When** the user tries to proceed
**Then** the Next button is disabled

### Story 10.3: Card-Based Config Views

As a user,
I want to see my detected apps and dotfiles as visual cards that I can toggle on/off,
So that I can choose which configs to manage through an intuitive browsing experience.

**Acceptance Criteria:**

**Given** the Dotfiles or Apps wizard step loads
**When** the view populates
**Then** detected items appear as `AppCard` controls in a card grid with: app icon (24-32px), name, one-line description, and `StatusRibbon` showing detection status
**And** cards are organized in three tiers via `TierSectionHeader`: "Your Apps" (detected), "Suggested for You" (profile-based), "Other Apps" (gallery)

**Given** detected configs on the filesystem (e.g., `.gitconfig` exists at `%UserProfile%`)
**When** the detection service scans
**Then** matching cards appear in the "Your Apps" tier with status "Detected - Not managed"

**Given** the user clicks a card's toggle
**When** the toggle activates
**Then** the card's `StatusRibbon` changes to "Selected" (accent green border)
**And** a running count is visible: "5 items selected"

**Given** the user clicks a card body (not the toggle)
**When** the card expands via `CardExpander`
**Then** config file paths, target paths, and options are shown inline
**And** for app entries with owned tweaks, toggleable sub-items (context menus, startup entries) are shown

**Given** a search term is entered in the search bar
**When** the filter applies
**Then** all three tiers are filtered simultaneously by name/description match

**Given** the user toggles the density control (grid/list icon)
**When** the view mode switches
**Then** cards switch between grid layout (icon-prominent) and compact list layout (horizontal rows)

**Given** the `AppsView` and `DotfilesView` UserControls
**When** used in wizard steps
**Then** the same controls can also be hosted in dashboard pages without modification

### Story 10.4: Wizard Flow & Deploy

As a user,
I want to progress through the wizard steps, review my selections, and deploy all chosen configs in one action,
So that my machine is configured and I feel a sense of completion.

**Acceptance Criteria:**

**Given** the wizard is active
**When** the user navigates between steps
**Then** a HandyControl `StepBar` at the top shows current progress with step labels
**And** Back/Next/Skip buttons appear in the footer
**And** step count and labels are dynamic based on profile selection (e.g., 4 steps for Developer, 3 for Casual)

**Given** the System Tweaks wizard step loads
**When** the detect-first scan runs
**Then** current machine state (registry values, installed fonts, startup entries) is scanned and matched against gallery
**And** tweaks already in desired state show "Applied" status; others show "Not Applied" with mechanism-aware StatusRibbon

**Given** the user reaches the Review & Deploy step
**When** the review page loads
**Then** a summary shows all selected items across all steps: "X dotfiles, Y apps, Z tweaks selected"
**And** a Deploy button is prominently displayed (WPF UI Primary appearance, accent green)

**Given** the user clicks Deploy
**When** the deploy executes via `IDeployService.DeployAsync()` with `IProgress<DeployResult>`
**Then** each card shows a `ProgressRing` overlay during its deploy
**And** cards update to show results as they complete (green StatusRibbon = linked, red = error)
**And** the `DeployBar` at the bottom shows aggregate progress

**Given** the deploy completes
**When** the completion page loads
**Then** a summary shows: "X configs linked, Y apps configured, Z items skipped"
**And** two options are presented: "Open Dashboard" or "Close"

**Given** the user closes the wizard (including mid-flow via window close)
**When** some items were already deployed
**Then** already-deployed items remain linked — no rollback on incomplete wizard

## Epic 11: Desktop Dashboard & Drift

Returning user opens Perch Desktop and sees a drift-focused dashboard: hero banner with config health summary, mechanism-aware smart status cards, one-click fix actions. GalleryTreeView for unified tree browsing. TweakDetailPanel with three-value inline display and Open Location. App-owned tweak sub-items toggleable within app cards.

### Story 11.1: Dashboard Home & Drift Summary

As a returning user,
I want to see a drift-focused dashboard when I open Perch Desktop,
So that I can instantly assess my config health and resolve any issues.

**Acceptance Criteria:**

**Given** the user has previously completed the wizard (deploy state exists)
**When** Perch Desktop launches
**Then** the app opens to the Dashboard Home page (not the wizard)
**And** module state is loaded from the filesystem at startup

**Given** the Dashboard Home loads
**When** the drift check completes
**Then** a `DriftHeroBanner` spans the top showing aggregate counts: "X linked - Y attention - Z broken"
**And** the banner state reflects health: all green = calm ("Everything looks good"), issues = attention/critical styling
**And** status uses mechanism-aware vocabulary: Linked/Broken for dotfiles, Applied/Drifted for tweaks, Installed for apps/fonts

**Given** modules with issues exist
**When** the Dashboard Home loads
**Then** attention cards appear below the hero, grouped by severity: broken (red) > attention (yellow) > info (blue)
**And** each card shows the module name, status, and a one-click action (Link / Fix / Re-deploy)

**Given** the user clicks a one-click action on an attention card
**When** the action executes
**Then** the card's `StatusRibbon` updates immediately (e.g., red → green)
**And** the hero banner counter updates ("Z broken" decreases)

**Given** destructive actions (Unlink, Restore from repo)
**When** the user clicks the action
**Then** a brief confirmation dialog appears: one sentence explaining what happens, Cancel / Confirm buttons

### Story 11.2: Dashboard Card Pages

As a returning user,
I want to navigate to dedicated Apps, Dotfiles, and System Tweaks pages from the sidebar,
So that I can browse and manage specific config categories in detail.

**Acceptance Criteria:**

**Given** the dashboard is active
**When** the user clicks "Apps" in the NavigationView sidebar
**Then** the `AppsPage` loads with `GalleryTreeView` for category navigation and `AppCard` controls with mechanism-aware `StatusRibbon`
**And** app entries with owned tweaks show expandable sub-items for their bad behavior toggles

**Given** the user clicks "System Tweaks" in the sidebar
**When** the `SystemTweaksPage` loads
**Then** `GalleryTreeView` shows the tweak category tree (Explorer, Privacy, Power, Context Menus, Startup, etc.)
**And** selecting a tweak shows `TweakDetailPanel` with three-value inline display (current / desired / default) and an "Open Location" button

**Given** the user clicks "Open Location" on a tweak
**When** the action executes
**Then** the appropriate tool opens: regedit for registry tweaks, Explorer for startup folder, services.msc for services, Fonts folder for fonts

**Given** the user clicks "Dotfiles" in the sidebar
**When** the `DotfilesPage` loads
**Then** the shared `DotfilesView` UserControl is displayed with detection-first three-tier layout
**And** "Dotfiles" only appears in the sidebar if a Developer profile was selected during wizard

**Given** the user toggles cards on in a dashboard card page
**When** items are selected
**Then** a `DeployBar` slides up at the bottom: "N items selected" with a Deploy button
**And** clicking Deploy runs `IDeployService.DeployAsync()` with the same progress/feedback pattern as the wizard

**Given** sidebar navigation between pages
**When** the user switches between pages
**Then** page state is preserved (scroll position, expanded cards, selections) via Singleton page lifetime

### Story 11.3: Settings & Configuration

As a user,
I want to access settings for Perch Desktop including config repo path, profile, and display preferences,
So that I can reconfigure the app without starting over.

**Acceptance Criteria:**

**Given** the user navigates to Settings via the sidebar footer item
**When** the Settings page loads
**Then** config repo path is displayed and editable (same setting as CLI's `--config-path`)
**And** current profile selection is shown with an option to change it
**And** display density preference (grid/list) is shown with a toggle

**Given** the user changes the config repo path
**When** the change is saved
**Then** the settings are persisted to `%APPDATA%/perch/settings.yaml` (same file as CLI)
**And** the app reloads module state from the new path

**Given** the user wants to re-run the wizard
**When** they click "Re-run Setup Wizard" in Settings
**Then** the wizard launches from the Profile Selection step

## Epic 12: Migration Tools

Users of chezmoi, Dotbot, or Dotter can import their dotfiles repo into Perch format, and Perch users can export back — enabling two-way migration.

### Story 12.1: Chezmoi Import

As a developer migrating from chezmoi,
I want to import my chezmoi-managed dotfiles repo into Perch format,
So that I can switch to Perch without manually recreating all my module manifests.

**Acceptance Criteria:**

**Given** the user runs `perch import chezmoi <source-dir>`
**When** the import scans the chezmoi source directory
**Then** `dot_` prefixed files are converted to Perch module folders with manifests
**And** templates with simple variable substitution are resolved to plain config files
**And** templates with complex logic (scripts, conditionals) are flagged for manual review

**Given** the import completes
**When** the user runs `perch deploy`
**Then** the imported modules deploy correctly

### Story 12.2: Dotbot and Dotter Import

As a developer migrating from Dotbot or Dotter,
I want to import my dotfiles repo into Perch format,
So that I can switch tools without losing my configuration.

**Acceptance Criteria:**

**Given** the user runs `perch import dotbot <source-dir>`
**When** the import processes the Dotbot YAML config
**Then** link directives are converted to Perch module manifests
**And** shell commands are converted to lifecycle hooks where possible

**Given** the user runs `perch import dotter <source-dir>`
**When** the import processes the Dotter config
**Then** file mappings are converted to Perch module manifests

### Story 12.3: Export to Other Formats

As a developer,
I want to export my Perch config repo to chezmoi, Dotbot, or Dotter format,
So that I can migrate away or share configs with users of other tools.

**Acceptance Criteria:**

**Given** the user runs `perch export chezmoi <output-dir>`
**When** the export processes all Perch modules
**Then** module folders are converted to chezmoi `dot_` format with appropriate directory structure

**Given** the user runs `perch export dotbot <output-dir>`
**When** the export processes all Perch modules
**Then** a Dotbot YAML config is generated with link directives matching the Perch manifests

**Given** the exported config
**When** the user runs the target tool against it
**Then** the same config files are linked to the same target locations

## Epic 13: Scoop Integration

Developer manages dev tools via Scoop from the config repo. Buckets and apps are declared in `scoop.yaml`, installed idempotently, and exportable. Scoop's predictable install paths integrate with config module discovery.

### Story 13.1: Scoop App Manifest

As a developer,
I want to define my Scoop buckets and apps in a `scoop.yaml` file in my config repo,
So that my dev tool list is version-controlled and portable across machines.

**Acceptance Criteria:**

**Given** a `scoop.yaml` file in the config repo with `buckets` and `apps` sections
**When** the Scoop manifest parser reads it
**Then** it returns a list of bucket names and app definitions (name, optional bucket source)

**Given** a `scoop.yaml` with invalid entries (missing app name, malformed YAML)
**When** the parser reads it
**Then** invalid entries are reported as errors and valid entries are still returned

**Given** a `scoop.yaml` with apps specifying their bucket (e.g., `extras/ripgrep`)
**When** parsed
**Then** the bucket association is preserved in the app definition

### Story 13.2: Declarative Bucket & App Installation

As a developer,
I want to run `perch scoop install` and have all listed buckets added and apps installed,
So that a fresh machine gets all my dev tools with one command.

**Acceptance Criteria:**

**Given** a `scoop.yaml` listing buckets `[extras, nerd-fonts]` and apps `[ripgrep, fd, fzf, delta, bat]`
**When** the user runs `perch scoop install`
**Then** each bucket is added via `scoop bucket add` (skipped if already added)
**And** each app is installed via `scoop install` (skipped if already installed)
**And** results are streamed to the console with status indicators (installed/skipped/failed)

**Given** Scoop is not installed on the system
**When** the user runs `perch scoop install`
**Then** an error explains that Scoop must be installed first and provides the install URL

**Given** an app install fails (e.g., network error, unknown app)
**When** processing continues
**Then** the failed app is reported as an Error result and remaining apps are still processed (fault isolation)

**Given** all listed apps are already installed
**When** the user runs `perch scoop install`
**Then** all apps are skipped with Info results and exit code is 0

### Story 13.3: Scoop App Export & Sync

As a developer,
I want to export my currently installed Scoop apps and diff against my manifest,
So that I can discover new tools I've installed but haven't tracked yet.

**Acceptance Criteria:**

**Given** the user has 20 Scoop apps installed and `scoop.yaml` lists 15
**When** the user runs `perch scoop export`
**Then** the 5 untracked apps are listed with their bucket source
**And** the user is offered to add them to `scoop.yaml`

**Given** the user confirms adding untracked apps
**When** the export completes
**Then** `scoop.yaml` is updated with the new entries

**Given** `scoop.yaml` lists apps that are not installed
**When** the user runs `perch scoop status`
**Then** missing apps are reported so the user can install them or remove them from the manifest

### Story 13.4: Scoop Path-Based Config Discovery

As a developer,
I want Perch to leverage Scoop's predictable install paths to find app config files,
So that onboarding a Scoop-installed app's config is easier.

**Acceptance Criteria:**

**Given** an app installed via Scoop at `~/scoop/apps/<name>/current/`
**When** the user runs `perch discover <name>` and the app is Scoop-installed
**Then** the system checks `~/scoop/apps/<name>/current/` for config files (JSON, YAML, INI, TOML)
**And** discovered config paths are reported as candidates for a new module

**Given** the app stores config outside the Scoop directory (e.g., in `%AppData%`)
**When** discovery runs
**Then** both the Scoop install path and standard config locations are checked

## Epic 14: Gallery Schema Evolution

Gallery YAML format evolves to support the unified tree taxonomy, type system, OS-aware filtering, and dependency graph. Gallery becomes the source of truth — user manifests store only deviations. Index auto-generated.

### Story 14.1: Gallery Source of Truth & Manifest Deviations

As a developer,
I want the gallery to define all defaults so my personal manifest only stores what I've changed,
So that my config stays minimal and gallery updates flow through automatically.

**Acceptance Criteria:**

**Given** a gallery entry defining `desired_value: 0` for a tweak
**When** the user's manifest has no override for that entry
**Then** the gallery value is used as the desired value

**Given** a user's manifest with an override `desired_value: 1` for the same entry
**When** the merge engine resolves the final config
**Then** the user's override takes precedence over the gallery default

**Given** the gallery updates a default value
**When** the user has no override for that entry
**Then** the new gallery default is automatically picked up on next deploy

### Story 14.2: Unified Type Field & Base Schema

As a developer,
I want gallery entries to declare `type: app | tweak | font` with a shared base schema,
So that the system knows how to process each entry while keeping the format consistent.

**Acceptance Criteria:**

**Given** a gallery entry with `type: tweak`
**When** the schema validator processes it
**Then** tweak-specific fields (`registry:`, `script:`, `undo_script:`, `default_value:`) are expected

**Given** a gallery entry with `type: app`
**When** the schema validator processes it
**Then** app-specific fields (`package:`, `tweaks:`) are expected alongside shared fields (name, description, category)

**Given** a gallery entry with `type: font`
**When** the schema validator processes it
**Then** font-specific fields (font family, file path) are expected

**Given** a gallery entry missing the `type:` field
**When** the schema validator processes it
**Then** a validation error is reported

### Story 14.3: Tree Taxonomy & Category Paths

As a developer,
I want gallery entries organized in deep category paths with sort order,
So that the unified tree is navigable and logically structured.

**Acceptance Criteria:**

**Given** gallery entries with `category: Apps/Languages/.NET/Editors/Visual Studio`
**When** the tree builder processes all entries
**Then** a navigable tree is constructed with proper parent-child nesting

**Given** categories with `sort: 10`, `sort: 20`, etc.
**When** the tree is displayed
**Then** categories appear in sort-value order, not alphabetical

**Given** a category with only 2 entries
**When** the tree is constructed
**Then** the convention of "don't create subcategories for 2 items" is flagged as a validation warning (not enforced at runtime)

### Story 14.4: OS Version & Restart Metadata

As a developer,
I want gallery entries to declare supported Windows versions and restart requirements,
So that the UI can filter irrelevant entries and warn about restarts.

**Acceptance Criteria:**

**Given** a gallery entry with `windows_versions: [11]` on a Windows 10 machine
**When** the Desktop UI loads the tree
**Then** the entry is hidden (not greyed out, fully absent from the tree)

**Given** a gallery entry with `windows_versions: [10, 11]` on a Windows 11 machine
**When** the Desktop UI loads
**Then** the entry is visible

**Given** a gallery entry with `restart_required: true`
**When** the entry is displayed in the UI
**Then** a restart indicator is shown on the card/detail view

**Given** multiple entries with `restart_required: true` are selected for deploy
**When** deploy completes
**Then** a summary notification indicates a restart is needed

### Story 14.5: Auto-Generated Index

As a developer,
I want `index.yaml` to be automatically generated from the catalog folder structure,
So that I never need to manually maintain it.

**Acceptance Criteria:**

**Given** a catalog folder with app/, tweak/, font/ subdirectories containing YAML files
**When** the index generator runs (as a build step or pre-commit hook)
**Then** `index.yaml` is generated listing all entries with their type, name, and category path

**Given** a new gallery entry is added to the catalog
**When** the index generator re-runs
**Then** the new entry appears in the generated index

**Given** `index.yaml` already exists with stale content
**When** the generator runs
**Then** it is overwritten with current contents (not appended)

## Epic 15: Gallery Import & Sourcing

One-time import tooling to populate the gallery from external sources (WinUtil, Sophia Script). License compliance checks. CI validation of registry paths across Windows versions.

### Story 15.1: WinUtil Importer

As a developer,
I want a script that parses WinUtil's `tweaks.json` and generates gallery YAML stubs,
So that I can import ~65 tweak definitions for human review without manual transcription.

**Acceptance Criteria:**

**Given** WinUtil's `tweaks.json` file as input
**When** the importer script runs
**Then** each tweak entry is converted to a gallery YAML file with: name, description, category, mechanism (registry/script), registry keys, original values, and undo information

**Given** a WinUtil entry with multiple mechanisms (registry + service + scheduledTask)
**When** the importer processes it
**Then** registry entries go into `registry:` section, service/task logic goes into `script:` and `undo_script:` sections

**Given** generated YAML stubs
**When** a human reviewer inspects them
**Then** each stub is marked as `verified: false` requiring manual testing before inclusion in the gallery

### Story 15.2: Sophia Script Importer

As a developer,
I want to import Sophia Script's ~270 PowerShell functions as gallery entries,
So that the gallery covers a broad range of Windows tweaks beyond what WinUtil provides.

**Acceptance Criteria:**

**Given** Sophia Script source files as input
**When** the importer parses -Enable/-Disable function pairs
**Then** each pair generates a gallery entry with `script:` (Enable function body) and `undo_script:` (Disable function body)

**Given** an imported Sophia entry that overlaps with an existing gallery entry (from WinUtil or manual)
**When** the dedup check runs
**Then** the overlap is flagged for human review (same registry key, similar description, etc.)

**Given** Sophia's 8 Windows version variants
**When** the importer processes entries
**Then** `windows_versions:` is populated based on which variants include the function

### Story 15.3: License & Compliance Check

As a developer,
I want the import pipeline to check LICENSE files of source repos,
So that I know which entries can be included in the gallery and under what terms.

**Acceptance Criteria:**

**Given** a source repo with an MIT license
**When** the license checker runs
**Then** the result indicates compatible license with attribution required

**Given** a source repo with a restrictive or missing license
**When** the license checker runs
**Then** the result flags the repo as requiring manual review before importing entries

**Given** imported entries from a licensed source
**When** the gallery YAML is generated
**Then** source attribution is included in the entry metadata

### Story 15.4: CI Registry Path Validation

As a developer,
I want a GitHub Action that validates every registry path in the gallery exists on real Windows versions,
So that stale or wrong registry paths are caught automatically.

**Acceptance Criteria:**

**Given** a GitHub Actions workflow with a matrix of Windows versions (10, 11)
**When** the validation job runs
**Then** every `registry:` key path in the gallery is checked for existence on that Windows version

**Given** a registry path that exists on Windows 11 but not Windows 10
**When** the validation runs on Windows 10
**Then** the path is reported as missing, but only fails if the entry claims `windows_versions: [10]`

**Given** all registry paths validate successfully
**When** the CI job completes
**Then** the job passes with a summary: "N registry paths validated on Windows X"

**Given** the validation runs on a schedule (e.g., monthly)
**When** a previously valid path no longer exists (Windows update removed it)
**Then** the CI fails and an issue is flagged for review

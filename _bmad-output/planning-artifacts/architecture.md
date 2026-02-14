---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
lastStep: 8
status: 'complete'
completedAt: '2026-02-14'
inputDocuments:
  - '_bmad-output/planning-artifacts/prd.md'
  - '_bmad-output/planning-artifacts/prd-validation-report.md'
  - '_bmad-output/planning-artifacts/competitive-research.md'
  - '_bmad-output/planning-artifacts/chezmoi-comparison.md'
  - '_bmad-output/brainstorming/brainstorming-session-2026-02-08.md'
  - '_bmad-output/brainstorming/brainstorming-session-2026-02-14.md'
workflowType: 'architecture'
project_name: 'Perch'
user_name: 'Wouter'
date: '2026-02-14'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**

48 FRs across 12 categories. Scope 1 (MVP) FRs are well-defined and tightly scoped:

- **Manifest & Module Management** (FR1-FR3, FR41-FR42): Convention-over-config discovery, co-located manifests with platform-aware target paths
- **Symlink Engine** (FR7-FR8): Create symlinks/junctions, backup existing files, re-runnable (additive only)
- **CLI Interface** (FR14-FR17): Deploy command, colored streaming output, clean exit codes, graceful Ctrl+C
- **Engine Configuration** (FR39-FR40): Config repo location via CLI argument, persisted for subsequent runs

Scope 2-3 FRs add surface area (cross-platform, registry, secrets, MAUI, package management, lifecycle hooks) but don't change the core architectural shape — they extend it.

**Non-Functional Requirements:**

- **Reliability:** Graceful shutdown (current module completes on Ctrl+C), fault isolation (one module failure doesn't block others), missing directories reported and skipped
- **Maintainability:** Platform-specific logic behind interfaces, core engine depends only on abstractions. TDD, KISS/YAGNI
- **Portability:** .NET 10 runtime, Windows 10+ (Scope 1), cross-platform (Scope 2), shell-agnostic, plain files + JSON manifests

**Scale & Complexity:**

- Primary domain: CLI tool / system utility (filesystem operations, platform APIs)
- Complexity level: Low
- Estimated architectural components: ~5-7 for Scope 1 (manifest parser, module discovery, symlink engine, platform abstraction, CLI layer, configuration, backup handler)

### Technical Constraints & Dependencies

- **C# / .NET 10** — chosen stack, distributed as `dotnet tool`
- **Spectre.Console** — CLI output rendering
- **NUnit + NSubstitute** — testing (Scope 2 CI, but TDD from Scope 1)
- **Engine/config repo split** — engine is open-sourceable, config is personal. Engine locates config via CLI arg or persisted setting
- **No hard-coded Windows assumptions in core engine** — even in Scope 1 Windows-only MVP, platform logic goes behind interfaces
- **Symlink-first philosophy** — core differentiator. Architecture must never compromise the "change a setting, it's immediately in git" workflow
- **Manifest format: JSON** — co-located with config files, minimum fields: source path(s), target path(s) per platform, link type

### Cross-Cutting Concerns Identified

- **Platform abstraction:** Core engine depends on interfaces (e.g., `IFileSystemOperations`, `ISymlinkProvider`). Windows implementation in Scope 1, Linux/macOS in Scope 2. Platform-specific classes tested with real filesystem; everything else mocked
- **Error handling & fault isolation:** Per-module independence. Failed modules logged + displayed, others continue. No partial state on cancellation
- **Idempotency:** Every operation checks current state before acting. Re-run produces zero changes if nothing changed
- **Testability:** Interface-based design enables NSubstitute mocking. Real filesystem tests only for platform implementations
- **Graceful cancellation:** CancellationToken threaded through engine operations. Current module completes fully before halting

## Starter Template Evaluation

### Primary Technology Domain

CLI tool / system utility — C# / .NET 10. No competing starter ecosystem; `dotnet new` provides all scaffolding.

### Verified Versions (Feb 2026)

| Package | Version | Notes |
|---|---|---|
| .NET 10 | GA (LTS) | Released Nov 2025, supported until Nov 2028 |
| Spectre.Console | 0.54.0 | CLI output rendering |
| Spectre.Console.Cli | 0.53.1 | Command/argument parsing |
| NUnit | 4.4.0 | NUnit 4 — modernized assertion model |
| NSubstitute | 5.3.0 | Interface mocking |

### Solution Structure

```
Perch.sln
├── src/
│   ├── Perch.Core/          # Engine library (classlib) — all logic, interfaces, models
│   └── Perch.Cli/           # Console app — Spectre.Console, references Core
└── tests/
    └── Perch.Core.Tests/    # NUnit + NSubstitute
```

**Perch.Core** (classlib): Manifest parsing, module discovery, symlink engine, platform interfaces + Windows implementation, configuration, backup. Zero UI/console dependencies.

**Perch.Cli** (console): Spectre.Console rendering, command definitions, DI wiring, references Perch.Core. Packed as `dotnet tool`.

**Perch.Core.Tests** (nunit): Unit tests against Core. Platform-specific code tested with real filesystem; everything else mocked via interfaces.

**Why not more projects?** Platform implementations live in Core behind interfaces until Scope 2 adds Linux/macOS — extract then if needed. No CLI tests yet — CLI is thin, logic lives in Core.

### Initialization

```bash
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
```

**Note:** Project initialization using these commands should be the first implementation story.

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

## Implementation Patterns & Consistency Rules

### Naming Patterns

- **Interfaces:** `I<Noun>Provider` for abstractions over external resources (e.g., `ISymlinkProvider`, `IFileBackupProvider`). `I<Noun>Service` for orchestration (e.g., `IDeployService`, `IModuleDiscoveryService`)
- **YAML manifest properties:** `kebab-case` (e.g., `target-path`, `link-type`, `source-files`)
- **Namespaces:** Match folder structure under `Perch.Core` (e.g., `Perch.Core.Modules`, `Perch.Core.Symlinks`)

### Project Organization

Feature folders in Perch.Core — related code together:

```
Perch.Core/
├── Modules/        # Manifest parsing, module discovery, Module model
├── Symlinks/       # ISymlinkProvider, WindowsSymlinkProvider, symlink logic
├── Deploy/         # DeployService, DeployResult, pipeline orchestration
├── Config/         # Settings loading/persistence, path resolution
└── Backup/         # File backup before symlink creation
```

Interfaces live in the same feature folder as their implementations.

### DI Lifetimes

- **Singleton:** Stateless services (manifest parser, symlink provider, settings loader)
- **Transient:** Stateful or per-operation objects (deploy context, result collectors)
- **No Scoped:** No request scope in a CLI tool

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
│   │   └── ServiceCollectionExtensions.cs  # AddPerchCore() DI registration
│   └── Perch.Cli/
│       ├── Perch.Cli.csproj
│       ├── Program.cs                 # Entry point, DI setup
│       └── Commands/
│           └── DeployCommand.cs       # Spectre.Console.Cli command + rendering
└── tests/
    └── Perch.Core.Tests/
        ├── Perch.Core.Tests.csproj
        ├── Modules/
        │   └── ModuleDiscoveryServiceTests.cs
        ├── Deploy/
        │   └── DeployServiceTests.cs
        ├── Config/
        │   └── YamlSettingsProviderTests.cs
        └── Symlinks/
            └── WindowsSymlinkProviderTests.cs  # Real filesystem tests
```

### FR to Structure Mapping

| FR Category | Location |
|---|---|
| FR1-FR3 Manifest & Module Management | `Perch.Core/Modules/` |
| FR7-FR8 Symlink Engine | `Perch.Core/Symlinks/` + `Perch.Core/Backup/` |
| FR14-FR17 CLI Interface | `Perch.Cli/Commands/` |
| FR39-FR40 Engine Configuration | `Perch.Core/Config/` |
| FR41-FR42 Platform-aware manifests | `Perch.Core/Modules/AppManifest.cs` (Scope 2 paths) |

### Architectural Boundaries

**Perch.Core → Perch.Cli:** One-way dependency. Core has zero knowledge of CLI. Core exposes services and result types; CLI renders them.

**Deploy pipeline data flow:**
```
CLI parses args → resolves settings → calls IDeployService.DeployAsync()
  → IModuleDiscoveryService discovers modules
  → per module: ISymlinkProvider checks state → IFileBackupProvider backs up → ISymlinkProvider creates link
  → returns IReadOnlyList<DeployResult>
CLI renders results via Spectre.Console
```

**External boundary:** Filesystem only. No network, no database, no external services in Scope 1.

## Architecture Validation

**Coherence:** Pass — all technology choices, patterns, and structure aligned.

**Scope 1 FR Coverage:** All 11 Scope 1 FRs mapped to specific architectural components.

**NFR Coverage:** Graceful shutdown (CancellationToken), fault isolation (per-module results), platform abstraction (ISymlinkProvider), testability (interfaces + mocks) — all addressed.

**Gaps:** None critical. Path resolution (`%AppData%` expansion) starts as a simple function — no premature abstraction.

**Status:** READY FOR IMPLEMENTATION

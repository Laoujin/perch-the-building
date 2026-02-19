---
project_name: 'Perch'
user_name: 'VANSCHWO'
date: '2026-02-19'
sections_completed: ['technology_stack', 'language_rules', 'framework_rules', 'testing_rules', 'code_quality', 'workflow', 'critical_rules']
status: 'complete'
rule_count: 62
optimized_for_llm: true
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing code in this project. Focus on unobvious details that agents might otherwise miss._

---

## Technology Stack & Versions

### Core Platform
- **.NET 10.0** (LTS) — `net10.0` for cross-platform, `net10.0-windows` for Desktop
- **C# latest** — file-scoped namespaces (enforced), records, primary constructors
- **Central Package Management** — versions ONLY in `Directory.Packages.props`, versionless `<PackageReference>` in `.csproj`
- **ImplicitUsings=disable** — explicit `GlobalUsings.cs` per project; agents must add `using` directives or check GlobalUsings
- **TreatWarningsAsErrors=true** — zero tolerance; unused vars, missing modifiers, boxing = build failure
- **Roslynator 4.15.0** + editorconfig analyzers enforced at build time

### Key Dependencies
| Project | Package | Version |
|---|---|---|
| Core | YamlDotNet | 16.3.0 |
| Core | MS DI Abstractions | 10.0.3 |
| CLI | Spectre.Console | 0.54.0 |
| CLI | Spectre.Console.Cli | 0.53.1 |
| Desktop | WPF-UI | 4.2.0 |
| Desktop | HandyControl | 3.5.1 |
| Desktop | CommunityToolkit.Mvvm | 8.4.0 |
| Desktop | MS Hosting | 10.0.0-preview.1 (⚠️ preview) |
| Tests | NUnit | 4.4.0 |
| Tests | NSubstitute | 5.3.0 |
| Tests | NUnit3TestAdapter | 5.0.0 (supports NUnit 4 — not a mismatch) |
| Smoke | FlaUI.Core / FlaUI.UIA3 | 5.0.0 |

### Build & CI Constraints
- **Two solution files:** `Perch.CrossPlatform.slnx` (Linux CI) excludes Desktop; `Perch.slnx` (Windows) is full
- **Perch.Core has zero UI dependencies** — adding Spectre or WPF references breaks Linux CI
- **Perch.Core.Tests** conditionally targets `net10.0-windows` and defines `DESKTOP_TESTS` — Desktop ViewModel tests must use `#if DESKTOP_TESTS`
- **Smoke tests excluded from CI** — FlaUI tests run locally only
- **CA1707 suppressed in test projects** — underscore test names OK in tests, not in production code

### Editorconfig Enforced Rules
- File-scoped namespaces (warning = error)
- Private fields: `_camelCase`; private static: `s_camelCase`
- Braces on all blocks preferred
- `var` only when type is apparent; explicit types for built-in types
- Cyclomatic complexity warning (CA1502)

## Critical Implementation Rules

### Language-Specific Rules (C#)

**Class & Type Patterns:**
- Classes are `sealed` by default — `public sealed class`, `public sealed record`
- Result types use static factory methods: `Result.Success(value)` / `Result.Failure(error)` — no exceptions for expected failures
- Domain models are `sealed record` with positional parameters: `AppModule(string Name, ...)`
- Settings/config models use `sealed record` with `init` properties
- `ImmutableArray<T>` and `ImmutableDictionary<K,V>` for collection properties on records — not `List<T>`

**Constructor & DI Patterns:**
- Traditional constructor injection with `_camelCase` readonly fields on services — not primary constructors
- Primary constructors only on small utility types
- All dependencies injected via interfaces

**Async & Threading:**
- `CancellationToken cancellationToken = default` on all public async APIs
- Async methods suffixed with `Async`
- `IProgress<T>` for reporting progress from Core to UI layers

**Error Handling:**
- No exceptions for expected failures — use result types with `ResultLevel` (Ok/Warning/Error)
- Try-catch only for truly unexpected failures (e.g., YAML deserialization)
- Failed operations return error results; callers decide how to present

**YAML Integration:**
- `HyphenatedNamingConvention` for YAML deserialization (manifest properties are `kebab-case`)
- `IgnoreUnmatchedProperties()` for forward compatibility
- Static `IDeserializer` instances (thread-safe, reusable)

**Namespace & File Organization:**
- One type per file (matching filename)
- Namespaces match folder structure: `Perch.Core.Modules`, `Perch.Core.Deploy`
- Interfaces co-located with implementations in the same folder

### Framework-Specific Rules

**Spectre.Console (CLI only):**
- Spectre types appear **only in `Perch.Cli`** — never in Core or Desktop
- Use `IAnsiConsole` (injected) in command classes — never `AnsiConsole` static
- Look up Spectre.Console API docs via Context7 MCP server before writing Spectre code
- Core returns data objects; CLI renders them

**WPF UI / Desktop:**
- WPF/XAML types appear **only in `Perch.Desktop`** — never in Core or CLI
- Pages implement `INavigableView<TViewModel>` for WPF UI navigation
- ViewModels: `ObservableObject` base, `[ObservableProperty]`, `[RelayCommand]` — partial classes (source generator)
- ViewModels depend on Core interfaces (mockable) — never on WPF types directly
- `NavigationView` sidebar for dashboard; `HandyControl.StepBar` for wizard
- DI via Generic Host in `App.xaml.cs` — singleton for cached pages, transient for wizard steps

**Perch.Core Engine:**
- Zero UI dependencies — only `YamlDotNet` and `MS DI Abstractions`
- Platform-specific logic behind interfaces: `ISymlinkProvider`, `IRegistryProvider`, `IStartupService`
- `AddPerchCore()` extension registers all Core services — used by both CLI and Desktop
- Platform-conditional registration: `OperatingSystem.IsWindows()` → Windows impls; else → Unix/NoOp
- All services registered as **Singleton** (stateless)

### Testing Rules

**Test Organization:**
- Test project mirrors source folder structure: `tests/Perch.Core.Tests/Deploy/` matches `src/Perch.Core/Deploy/`
- Test classes: `[TestFixture] public sealed class <ClassName>Tests`
- Test naming: `Method_Scenario_ExpectedResult` with underscores (CA1707 suppressed in tests)
- Desktop ViewModel tests live in `Perch.Core.Tests/Desktop/`
- `NUnit.Framework` and `NSubstitute` are in test `GlobalUsings.cs` — no per-file imports needed

**Test Boundaries (sociable with boundary mocks):**
- **Sociable tests preferred** — test behavior through real collaborators, not isolated classes
- **Mock only at system boundaries**: filesystem (`ISymlinkProvider`, `IFileBackupProvider`), registry (`IRegistryProvider`), processes (`IProcessRunner`, `IHookRunner`), network, platform APIs (`IStartupService`)
- **Use real instances** for parsers, orchestrators, and services that coordinate other services
- Don't rewrite existing solitary tests — this applies to new tests going forward
- Real filesystem tests: only for platform-specific providers (`WindowsSymlinkProviderTests`)
- Desktop ViewModel tests: guarded by `#if DESKTOP_TESTS` — only compile on Windows
- Smoke tests (FlaUI): separate `Perch.SmokeTests` project, run locally, screenshots to `tests/Perch.SmokeTests/screenshots/`

**SetUp Pattern:**
- `[SetUp]` method creates fresh mocks and SUT per test
- Mock fields declared with `null!`: `private ISymlinkProvider _symlinkProvider = null!;`
- `Substitute.For<T>()` for interface mocking
- Custom `SynchronousProgress<T>` helper captures `IProgress<T>` reports into a `List<T>` for assertions

### Code Quality & Style Rules

**Naming Conventions:**
- Interfaces: `I<Noun>Provider` for resource abstractions, `I<Noun>Service` for orchestration
- YAML manifest properties: `kebab-case` (`target-path`, `link-type`)
- Desktop ViewModels: `<Page>ViewModel`; Pages: `<Page>Page`; Reusable controls: `<Name>View`
- Private fields: `_camelCase`; private static: `s_camelCase`; constants: `PascalCase`

**Code Style:**
- No comments restating what code does — self-documenting code
- No XML doc comments unless they add value beyond naming
- KISS / YAGNI — only build what's needed right now
- Prefer editing existing files over creating new ones
- Don't add features beyond what's asked

**File Structure:**
- Feature folders in Core — related code together (`Modules/`, `Deploy/`, `Symlinks/`, `Catalog/`)
- One type per file, filename matches type name
- Desktop: `Views/Pages/`, `Views/Controls/`, `Views/Wizard/`, `ViewModels/` with `Wizard/` subfolder

**Analyzer Rules That Bite:**
- `RCS1018` — add accessibility modifiers (can't omit `public`/`private`/`internal`)
- `RCS1077` — optimize LINQ (don't `.Where().First()` when `.First(predicate)` works)
- `RCS1198` — avoid unnecessary boxing
- `CA1502` — cyclomatic complexity (keep methods focused)

### Development Workflow Rules

**Git:**
- Commit directly to `master` and push
- No branches or PRs required

**Build Verification:**
- `dotnet build` must complete with zero warnings (warnings = errors)
- `dotnet test` must pass all tests before committing

**CI:**
- Linux CI: `Perch.CrossPlatform.slnx` — build + test (excludes Desktop)
- Windows CI: `Perch.slnx` — build + test + integration smoke script

**Smoke Tests (optional):**
- Available for Desktop UI verification: `dotnet test tests/Perch.SmokeTests --filter PageScreenshotTests`
- Screenshots saved to `tests/Perch.SmokeTests/screenshots/` — local-only, never committed

### Critical Don't-Miss Rules

**Architectural Boundary Violations (build-breaking):**
- Adding Spectre.Console or WPF references to `Perch.Core` → breaks Linux CI
- Adding `using Spectre.Console;` in Core → same result even if no types are used
- Perch.Core must remain UI-framework-free — it's the shared engine

**Central Package Management Traps:**
- Adding `Version="x.y.z"` on a `<PackageReference>` in a `.csproj` → build error (`NETSDK1071`)
- Adding a new package without entry in `Directory.Packages.props` → build error
- Correct flow: add version to `Directory.Packages.props`, then versionless reference in `.csproj`

**ImplicitUsings=disable Gotchas:**
- Every file needs explicit `using` statements or must rely on `GlobalUsings.cs`
- Common missing imports: `System.Collections.Immutable`, `YamlDotNet.*`, `Perch.Core.*` sub-namespaces

**TreatWarningsAsErrors Landmines:**
- Unused variables, parameters, `using` directives → build failure
- Missing accessibility modifiers → build failure (RCS1018)
- Unnecessary boxing → build failure (RCS1198)
- Any Roslynator suggestion elevated to warning in `.editorconfig` → build failure

**Desktop-Specific Traps:**
- `[ObservableProperty]` requires the class to be `partial` — forgetting produces cryptic source generator errors
- `[ObservableProperty]` fields must be `_camelCase` — the generated property is `PascalCase`
- Desktop tests without `#if DESKTOP_TESTS` guard will fail Linux CI

**YAML Manifest Gotchas:**
- Properties are `kebab-case` in YAML but `PascalCase` in C# — `HyphenatedNamingConvention` handles mapping
- `IgnoreUnmatchedProperties()` means typos in YAML keys silently succeed

---

## Usage Guidelines

**For AI Agents:**
- Read this file before implementing any code
- Follow ALL rules exactly as documented
- When in doubt, prefer the more restrictive option
- `dotnet build` and `dotnet test` must pass before any commit

**For Humans:**
- Keep this file lean and focused on agent needs
- Update when technology stack or patterns change
- Remove rules that become obvious over time

Last Updated: 2026-02-19

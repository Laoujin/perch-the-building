# Task: Set up CLAUDE.md and AGENTS-DOTNET-STYLE.md for the Perch project

## Context

Perch is a cross-platform dotfiles and application settings manager built in C# / .NET 10. It's a greenfield project — no code written yet. I need you to create the coding conventions files that Claude Code agents will follow when implementing code.

## Project Tech Stack (verified current versions)

- **Runtime:** .NET 10 (LTS, released Nov 2025)
- **Language:** C# (latest version supported by .NET 10)
- **CLI Output:** Spectre.Console 0.54.0, Spectre.Console.Cli 0.53.1
- **Testing:** NUnit 4.4.0, NSubstitute 5.3.0
- **CI:** GitHub Actions (Windows + Linux runners)
- **Analyzers:** Roslynator, Microsoft.CodeAnalysis.Analyzers — warnings as errors
- **Distribution:** `dotnet tool install perch -g`

## Solution Structure

```
Perch.sln
├── src/
│   ├── Perch.Core/          # Engine library (classlib) — all logic, interfaces, models
│   └── Perch.Cli/           # Console app — Spectre.Console, references Core
└── tests/
    └── Perch.Core.Tests/    # NUnit + NSubstitute
```

## What I Need You to Create

### 1. CLAUDE.md (project root)

Top-level file that Claude Code reads automatically. Keep it concise. Should contain:
- Project overview (1-2 lines)
- Tech stack summary with versions
- Solution structure
- Build/test/run commands (`dotnet build`, `dotnet test`, `dotnet run --project src/Perch.Cli`)
- Pointer to `AGENTS-DOTNET-STYLE.md` for coding conventions
- General coding principles: KISS, YAGNI, no over-engineering, no premature abstraction
- "Don't add features beyond what's asked. A bug fix doesn't need surrounding code cleaned up."
- Prefer editing existing files over creating new ones

### 2. AGENTS-DOTNET-STYLE.md (project root)

Detailed .NET/C# coding conventions for AI agents. Research current best practices and include:

**Code Style:**
- Naming conventions (PascalCase for public, _camelCase for private fields, etc.)
- File organization (one type per file, file name matches type name)
- `using` directives style (global usings in a single file vs per-file)
- Nullable reference types enabled — no suppression operators unless justified
- Prefer `var` when type is obvious from right-hand side
- Record types vs classes guidance
- Pattern matching preferences

**Comments & Documentation:**
- XML doc comments ONLY where they add value beyond what naming already conveys
- No comments restating what code does — code should be self-documenting
- Comments explain WHY, not WHAT
- No `// TODO` without a linked issue

**Complexity & Analyzers:**
- Max cyclomatic complexity (look up what Roslynator enforces by default or what a good threshold is)
- Warnings as errors — zero tolerance for analyzer warnings
- Roslynator + Microsoft.CodeAnalysis.Analyzers rules
- Research what .editorconfig settings are standard for modern .NET 10 projects

**Testing Conventions (NUnit 4 + NSubstitute 5):**
- Research NUnit 4 assertion syntax (the new Assert.That constraint model vs classic)
- Research NSubstitute 5 patterns (Arg matchers, Returns, Received)
- Test naming: `MethodName_Scenario_ExpectedResult` or similar — pick one and be consistent
- One assertion concept per test (but multiple Assert.That calls for the same concept is fine)
- Arrange/Act/Assert structure
- Platform-specific code tested with real filesystem; everything else mocked via interfaces

**Error Handling:**
- Don't catch exceptions just to rethrow
- Use specific exception types
- No empty catch blocks
- Result pattern vs exceptions guidance (if applicable)

**Async:**
- Async all the way — no `.Result` or `.Wait()`
- CancellationToken threading through public APIs
- ConfigureAwait guidance for library code (Perch.Core)

**Spectre.Console:**
- Note: Use Context7 MCP server if available to look up Spectre.Console API docs before writing Spectre code, to avoid syntax errors with this library
- Spectre.Console rendering belongs ONLY in Perch.Cli, never in Perch.Core
- Perch.Core should expose data/results that the CLI layer renders

## Research Requests

Before writing these files, please research:
1. Current .editorconfig best practices for .NET 10 projects
2. NUnit 4 migration guide / new assertion patterns (it changed significantly from NUnit 3)
3. Roslynator default rules and recommended severity levels
4. Common CLAUDE.md patterns from popular open-source .NET projects (if any exist)
5. Whether there's a standard `dotnet new editorconfig` template worth using

## Output

Write both files directly to the project root:
- `/CLAUDE.md`
- `/AGENTS-DOTNET-STYLE.md`

Keep CLAUDE.md under 80 lines. AGENTS-DOTNET-STYLE.md can be longer — it's a reference document.

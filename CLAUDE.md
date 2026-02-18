# Perch

Cross-platform dotfiles and application settings manager. Symlink-first — change a setting, it's immediately in git.

## Tech Stack

- .NET 10 / C# (latest)
- NUnit 4.4.0, NSubstitute 5.3.0
- Roslynator + Microsoft.CodeAnalysis.Analyzers — warnings as errors
- CI: GitHub Actions (Linux)

## Commands

```bash
dotnet build                              # Build entire solution
dotnet test                               # Run all tests
dotnet run --project src/Perch.Cli        # Run the CLI
dotnet run --project src/Perch.Cli -- deploy   # Run with arguments
```

After making changes to Perch.Desktop, take smoke test screenshots to verify:

```bash
dotnet test tests/Perch.SmokeTests --filter PageScreenshotTests  # Screenshot all pages
dotnet test tests/Perch.SmokeTests --filter <TestName>            # Targeted test
```

Screenshots saved to `tests/Perch.SmokeTests/screenshots/`. Review them with the Read tool (supports images).

## Coding Conventions

See global `~/.claude/AGENTS-DOTNET-STYLE.md` and `~/.claude/AGENTS-DOTNET-TESTING.md`.

### Spectre.Console

- **Use Context7 MCP server** to look up Spectre.Console API docs before writing Spectre code
- Spectre.Console rendering belongs **only in Perch.Cli** — never in Perch.Core
- Perch.Core returns data/result objects; the CLI layer decides how to render them
- Use `IAnsiConsole` (injected) in command classes for testability

## Architecture

- Platform-specific logic behind interfaces — core engine depends only on abstractions
- Spectre.Console rendering belongs ONLY in Perch.Cli, never in Perch.Core
- Perch.Core exposes data/results; the CLI layer renders them
- CancellationToken threaded through public APIs for graceful shutdown

## Principles

- **KISS / YAGNI** — only build what's needed right now
- **No over-engineering** — no premature abstractions, no design for hypothetical futures
- **Don't add features beyond what's asked.** A bug fix doesn't need surrounding code cleaned up
- **Prefer editing existing files** over creating new ones
- **No comments restating what code does** — code should be self-documenting
- **Warnings as errors** — zero tolerance for analyzer warnings

## Agent Workflow (MANDATORY)

**Use `/fix-issue` or `/fix-issue <number>` to work on issues.** This is the default operating mode.

The full pipeline is documented in `AGENTS-WORKFLOW.md`:

1. **Worktree** -- branch from master, work in a git worktree
2. **Fix** -- implement, build (zero warnings), test (all pass)
3. **Screenshot** -- run smoke tests, show screenshots to user via Read tool
4. **PR** -- create PR linking the issue with `Closes #N`
5. **Browser** -- open PR in browser, report to user
6. **Repeat** -- after merge, pull master, clean up, pick next issue

Do NOT commit directly to master. Do NOT skip screenshots for Desktop changes. Do NOT commit screenshots -- they are local-only.

## Project Docs

- `_bmad-output/planning-artifacts/prd.md` — Product requirements
- `_bmad-output/planning-artifacts/architecture.md` — Architecture decisions

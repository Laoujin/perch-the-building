# Story 20.4: Gallery Language — Node Ecosystem

Status: review

## Story

As a Perch Desktop user browsing the Languages page,
I want the Node/JavaScript ecosystem to have complete gallery entries covering runtimes, version managers, package managers, global packages, and config files,
so that the Node ecosystem detail page shows my full JS toolchain with correct detection, alternatives, and relationships.

## Design Source

- `_bmad-output/design-thinking-2026-02-19.md` — Unified gallery architecture, Languages page wireframes (Screen 2: sub-categories with cards), alternative switching (npm -> bun)
- `_bmad-output/planning-artifacts/gallery-schema.md` — YAML schema reference for all entry fields
- `_bmad-output/planning-artifacts/gallery-epics.md` — Story 4.3 (Node ecosystem apps)

## Acceptance Criteria

1. **Complete ecosystem.** nodejs.yaml `suggests` list references all ecosystem tools that should appear in the Node ecosystem detail page sub-categories.
2. **Sub-category coverage.** At least one entry per: Runtimes (Node, Deno, Bun), Version Managers (fnm, nvm, volta), Package Managers (npm, yarn, pnpm), Global Packages (typescript, tsx, eslint_d, prettier, etc.), Configuration Files (.npmrc, .nvmrc, bunfig.toml).
3. **Alternatives set.** Runtime alternatives (Node <-> Deno <-> Bun), package manager alternatives (npm <-> yarn <-> pnpm), version manager alternatives (fnm <-> nvm <-> volta) are correctly defined using `alternatives` field (bidirectional, Core auto-mirrors).
4. **Relationship fixes.** Version managers (fnm, nvm, volta) use `suggests: [nodejs]` not `requires: [nodejs]` — version managers are an alternative way to install Node, not dependent on a prior Node installation.
5. **Config file entries.** .npmrc (standalone `kind: dotfile` entry for Languages page "Configuration Files" sub-category), .nvmrc, bunfig.toml (already on bun.yaml) each have correct `config.links` with platform targets.
6. **Global package entries.** Common global npm/bun tools have entries with `kind: cli-tool`, `install.node-package`, category `Development/Node`, `hidden: true`. At minimum: typescript, tsx, eslint_d, prettier, npm-check-updates, http-server, rimraf, tldr (matching perch-config/bun/manifest.yaml).
7. **Missing `kind` fields.** deno.yaml gets `kind: runtime`, nvm.yaml gets `kind: cli-tool`.
8. **Install IDs verified.** All entries with winget/choco/node-package IDs resolve correctly.
9. **Gallery index regenerated.** Running `node scripts/generate-index.mjs` produces valid index.yaml including all new/modified entries.

## Current Gallery State

In `../perch-gallery/catalog/apps/node/` — 8 files exist:

| File | kind | category | Issues |
|------|------|----------|--------|
| `nodejs.yaml` | runtime | Development/Languages | `alternatives: [bun]` too narrow — should include deno. Category should be `Development/Node` to match ecosystem pattern |
| `bun.yaml` | runtime | Development/Node | OK — has bunfig.toml config.link, hidden: true |
| `deno.yaml` | *(missing)* | Development/Node | Missing `kind: runtime` |
| `fnm.yaml` | cli-tool | Development/Node | `requires: [nodejs]` is wrong — fnm installs Node, doesn't depend on it |
| `nvm.yaml` | *(missing)* | Development/Node | Missing `kind: cli-tool`. `requires: [nodejs]` same issue as fnm |
| `pnpm.yaml` | cli-tool | Development/Node | `alternatives: [npm, yarn]` — but `npm` entry doesn't exist yet. `requires: [nodejs]` correct |
| `volta.yaml` | cli-tool | Development/Node | `requires: [nodejs]` is wrong — volta installs Node, doesn't depend on it |
| `yarn.yaml` | cli-tool | Development/Node | `install.node-package: yarn` only (no winget/choco) — correct |

## Tasks / Subtasks

- [x] Task 1: Fix existing entries (AC: #3, #4, #7)
  - [x] `nodejs.yaml`: change category to `Development/Node`, add `deno` to alternatives, expand `suggests` to include all new entry IDs (npm, typescript, tsx, eslint-d, prettier, npm-check-updates, http-server, rimraf, tldr, npmrc, nvmrc)
  - [x] `deno.yaml`: add `kind: runtime`, add `alternatives: [nodejs, bun]`
  - [x] `nvm.yaml`: add `kind: cli-tool`, change `requires: [nodejs]` to `suggests: [nodejs]`
  - [x] `fnm.yaml`: change `requires: [nodejs]` to `suggests: [nodejs]`
  - [x] `volta.yaml`: change `requires: [nodejs]` to `suggests: [nodejs]`
  - [x] `bun.yaml`: verify `alternatives: [nodejs, deno]` present (Core auto-mirrors from nodejs.yaml, but explicit is clearer)

- [x] Task 2: Create npm entry (AC: #2, #3)
  - [x] `npm.yaml`: kind: cli-tool, no install section (bundled with Node), hidden: true, `alternatives: [yarn, pnpm]`, `requires: [nodejs]`

- [x] Task 3: Create global package entries (AC: #6)
  - [x] `typescript.yaml`: kind: cli-tool, install.node-package: typescript, description: "TypeScript compiler and language"
  - [x] `tsx.yaml`: kind: cli-tool, install.node-package: tsx, description: "TypeScript execute — run TS files directly"
  - [x] `eslint-d.yaml`: kind: cli-tool, install.node-package: eslint_d, description: "Fast ESLint daemon for editors"
  - [x] `prettier.yaml`: kind: cli-tool, install.node-package: prettier, description: "Opinionated code formatter"
  - [x] `npm-check-updates.yaml`: kind: cli-tool, install.node-package: npm-check-updates, description: "Upgrade package.json dependencies"
  - [x] `http-server.yaml`: kind: cli-tool, install.node-package: http-server, description: "Simple zero-configuration HTTP server"
  - [x] `rimraf.yaml`: kind: cli-tool, install.node-package: rimraf, description: "Cross-platform rm -rf"
  - [x] `tldr.yaml`: kind: cli-tool, install.node-package: tldr, description: "Simplified community-driven man pages"
  - [x] All entries: category: Development/Node, tags: [node, cli, ...], profiles: [developer], hidden: true, requires: [nodejs]

- [x] Task 4: Create config file entries (AC: #5)
  - [x] `npmrc.yaml`: kind: dotfile, config.links: source: .npmrc, target windows: "%USERPROFILE%/.npmrc", linux/macos: "$HOME/.npmrc"
  - [x] `nvmrc.yaml`: kind: dotfile, config.links: source: .nvmrc, target windows: "%USERPROFILE%/.nvmrc", linux/macos: "$HOME/.nvmrc"
  - [x] Verify `bun.yaml` already has bunfig.toml config.link (it does)
  - [x] All dotfile entries: category: Development/Node, hidden: true, no install section

- [x] Task 5: Verify install IDs (AC: #8)
  - [x] nodejs.yaml: winget `OpenJS.NodeJS.LTS`, choco `nodejs-lts` — verify
  - [x] bun.yaml: winget `Oven-sh.Bun` — verify
  - [x] deno.yaml: winget `DenoLand.Deno`, choco `deno` — verify
  - [x] fnm.yaml: winget `Schniz.fnm`, choco `fnm` — verify
  - [x] nvm.yaml: winget `CoreyButler.NVMforWindows`, choco `nvm` — verify
  - [x] pnpm.yaml: winget `pnpm.pnpm`, node-package `pnpm` — verify
  - [x] volta.yaml: winget `Volta.Volta`, choco `volta` — verify
  - [x] Global packages: verify node-package names match actual npm registry names

- [x] Task 6: Regenerate gallery index (AC: #9)
  - [x] Run `node scripts/generate-index.mjs` from `../perch-gallery/`
  - [x] Verify index.yaml includes all new entries

## Dev Notes

### Pattern Reference: Story 20-3 (.NET)

Follow the same workflow structure as the .NET ecosystem story. Same audit-create-fix-verify-regenerate pattern.

### Schema Conventions (from gallery-schema.md)

- **ID = filename** (without .yaml extension)
- **`kind` values**: `app` (GUI), `cli-tool` (CLI), `runtime` (SDK/runtime), `dotfile` (config-only, no install)
- **`alternatives`**: Bidirectional — declare on one side, Core auto-mirrors. Still good practice to declare on both sides for clarity.
- **`suggests`**: One-way soft recommendation ("you might also want")
- **`requires`**: One-way hard dependency ("won't work without")
- **`hidden: true`**: Ecosystem sub-items hidden from top-level views, visible inside ecosystem detail
- **`install.node-package`**: Resolved by user's chosen package manager (npm/bun/pnpm)
- **Hyphenated naming**: all YAML keys use kebab-case
- **All new sub-entries**: category `Development/Node`, `profiles: [developer]`, `hidden: true`

### Detection Integration (from Story 20-2)

- `RuntimeDetectionService` already handles `node --version`, `deno --version`, `bun --version`
- Global tool detection via `npm list -g --json` matches against `install.node-package` — new entries will auto-detect
- Version managers detected via winget/choco scan or CLI: `fnm --version`, `nvm version`, `volta --version`

### perch-config Cross-Reference

The user's perch-config has:
- `bun/manifest.yaml` — global-packages via bun: eslint_d, http-server, rimraf, npm-check-updates, prettier, tsx, tldr
- `install.yaml` — references `nvm` and `bun` gallery IDs
- No .npmrc, .nvmrc, or bunfig.toml config files in perch-config yet

Global package entries created here will be detected by `DetectGlobalToolsAsync` when it parses `npm list -g --json` output.

### Languages Page Sub-Categories (from Story 20-1)

Entries appear in sub-categories on the ecosystem detail page:

| Sub-category | Entries |
|-------------|---------|
| Runtimes | nodejs, bun, deno |
| CLI Tools | fnm, nvm, volta, npm, yarn, pnpm, typescript, tsx, eslint-d, prettier, npm-check-updates, http-server, rimraf, tldr |
| Configuration Files | npmrc, nvmrc (+ bunfig.toml from bun.yaml config.links) |

Sub-category grouping is driven by `kind` field. `kind: runtime` entries go in Runtimes. `kind: cli-tool` entries go in CLI Tools. `kind: dotfile` entries go in Configuration Files (sort order 99).

### Example New Entry Template

```yaml
type: app
name: TypeScript
kind: cli-tool
category: Development/Node
tags: [typescript, compiler, node, cli]
description: TypeScript compiler and language
hidden: true
profiles: [developer]
os: [windows, linux, macos]
license: Apache-2.0
requires: [nodejs]
links:
  website: https://www.typescriptlang.org
  github: https://github.com/microsoft/TypeScript
install:
  node-package: typescript
```

### Project Structure Notes

- All changes in `../perch-gallery/catalog/apps/node/` directory
- No changes to Perch source code — this is gallery content only
- Gallery is a separate git repo (`../perch-gallery/`)
- Don't remove or rename existing entries that perch-config or other repos may reference

### References

- [Source: _bmad-output/design-thinking-2026-02-19.md] — Unified gallery architecture, sub-categories, Languages page wireframes
- [Source: _bmad-output/planning-artifacts/gallery-schema.md] — Full YAML schema with field descriptions
- [Source: _bmad-output/planning-artifacts/gallery-epics.md#Story 4.3] — Original Node ecosystem story
- [Source: _bmad-output/implementation-artifacts/20-3-gallery-language-dotnet.md] — .NET story pattern reference
- [Source: _bmad-output/implementation-artifacts/20-2-language-sdk-detection.md] — Detection service integration
- [Source: _bmad-output/implementation-artifacts/20-1-languages-page-scaffold.md] — Sub-category grouping logic

## Constraints

- Changes are in `../perch-gallery/` repo (separate git repo).
- Follow existing YAML schema conventions (hyphenated-naming, see gallery-schema.md).
- Don't remove or rename existing entries that other repos may reference.
- No new NuGet packages or Perch source changes.

## Dev Agent Record

### Agent Model Used
Claude Opus 4.6

### Debug Log References
None — no errors encountered.

### Completion Notes List
- Fixed 7 existing entries: nodejs (category + alternatives + suggests), deno (kind + alternatives), nvm (kind + requires→suggests), fnm (requires→suggests + alternatives), volta (requires→suggests + alternatives), bun (alternatives), yarn (alternatives)
- Created npm.yaml — bundled package manager, no install section
- Created 8 global package entries: typescript, tsx, eslint-d, prettier, npm-check-updates, http-server, rimraf, tldr — all with install.node-package, hidden, requires nodejs
- Created 2 config file entries: npmrc.yaml, nvmrc.yaml — kind: dotfile with platform config.links
- Verified all 7 winget IDs via `winget show` and all 8 npm package names via `npm info`
- Regenerated index.yaml — 261 apps total, all 11 new entries confirmed present

### Change Log
- 2026-02-20: Story 20-4 implemented — Node ecosystem gallery entries complete

### File List
- Modified: `../perch-gallery/catalog/apps/node/nodejs.yaml`
- Modified: `../perch-gallery/catalog/apps/node/deno.yaml`
- Modified: `../perch-gallery/catalog/apps/node/nvm.yaml`
- Modified: `../perch-gallery/catalog/apps/node/fnm.yaml`
- Modified: `../perch-gallery/catalog/apps/node/volta.yaml`
- Modified: `../perch-gallery/catalog/apps/node/bun.yaml`
- Modified: `../perch-gallery/catalog/apps/node/yarn.yaml`
- Created: `../perch-gallery/catalog/apps/node/npm.yaml`
- Created: `../perch-gallery/catalog/apps/node/typescript.yaml`
- Created: `../perch-gallery/catalog/apps/node/tsx.yaml`
- Created: `../perch-gallery/catalog/apps/node/eslint-d.yaml`
- Created: `../perch-gallery/catalog/apps/node/prettier.yaml`
- Created: `../perch-gallery/catalog/apps/node/npm-check-updates.yaml`
- Created: `../perch-gallery/catalog/apps/node/http-server.yaml`
- Created: `../perch-gallery/catalog/apps/node/rimraf.yaml`
- Created: `../perch-gallery/catalog/apps/node/tldr.yaml`
- Created: `../perch-gallery/catalog/apps/node/npmrc.yaml`
- Created: `../perch-gallery/catalog/apps/node/nvmrc.yaml`
- Regenerated: `../perch-gallery/catalog/index.yaml`

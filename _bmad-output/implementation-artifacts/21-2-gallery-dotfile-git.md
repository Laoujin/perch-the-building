# Story 21.2: Gallery Dotfile — Git

Status: review

## Story

As a Perch Desktop user on the Dotfiles page,
I want the Git dotfile entry to have complete gallery metadata including config links, tweaks, and detail page content,
so that Git appears as a rich, actionable card with accurate symlink detection and configurable tweaks.

## Design Source

`_bmad-output/design-thinking-2026-02-19.md` — Dotfiles page prototype, gear icon for dotfiles with tweaks.

## Current Gallery State

**File:** `../perch-gallery/catalog/apps/git.yaml`

Current content:
```yaml
type: app
name: Git
kind: dotfile
category: Development/Version Control
tags: [vcs, cli, developer]
description: Distributed version control system
profiles: [developer]
os: [windows, linux, macos]
license: GPL-2.0
suggests: [github-desktop, gitkraken, sourcetree, fork, tortoisegit]
links:
  website: https://git-scm.com
  docs: https://git-scm.com/doc
  github: https://github.com/git/git
install:
  winget: Git.Git
  choco: git
config:
  links:
    - source: .gitconfig
      target:
        windows: "%USERPROFILE%/.gitconfig"
        linux: "$HOME/.gitconfig"
        macos: "$HOME/.gitconfig"
    - source: .gitignore_global
      target:
        windows: "%USERPROFILE%/.gitignore_global"
        linux: "$HOME/.gitignore_global"
        macos: "$HOME/.gitignore_global"
tweaks:
  - id: git-bash-here
    name: Git Bash Here
    description: Add "Git Bash Here" to the Explorer context menu
    reversible: true
    registry:
      - key: HKCR\Directory\Background\shell\git_shell\command
        name: "(Default)"
        value: '"C:\Program Files\Git\git-bash.exe" "--cd=%v"'
        type: string
```

### What's Already Correct

- `kind: dotfile` — correct, ensures it appears on the Dotfiles page (not Apps page)
- `category: Development/Version Control` — appropriate cross-cutting category
- `config.links` — .gitconfig and .gitignore_global with platform paths
- `install` — winget `Git.Git` and choco `git` are correct IDs
- `suggests` — references 5 Git GUI tools, all of which exist in the gallery
- `tweaks` — git-bash-here has registry entry

## What Needs Verification / Enrichment

### 1. Config Links — Path Verification

Current paths use `%USERPROFILE%/.gitconfig` and `$HOME/.gitconfig`. These are correct:
- **Windows:** `%USERPROFILE%/.gitconfig` resolves to `C:\Users\<user>\.gitconfig`. Git for Windows supports both `/` and `\`.
- **Linux/macOS:** `$HOME/.gitconfig` is the standard location.
- `.gitignore_global` follows the same pattern — correct.

**Verdict:** Paths are correct. No change needed.

### 2. Tweaks — Enrichment Needed

**Existing:** `git-bash-here` — registry entry adds "Git Bash Here" to Explorer directory background context menu. Marked `reversible: true`.

**Missing tweaks to add:**

| Tweak ID | Name | Description | Registry |
|----------|------|-------------|----------|
| `git-gui-here` | Git GUI Here | Add "Git GUI Here" to Explorer context menu | `HKCR\Directory\Background\shell\git_gui\command` → `"C:\Program Files\Git\cmd\git-gui.exe" "--working-dir" "%v"` |
| `git-lfs-enable` | Enable Git LFS | Install and configure Git Large File Storage globally | **Not registry-based** — requires `git lfs install` command. Use `script` field instead of `registry`. |

**Decision needed on git-lfs-enable:** The gallery tweak schema supports a `script` field for PowerShell-based tweaks. Git LFS enablement is `git lfs install` (sets up global git hooks). This is a valid tweak but needs:
- `script: "git lfs install"`
- `undo-script: "git lfs uninstall"`
- Requires git-lfs to be installed first (it ships with Git for Windows since 2.x)

**Registry details for git-gui-here:**
```yaml
- id: git-gui-here
  name: Git GUI Here
  description: Add "Git GUI Here" to the Explorer context menu
  reversible: true
  registry:
    - key: HKCR\Directory\Background\shell\git_gui
      name: "(Default)"
      value: "Git &GUI Here"
      type: string
    - key: HKCR\Directory\Background\shell\git_gui\command
      name: "(Default)"
      value: '"C:\Program Files\Git\cmd\git-gui.exe" "--working-dir" "%v"'
      type: string
```

### 3. Suggests — Enrichment Needed

**Current suggests:** `[github-desktop, gitkraken, sourcetree, fork, tortoisegit]`

**Missing from suggests:**
- `gitui` — exists in gallery as `kind: cli-tool`, `hidden: true`. A TUI git client. Should be in suggests.
- `lazygit` — exists in gallery as `kind: cli-tool`. A TUI git client. Should be in suggests.

**Updated suggests:** `[github-desktop, gitkraken, sourcetree, fork, tortoisegit, gitui, lazygit]`

### 4. Extensions / Additional References

The gallery schema has no `extensions` field for `kind: dotfile` entries. Git-lfs and git-credential-manager are best handled as:
- **git-lfs**: tweak on git.yaml (see above) — it's a git feature toggle, not a separate app
- **git-credential-manager**: ships bundled with Git for Windows. Not a separate install. No gallery entry needed. Could be mentioned in `description` or as a tag.

**Decision:** No new gallery entries needed for git-lfs or git-credential-manager. Git-lfs becomes a tweak on git.yaml. GCM is built-in.

### 5. Detail Page Content Assessment

For the gear icon to be justified on the Dotfiles page card (per design-thinking), Git needs enough depth:
- **Config links:** 2 (`.gitconfig`, `.gitignore_global`) -- good
- **Tweaks:** 2-3 (git-bash-here, git-gui-here, optionally git-lfs-enable) -- good
- **Suggests:** 7 Git GUI tools -- excellent for "Also Consider" section
- **Alternatives:** none (Git is the only VCS) -- correct

**Verdict:** Git is the richest dotfile entry in the gallery. Gear icon is fully justified.

## Acceptance Criteria

1. **Config links verified.** .gitconfig and .gitignore_global have correct platform-specific target paths that resolve on Windows, Linux, and macOS.
2. **Tweaks complete.** At minimum: (a) `git-bash-here` has full registry definition (already done), (b) `git-gui-here` added with registry key/name/value/type. Optional: `git-lfs-enable` as script-based tweak.
3. **Suggests populated.** All 7 Git GUI tools in the gallery are referenced: github-desktop, gitkraken, sourcetree, fork, tortoisegit, gitui, lazygit.
4. **Detail page worthy.** Entry has 2 config links + 2-3 tweaks + 7 suggests — gear icon on Dotfiles page card is justified.
5. **Gallery index regenerated.** Running `node scripts/generate-index.mjs` in `../perch-gallery/` produces valid index.yaml including updated git entry.
6. **No schema violations.** Entry follows gallery-schema.md conventions exactly.

## Tasks / Subtasks

- [x] Task 1: Verify config.links target paths (AC: #1)
  - [x] Confirm `%USERPROFILE%/.gitconfig` resolves correctly on Windows (Git uses forward slashes)
  - [x] Confirm `$HOME/.gitconfig` is correct for Linux/macOS
  - [x] Same verification for `.gitignore_global`

- [x] Task 2: Add `git-gui-here` tweak (AC: #2)
  - [x] Add tweak with id `git-gui-here`, registry entries for `HKCR\Directory\Background\shell\git_gui` and `...\command`
  - [x] Mark `reversible: true`
  - [x] Verify registry key structure matches Git for Windows default installer behavior

- [x] Task 3: (Optional) Add `git-lfs-enable` tweak (AC: #2)
  - [x] Add script-based tweak: `script: "git lfs install"`, `undo-script: "git lfs uninstall"`
  - [x] Mark `reversible: true`
  - [x] Note: Only valid if git-lfs binary is present (ships with Git for Windows 2.x+)

- [x] Task 4: Update suggests list (AC: #3)
  - [x] Add `gitui` and `lazygit` to existing suggests array
  - [x] Final list: `[github-desktop, gitkraken, sourcetree, fork, tortoisegit, gitui, lazygit]`

- [x] Task 5: Regenerate gallery index (AC: #5)
  - [x] Run `node scripts/generate-index.mjs` in `../perch-gallery/`
  - [x] Verify git entry in generated index.yaml

## Dev Notes

### File to Modify

| File | Repo | Change |
|------|------|--------|
| `catalog/apps/git.yaml` | `../perch-gallery/` | Add tweaks, update suggests |

### Gallery Schema Reminders

- App-owned tweaks use the same schema as standalone tweaks minus `type`, `category`, and `windows-versions` (inherited from parent)
- `script` field is valid for non-registry tweaks (PowerShell command)
- `undo-script` for reversible script tweaks
- `suggests` is one-way soft relationship ("you might also want")
- `alternatives` is mutual (Core auto-mirrors) — but Git has no alternatives

### Registry Key Conventions

- Use `HKCR` (not `HKEY_CLASSES_ROOT`) per gallery convention
- `(Default)` for default value name
- String type for context menu entries
- `reversible: true` on all tweaks — Perch should be able to clean up what it adds

### What NOT to Do

- Do NOT create separate gallery entries for git-lfs or git-credential-manager — they're features of Git, not standalone apps
- Do NOT change `kind: dotfile` — Git must remain on the Dotfiles page
- Do NOT add `config.links` for `.git/config` (per-repo config) — only global dotfiles
- Do NOT modify the Perch C# codebase — this story is gallery YAML only

### Project Structure Notes

- All changes in `../perch-gallery/` (separate git repo from `Perch/`)
- Gallery index auto-generated by `node scripts/generate-index.mjs` — don't manually edit `index.yaml`
- Commit in `perch-gallery` repo, not in `Perch` repo

### References

- [Source: _bmad-output/design-thinking-2026-02-19.md#Dotfiles Page] — Gear icon, flat grid, cross-cutting only
- [Source: _bmad-output/planning-artifacts/gallery-schema.md#App-Owned Tweaks] — Tweak schema within apps
- [Source: _bmad-output/planning-artifacts/gallery-schema.md#Tweak Schema] — Script/undo-script fields
- [Source: _bmad-output/planning-artifacts/prd.md#P2: Dotfiles Page + Gallery] — Acceptance per dotfile
- [Source: perch-gallery/catalog/apps/git.yaml] — Current state
- [Source: 21-1-dotfiles-page-improvements.md] — Page-level context (gear icon conditional, cross-cutting filter)

## Dev Agent Record

### Implementation Plan

- Verified existing config.links paths are correct (no changes needed)
- Added `git-gui-here` tweak with two registry entries (shell display name + command)
- Added `git-lfs-enable` as script-based tweak with undo-script
- Extended suggests from 5 to 7 entries (added gitui, lazygit)
- Regenerated gallery index — 250 apps, 5 fonts, 102 tweaks

### Completion Notes

All 5 tasks completed. Git entry now has 2 config links, 3 tweaks (git-bash-here, git-gui-here, git-lfs-enable), and 7 suggests. Gallery index regenerated and git entry confirmed present with `kind: dotfile`. No schema violations — all tweaks follow app-owned tweak schema (no `type`, `category`, or `windows-versions`). Script-based tweak uses `script`/`undo-script` fields per schema.

## File List

| File | Action | Repo |
|------|--------|------|
| `catalog/apps/git.yaml` | Modified | perch-gallery |
| `catalog/index.yaml` | Regenerated | perch-gallery |

## Change Log

- 2026-02-20: Added git-gui-here and git-lfs-enable tweaks, updated suggests with gitui and lazygit, regenerated index

## Constraints

- Changes are in `../perch-gallery/` repo (separate git repo).
- Git is a cross-cutting dotfile — it stays on the Dotfiles page, NOT on Languages page.
- Follow existing YAML schema conventions (hyphenated-naming, see gallery-schema.md).
- Don't remove or rename existing entries that other repos may reference.

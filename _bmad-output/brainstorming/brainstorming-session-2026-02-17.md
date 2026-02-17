---
stepsCompleted: [1, 2, 3, 4]
inputDocuments: []
session_topic: 'Windows tweaks ecosystem expansion -- gallery sourcing, sync strategy, UX for fonts/startup/services/context menus'
session_goals: 'Discover what tweaks to catalog, how to mine external repos for registry keys, shape the user experience'
selected_approach: 'ai-recommended'
techniques_used: ['Morphological Analysis', 'Cross-Pollination', 'SCAMPER Method']
ideas_generated: 30
context_file: ''
session_active: false
workflow_completed: true
---

# Brainstorming Session Results

**Facilitator:** Wouter
**Date:** 2026-02-17

## Session Overview

**Topic:** Windows tweaks ecosystem expansion -- gallery sourcing, sync strategy, UX for fonts/startup/services/context menus
**Goals:** Discover what tweaks to catalog, how to mine external repos for registry keys, shape the user experience

### Context

- perch-gallery currently has 15 tweaks, 59 apps, 5 fonts
- perch-config has raw .reg files for explorer folders and context menus
- PRD plans declarative registry.yaml with drift detection, gallery merging, per-machine overrides
- External import tools planned: WinUtil JSON importer, Scoop manifest importer
- Gallery YAML format supports: registry entries (key, name, value, type), reversible flag, profiles, priority

### Session Setup

- **Approach selected:** AI-Recommended Techniques
- **Technique sequence:** Morphological Analysis -> Cross-Pollination -> SCAMPER Method

## Technique Execution Results

### Phase 1: Morphological Analysis

Mapped 6 axes of the Windows tweaks parameter space:

| Axis | Dimensions |
|------|------------|
| **Categories** | Startup Programs, Context Menus, Fonts, Power (+ existing 15 tweak categories) |
| **Sources** | WinUtil, Sophia Script, O&O ShutUp10, Win10Tweaker, Scoop, Windows Docs, manual |
| **Mechanisms** | Registry YAML, PowerShell commands, Font installation (.reg retired) |
| **Sync/Deploy** | Apply/capture, drift detection, reversibility, per-machine, profiles, restart tracking |
| **UX** | Unified tree taxonomy, WPF wizard+dashboard, status ribbons, deep links, diff view, machine toggle |
| **Sourcing pipeline** | One-time import tooling, verification, human testing, deduplication, license check, CI staleness |

**Key decisions made during exploration:**

- **Top categories:** Startup Programs, Context Menus, Fonts, Power
- **Mechanisms:** Registry YAML + PowerShell commands + Font installation. .reg files retired
- **Sourcing:** One-time import with tooling, not an ongoing sync framework
- **Quality gates:** Verification (CI), human visual testing, deduplication (side-by-side), license check, CI staleness per Windows version

### Phase 2: Cross-Pollination

Studied four external tools:

**WinUtil (Chris Titus):** 65 tweaks in declarative JSON. Multi-mechanism definitions (registry + service + scheduledTask + InvokeScript). OriginalValue baked into definition for revert. No drift detection.

**Sophia Script:** ~270 PowerShell functions with paired -Enable/-Disable parameters. 8 Windows version variants. Non-destructive philosophy using only Microsoft-documented methods. Covers security, file associations, developer tooling beyond what WinUtil offers.

**O&O ShutUp10++:** Three-tier recommendation system (green/amber/red). Exportable .cfg format. Strong on privacy/telemetry and AI/Copilot tweaks.

**Winaero Tweaker:** Hierarchical category tree that adapts per OS version. Full Windows 11 support.

### Phase 3: SCAMPER Method

Applied all 7 lenses to existing gallery and ideas from phases 1-2:

- **Substitute:** .reg -> YAML, flat categories -> tree paths, reversible boolean -> three-value model, website -> WPF as primary browser
- **Combine:** Apps + their tweaks in one tree node, WinUtil + Sophia into one dedup pipeline, per-machine + profiles, drift detection across all mechanisms, "Open Location" across mechanism types
- **Adapt:** Package manager dependencies (suggests/requires), browser extension manager UX for status cards, CI matrix for registry validation
- **Modify:** Import pipeline as one-off scripts not a framework, inline value display on tweak detail view
- **Put to other uses:** Three-value model as drift dashboard at startup
- **Eliminate:** Retire priority field (use dependency graph), eliminate manual index.yaml (auto-generate), eliminate WinUtil panel concept
- **Reverse:** Detect-first flow (scan before configure), gallery as source of truth (manifest stores deviations), app-owned tweaks (bad behavior is part of the app entry, not a separate tweak phase)

## Idea Organization and Prioritization

### Execution Order

#### Phase 1: Quick Wins

Schema changes that are small and independent:

1. Auto-generate `index.yaml` from catalog folder contents
2. Add `restart_required: true` field to gallery schema
3. Add `windows_versions: [10, 11]` field to gallery schema
4. Add category `sort:` field for controlling display order in tree
5. Retire `priority:` field, derive execution order from dependency graph
6. Add unified `type:` field (app, tweak, font) with shared base schema
7. Convention: tree depth matches content density -- don't create subcategories for 2 items

#### Phase 2: Top Priorities

Foundation that everything else builds on:

1. **Gallery as source of truth** -- manifest stores only deviations + captured old values
2. **Three-value model** -- `default_value` / captured machine state / desired value per registry entry
3. **Multi-mechanism tweak definition** -- `registry:` + `script:` + `undo_script:` in one YAML entry
4. **Dependency links** -- `suggests:` (soft) and `requires:` (hard) between gallery entries
5. **App-owned tweaks** -- app entry includes its bad behavior toggles (context menu additions, startup entries)
6. **Unified tree taxonomy** -- deep category paths like `Apps/Languages/.NET/Editors/Visual Studio`
7. **Detect-first flow** -- scan machine on first run, show what's already in place before configuring
8. **Smart status cards** -- mechanism-aware status display
9. **Retire .reg files** -- migrate perch-config .reg files to YAML

#### Phase 3: Import & Sourcing

1. License check per source repo
2. WinUtil importer -- parse `tweaks.json`, generate gallery YAML stubs, human review
3. Sophia Script as deep catalog (after WinUtil proves the pattern)
4. Unified import + dedup pipeline across sources
5. O&O ShutUp10 / Winaero Tweaker as additional sources

#### Phase 4: Backlog

Good ideas, not blocking:

- Profile color coding on cards/tree nodes
- OS-version-aware tree (hide entries for wrong version)
- Universal "Open Location" button (regedit, Explorer, services.msc, Fonts folder)
- Inline value display (current / desired / default) on tweak detail
- Per-machine toggle with active machine tracking
- Startup items show source location (Run key, Task Scheduler, shell:startup) with direct link
- Drift dashboard: unified view across all mechanism types
- Font installation via winget or file copy
- First deploy captures current value per machine
- Revert options: "restore my previous" vs "restore Windows default"

### Smart Status Values by Type

**Registry tweaks:**
- **Applied** -- current value matches desired value
- **Drifted** -- current value differs from desired (something changed it)
- **Not Applied** -- in config but never deployed
- **Reverted** -- user explicitly restored to default/previous
- **Error** -- couldn't apply (access denied, path doesn't exist)

**Apps:**
- **Installed** -- app is present on the machine
- **Not Installed** -- in config but not on machine
- **Update Available** -- newer version exists

**Dotfiles/Config:**
- **Linked** -- symlink in place, correct target
- **Broken** -- symlink exists but target wrong/missing
- **Not Linked** -- in config but symlink not created
- **Modified** -- file has local changes (git dirty)

**Fonts:**
- **Installed** -- font registered in system
- **Not Installed** -- in config but not on machine

**Cross-cutting:**
- **Skipped** -- excluded for this machine via per-machine config
- **Unavailable** -- wrong OS version (hidden from UI)

### Key Architectural Decisions

1. **Unified tree, not separate catalogs** -- apps, tweaks, fonts, dotfiles all in one navigable tree
2. **Gallery is source of truth** -- manifests store only deviations from gallery defaults
3. **Three-value model** -- default / captured / desired enables drift detection without prior deploy
4. **Multi-mechanism** -- registry + PowerShell + fonts now, services/tasks/group policy later as needed
5. **Detect-first** -- always start from current machine state, not blank config
6. **App-owned tweaks** -- no artificial separation between installing an app and cleaning up after it
7. **Website is marketing only** -- WPF app is the primary gallery browser
8. **One-time import scripts** -- not a framework, not ongoing sync
9. **Human visual verification** -- automated CI checks registry paths exist, humans verify actual effect

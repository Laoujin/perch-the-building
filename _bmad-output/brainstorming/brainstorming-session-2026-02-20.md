---
stepsCompleted: [1, 2, 3]
inputDocuments: []
session_topic: 'Perch-Gallery catalog curation and configuration'
session_goals: 'Comprehensive audit of all entries, taxonomy redesign, sorting logic, detection fixes, badge strategy, actionable stories'
selected_approach: 'ai-recommended'
techniques_used: ['question-storming', 'morphological-analysis', 'solution-matrix']
ideas_generated: [15]
context_file: ''
technique_execution_complete: true
---

# Brainstorming Session Results

**Facilitator:** Wouter
**Date:** 2026-02-20

## Session Overview

**Topic:** Perch-Gallery catalog curation and configuration
**Goals:**
1. Comprehensive audit + action plan for every catalog entry (data completeness, GitHub links, stars, badges)
2. Taxonomy redesign -- rethink the 3 main categories (Languages, Dotfiles, Apps) and define subcategories
3. Sorting logic within categories and subcategories
4. Detection system reliability fixes
5. Badge strategy (hot, suggested, and others)
6. Produce concrete stories/issues for implementation

### Session Setup

_Full-scope brainstorming covering the entire Perch-Gallery catalog configuration. The session will explore taxonomy, metadata, sorting, detection, and badges -- producing actionable stories._

## Technique Selection

**Approach:** AI-Recommended Techniques
**Analysis Context:** Perch-Gallery catalog curation with focus on taxonomy, metadata, sorting, detection, badges

**Recommended Techniques:**

- **Question Storming:** Map the full decision space before jumping to solutions
- **Morphological Analysis:** Systematically explore all parameter combinations across catalog dimensions
- **Solution Matrix:** Grid every entry against its final configuration and produce actionable stories

**AI Rationale:** Multi-dimensional information architecture problem requiring systematic exploration before solution design. The progressive depth (questions -> parameters -> decisions) ensures nothing is missed.

## Technique Execution Results

### Question Storming

**78 questions generated** across 7 dimensions: taxonomy, detection, badges, sorting, metadata, kind values, navigation.

**Key decisions made:**
- 8 top-level categories (down from 13+ candidates)
- Dotfiles + Terminal merged into "Terminal"
- Languages become top-level with drill-down cards
- Suggested tier dropped -- hot only (manually curated)
- `kind` field removed -- replaced by `cli-tool: true` flag + category placement
- `hidden` field removed entirely
- Sort order moves from C# code to YAML
- Detection expanded: config link targets + perch-config manifest existence
- Profiles kept for Wizard + Tweaks, removed from Apps view

### Morphological Analysis

**6 parameter axes mapped:**

1. **Category Taxonomy** -- 8 categories with full subcategory trees
2. **YAML Schema** -- fields added (sort, cli-tool), removed (kind, hidden), updated (category)
3. **categories.yaml format** -- 3 levels, sort fields, pattern field (drill-down/direct)
4. **Sort Order** -- status > sort field > alphabetical, defined in YAML
5. **Detection** -- 4 methods (winget/choco, install.detect, config target, perch-config manifest)
6. **UI Pattern** -- drill-down (Languages, Essentials) vs direct (all others)

### Solution Matrix

**15 stories produced** -- see Stories section below.

## Final Taxonomy

| # | Category | Pattern | Subcategories |
|---|----------|---------|---------------|
| 1 | Languages | Drill-down | .NET, Node, Python, Rust, Go, Ruby, Java, C/C++, PHP -- each with Runtimes, Version Managers, Package Managers, IDEs, Global Packages (per language) |
| 2 | Terminal | Direct | Shells/Bash, Shells/PowerShell, Terminal Apps, CLI Tools, Git, SSH |
| 3 | Development | Direct | IDEs & Editors, API Tools, Databases, Containers, Diff Tools |
| 4 | Essentials | Drill-down | Browsers, Communication, Passwords, Office, Note-Taking, Cloud Storage, Compression, Screenshots, Clipboard, PDF, Downloads, FTP, Window Managers |
| 5 | Media | Direct | Graphics, Video, Audio, Other |
| 6 | Gaming | Direct | Launchers, Controllers, Modding, Streaming, Performance |
| 7 | Power User | Direct | File Management, System Monitors, System Tools, Networking |
| 8 | Companion Tools | Direct | Flat list: alternatives + complementary tools to Perch |

## Schema Changes

**Removed:** `kind` (93 entries), `hidden` (128 entries)
**Added:** `cli-tool: true` (boolean flag), `sort` (numeric)
**Updated:** `category` (all 261 entries remapped)

**Detection logic (code changes):**
1. Winget/Choco package match (existing)
2. `install.detect` PATH/file check (existing, expand to more entries)
3. Config link target existence (NEW)
4. Manifest in perch-config (NEW)

**Sort priority within subcategory:**
1. Synced → 2. Drifted → 3. Detected → 4. Hot + unmanaged → 5. Unmanaged (by sort, then alpha) → 6. CLI tools last

## Stories

### Story 1: Redesign categories.yaml with new taxonomy
**Repo:** perch-gallery
Replace current categories.yaml with 8-category structure. Add `pattern` field (drill-down/direct), `sort` fields at every level, support 3 levels of nesting.

### Story 2: Remove hardcoded subcategory sort order from AppsViewModel
**Repo:** Perch
**Blocked by:** Story 1
Delete `_subcategoryOrder` dictionary from C# code. Read sort order from categories.yaml instead.

### Story 3: Re-categorize all 261 entries to new taxonomy
**Repo:** perch-gallery
**Blocked by:** Story 1
Update `category` field on every YAML entry to match new taxonomy paths.

### Story 4: Remove `kind` field, add `cli-tool` flag
**Repo:** Both
Remove `kind` from all 93 entries. Add `cli-tool: true` to entries that were `kind: cli-tool`. Update CatalogEntry model, CatalogParser, and code filtering on `kind: dotfile` to filter by category instead.

### Story 5: Remove `hidden` field from all entries
**Repo:** Both
Remove `hidden: true` from 128 entries. Remove Hidden property from model and UI code.

### Story 6: Add `sort` field to catalog entries
**Repo:** Both
**Blocked by:** Story 2
Add Sort property to CatalogEntry model. Update parser and sorting logic. Add sort values to entries needing explicit ordering.

### Story 7: Implement drill-down pattern for Essentials
**Repo:** Perch
**Blocked by:** Story 2
Generalize the Languages drill-down view to work for any `pattern: drill-down` category.

### Story 8: Update sidebar navigation for 8 categories
**Repo:** Perch
**Blocked by:** Story 7
Replace current navigation with 8 top-level items.

### Story 9: Remove Suggested tier from Apps view
**Repo:** Perch
Remove CardTier.Suggested, IsSuggested from AppCardModel, profile-based tier assignment for apps. Keep profiles for Wizard + Tweaks.

### Story 10: Detection -- config link target existence
**Repo:** Perch
Check if `config.links[].target` file/folder exists. If present (even not symlinked), mark as detected.

### Story 11: Detection -- perch-config manifest existence
**Repo:** Perch
If YAML manifest exists in perch-config for an app, treat as detected.

### Story 12: Add `install.detect` to entries with detection gaps
**Repo:** perch-gallery
**Blocked by:** Story 3
Audit entries lacking winget/choco coverage. Add `install.detect` paths for Docker Desktop, Oh My Posh, Postman, Hoppscotch, Robot3T, DB Browser, WinDirStat, etc.

### Story 13: Audit and add missing GitHub links
**Repo:** perch-gallery
~100 entries missing `links.github`. Add URLs where available.

### Story 14: Validation script for missing fields
**Repo:** perch-gallery
Script checking all entries for: description, category (valid path in categories.yaml), links.github. Flags missing data.

### Story 15: Retire index.yaml
**Repo:** Both
Build index from individual YAML files at runtime. Remove generate-index.mjs dependency.

## Execution Order

```
Story 1  (categories.yaml)
  ├── Story 2  (remove hardcoded sort)
  │     ├── Story 6  (sort field on entries)
  │     └── Story 7  (drill-down for Essentials)
  │           └── Story 8  (sidebar navigation)
  ├── Story 3  (re-categorize 261 entries)
  │     ├── Story 12 (add install.detect values)
  │     └── Story 13 (add GitHub links)
  ├── Story 4  (remove kind, add cli-tool)
  └── Story 5  (remove hidden)

Independent:
  Story 9   (remove Suggested tier)
  Story 10  (detection: config target)
  Story 11  (detection: perch-config manifest)
  Story 14  (validation script)
  Story 15  (retire index.yaml)
```

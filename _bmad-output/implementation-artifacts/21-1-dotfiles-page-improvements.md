# Story 21.1: Dotfiles Page Improvements

Status: ready-for-dev

## Story

As a Perch Desktop user,
I want the Dotfiles page to show a curated flat grid of cross-cutting config files with status-aware sorting and summary badges,
so that I can quickly see which dotfiles are managed, drifted, or detected, and bring them under Perch management.

## Design Source

`_bmad-output/design-thinking-2026-02-19.md` — Dotfiles Page prototype, unified page architecture, and Priority 4 alignment notes.

## Design Decisions

1. **Flat grid, no sub-categories.** The curated gallery keeps this page intentionally small (~8-10 items). No category drill-in like Apps or Languages.
2. **Cross-cutting dotfiles only.** Git, PowerShell, Claude, SSH, .editorconfig, .wslconfig. Language-specific dotfiles (.npmrc, nuget.config, global.json, bunfig.toml) live on the Languages page under their ecosystem.
3. **Same card components as Languages/Apps.** AppCard with status badges, action buttons (post 22-5), same sort logic.
4. **Sort: Drifted -> Detected -> Synced -> unmanaged**, then gallery sort index within status group.
5. **Status summary badges in header** (like Languages page) — synced (green), drifted (amber), detected (blue) pill counts.
6. **Gear icon on dotfiles with a detail page.** Git config has tweaks (enable git-lfs, set default editor). Simple config files without tweaks have no gear — the card is the complete experience.
7. **No filesystem scanning.** Gallery defines which dotfiles exist. If it's not in the gallery, it doesn't show up. Manual onboarding is the escape hatch (future).

## Current State

The DotfilesPage exists with:
- CardGalleryLayout with grid/detail split
- AppCard cards in a WrapPanel (no sorting)
- Search + refresh
- Detail panel with file statuses, module info, manifest details, alternatives
- Old ToggleSwitch interaction (will be updated by 22-5)
- `DetectDotfilesAsync()` loads dotfile catalog entries and checks filesystem status

## What Changes

### Header
- Add status summary badges (synced/drifted/detected pill counts) matching Languages page pattern
- Keep search + refresh

### Grid
- Add sort: Drifted -> Detected -> Synced -> unmanaged, then by gallery sort index
- Remove the detail panel side-split (the grid/detail toggle via CardGalleryLayout) — dotfiles are simple enough that inline expand (when 22-4 lands) or a condensed detail below the grid is sufficient
- Gear icon visibility: only show on cards that have tweaks or rich detail (e.g., Git). Cards without tweaks have no gear — "Add to Perch" / status badge is the full experience

### Data
- Filter out language-owned dotfiles from `DetectDotfilesAsync()` — entries whose category starts with `Languages/` should not appear here (they belong on the Languages page)
- Tier assignment: Detected items sort first, then Synced, then unmanaged gallery items

## Acceptance Criteria

1. **Status summary badges in header.** Synced (green), Drifted (amber), Detected (blue) pill counts appear next to the title, matching the Languages page pattern. Hidden when count is zero.
2. **Sort order applied.** Cards sort: Drifted/Broken first, then Detected, then Synced, then unmanaged. Within each status group, sort by gallery sort index (or name alphabetically).
3. **Cross-cutting only.** Dotfiles with categories under `Languages/*` do not appear on this page.
4. **Gear icon conditional.** The configure/gear button on AppCard only appears for dotfiles that have tweaks or a meaningful detail page. Simple config-only entries hide the gear.
5. **Flat grid, no categories.** No sub-category headers or grouping — all dotfiles in a single flat WrapPanel.
6. **Linked/total counter updated.** The "X of Y linked" subtitle reflects the filtered (cross-cutting only) count.
7. **All existing tests pass.** No regressions.

## Tasks / Subtasks

- [ ] Task 1: Add status summary badges to header (AC: #1)
  - [ ] Add synced/drifted/detected count properties to `DotfilesViewModel`
  - [ ] Add pill badge Border elements in header (copy pattern from `LanguagesPage.xaml` header)
  - [ ] Wire counts from `ApplyFilter()` using new status enum values

- [ ] Task 2: Implement sort order (AC: #2)
  - [ ] Add `StatusSortOrder` method to `DotfilesViewModel` (Drifted=0, Detected=1, Synced=2, unmanaged=3)
  - [ ] Apply `.OrderBy(StatusSortOrder).ThenBy(Name)` in `ApplyFilter()`

- [ ] Task 3: Filter out language-owned dotfiles (AC: #3)
  - [ ] In `ApplyFilter()` (or at data load), exclude entries where `Category.StartsWith("Languages/", OrdinalIgnoreCase)`
  - [ ] Alternatively, add a filter in `DetectDotfilesAsync()` or its caller

- [ ] Task 4: Conditional gear icon (AC: #4)
  - [ ] Add `HasDetailPage` computed property to `AppCardModel` (true when entry has tweaks, or rich config worth drilling into)
  - [ ] Bind configure button visibility to `HasDetailPage` on AppCard (or pass through a new DP)
  - [ ] For now: Git = has detail, everything else = no detail (refine later as gallery grows)

- [ ] Task 5: Simplify layout — remove detail panel side-split (AC: #5)
  - [ ] Replace `CardGalleryLayout.DetailContent` with a simpler layout — either remove it entirely (cards are self-contained) or keep a minimal collapsed detail below the grid
  - [ ] The detail information (file statuses, module links) can move into the inline expand pattern when 22-4 lands

- [ ] Task 6: Update linked/total counter (AC: #6)
  - [ ] `LinkedCount` and `TotalCount` should count only cross-cutting dotfiles (post-filter)

- [ ] Task 7: Tests (AC: #7)
  - [ ] Verify sort order in ViewModel test
  - [ ] Verify language-owned dotfiles are excluded
  - [ ] Build passes with zero warnings

## Files to Modify

| File | Change |
|------|--------|
| `src/Perch.Desktop/ViewModels/DotfilesViewModel.cs` | Add status counts, sort logic, language filter, StatusSortOrder |
| `src/Perch.Desktop/Views/Pages/DotfilesPage.xaml` | Add header status badges, simplify/remove detail panel |
| `src/Perch.Desktop/Views/Pages/DotfilesPage.xaml.cs` | Remove detail-related handlers if detail panel removed |
| `src/Perch.Desktop/Models/AppCardModel.cs` | Add `HasDetailPage` computed property (optional, may defer) |

## Dependencies

- **22-5 (AppCard finish)** must land first — this story assumes the new action button + status model are in place. If working in parallel, use the current ToggleSwitch and update when 22-5 merges.

## Constraints

- **No new NuGet packages.**
- **Win10 safe.** Hardcoded opaque colors, no DynamicResource theme brushes for backgrounds.
- **Gallery content (stories 21-2 through 21-5)** is separate — this story is about the page, not about creating gallery YAML entries for specific dotfiles.

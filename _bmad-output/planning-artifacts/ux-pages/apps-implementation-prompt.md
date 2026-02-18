# Implementation Prompt: Apps Page Redesign

Paste this into a new Claude Code session in the `dotfiles/Perch` directory.

---

## Prompt

I need you to implement the Apps page UX redesign for the Perch Desktop WPF app.

### Context

Perch is a Windows dotfiles manager with a WPF Desktop app (wizard + dashboard). Read these files first:

1. **`CLAUDE.md`** -- project conventions, build/test commands
2. **`_bmad-output/planning-artifacts/ux-pages/apps.md`** -- the UX spec you're implementing (READ THIS FULLY)
3. **`_bmad-output/planning-artifacts/desktop-spec.md`** -- current implementation state (what exists today)
4. **`_bmad-output/planning-artifacts/ux-design-specification.md`** -- global design system (colors, typography, spacing)
5. **`_bmad-output/planning-artifacts/gallery-schema.md`** -- YAML catalog structure (app fields, install, config, requires, suggests, etc.)

### What exists today

- `src/Perch.Desktop/Views/Pages/AppsPage.xaml` (462 lines) -- current page with 3-level drill-in navigation (categories → category detail → app detail)
- `src/Perch.Desktop/Views/Pages/AppsPage.xaml.cs` (76 lines) -- code-behind routing events
- `src/Perch.Desktop/ViewModels/AppsViewModel.cs` (293 lines) -- ViewModel with category drill-in, Link/Unlink/Fix commands
- `src/Perch.Desktop/Views/Controls/AppCard.xaml` (145 lines) -- card control with Link/Unlink/Fix buttons, 320px wide
- `src/Perch.Desktop/Views/Controls/AppCard.xaml.cs` (180 lines) -- code-behind with routed events
- `src/Perch.Desktop/Models/AppCardModel.cs` (77 lines) -- card model with Status, BroadCategory, SubCategory, MatchesSearch
- `src/Perch.Desktop/Models/AppCategoryCardModel.cs` (44 lines) -- category card model with icon mapping
- `src/Perch.Desktop/Models/AppDetail.cs` (15 lines) -- record for detail view (Card, OwningModule, Manifest, Alternatives)
- `src/Perch.Desktop/Models/CardStatus.cs` -- enum: NotInstalled, Detected, Selected, Linked, Drift, Broken, Error
- `src/Perch.Desktop/Models/CardTier.cs` -- enum: YourApps, Suggested, Other
- `src/Perch.Desktop/Views/Controls/StatusRibbon.xaml` -- status ribbon control
- `src/Perch.Desktop/Services/IAppDetailService.cs` / `AppDetailService.cs` -- loads manifest, module, alternatives
- `src/Perch.Desktop/Services/IGalleryDetectionService.cs` / `GalleryDetectionService.cs` (484 lines) -- detects installed apps, classifies into tiers
- `src/Perch.Desktop/Views/WizardWindow.xaml` -- wizard with Apps step
- `tests/Perch.Core.Tests/Desktop/GalleryDetectionServiceAppTests.cs` (349 lines) -- app detection tests
- `tests/Perch.Core.Tests/Desktop/AppDetailServiceTests.cs` (138 lines) -- detail service tests

### Implementation plan

Work in small commits on master. Each step should build and tests should pass.

**Step 1: Replace drill-in with flat tiers**
- Remove the 3-level navigation (categories → category detail → app detail)
- Replace with a single scrollable page showing three tiers vertically:
  - Tier 1: "Your Apps" -- apps with status Linked/Drifted/Broken/Detected
  - Tier 2: "Suggested for You" -- apps matching user profiles, not in Tier 1
  - Tier 3: "Browse All" -- collapsed category cards that expand inline
- Tier 1 and 2 show flat card grids. Tier 3 shows category cards; clicking expands that category inline (no page navigation)
- Within expanded categories, group apps by SubCategory. Apps with `kind: cli-tool` or `install.dotnet-tool` group under "CLI Tools" heading
- Keep the existing `TierSectionHeader` pattern for tier labels
- Each tier hidden when empty (no empty state UI, just hidden)
- Header shows status summary: "X linked, Y drifted, Z detected" (dashboard) or "X selected" (wizard)

**Step 2: Replace Link/Unlink/Fix with toggle**
- Remove the three action buttons from AppCard
- Add a single toggle switch (ToggleSwitch) to the top-right corner of each card
- Toggle ON = Perch manages this app's config (creates symlinks)
- Toggle OFF = stop tracking (symlinks remain, status reverts to Detected)
- Toggle **disabled** for Not Installed apps (user must install first)
- Default state: OFF unless already Linked
- Snackbar feedback on toggle: "VS Code config linked. 2 files symlinked." / "No longer managing VS Code config."
- In wizard: toggle = select for deploy (deferred action)
- Remove `CanLink`, `CanUnlink`, `CanFix` from AppCardModel
- Remove `LinkAppCommand`, `UnlinkAppCommand`, `FixAppCommand` from ViewModel -- replace with single `ToggleAppCommand`

**Step 3: Card redesign**
- Card width: 280px (down from 320px)
- Layout top to bottom:
  1. Status ribbon + badges + toggle (top row)
  2. App name (`display-name` ?? `name`, 13px SemiBold)
  3. Description (11px #888, max 2 lines)
  4. Tags as clickable pills (10px, #666 on #222240, max 3 visible + "+N" overflow)
  5. GitHub stars (clickable → opens GitHub URL) + expand chevron (bottom row)
- Suggested badge: shown if app matches user profiles and is in Tier 2 (green outline pill)
- Kind badge: `cli-tool` / `runtime` / `dotfile` as subtle pill. `app` not shown.
- Border: green #10B981 when toggle ON, default #2A2A3E
- Website link: small icon next to stars if `links.website` exists

**Step 4: Expandable card detail**
- Clicking expand chevron (▶ / ▼) reveals detail sections inline below the card
- Sections in order:
  1. **Requires** (top, amber styling) -- "Requires: .NET SDK" with clickable link. Only if `requires[]` exists.
  2. **Config Links** -- source → target path per link, with symlink status icon (✓/✗/○)
  3. **Install** -- package manager IDs (winget, choco, dotnet-tool, node-package), copiable text
  4. **Extensions** -- bundled + recommended extension IDs. Only if `extensions` exists.
  5. **App Tweaks** -- app-owned tweaks as mini tweak cards with toggle + expandable registry. Only if `tweaks[]` exists.
  6. **Alternatives** -- clickable links to alternative apps (scroll to / highlight). Only if alternatives exist.
  7. **Also consider** -- clickable links from `suggests[]`. Only if suggests exist.
  8. **Footer** -- license badge + OS platform pills
- Reuse/adapt the existing `AppDetail` loading from `IAppDetailService`

**Step 5: Dependency grouping (Option C)**
- Build dependency graph: for each app, find all apps where `requires` contains this app's ID (invert the relationship)
- Apps that `requires` another app are hidden from top-level tier display
- They appear as nested compact cards when the parent app is expanded, in a "Related Apps" section
- Nested cards grouped by SubCategory (e.g., "IDEs", "Tools", "CLI Tools")
- Nested cards: compact variant (160px, name + status + primary tag + toggle, no stars/description/expand)
- Clicking a nested card navigates to a **detail screen** (new view, not inline expand). Back button returns to parent's expanded state.
- Parent apps show a "N related apps" badge on the collapsed card
- Circular `requires`: detect in Core, treat both as top-level
- Dependency rule applies in all three tiers and within expanded categories

**Step 6: Search and tag filtering**
- Search box in header: "Search name or tag..."
- Matches against: `name`, `display-name`, `tags[]`, `description` (case-insensitive)
- Filters all three tiers simultaneously
- Category cards in Tier 3 hide when 0 apps match within them
- When search active, matching categories auto-expand to show results
- Related/nested apps also searched (parent shows if any child matches)
- Clicking a tag pill on any card fills the search box with that tag text
- Keep the existing `MatchesSearch()` on AppCardModel, extend it to also match tags

**Step 7: Wizard sync**
- Update WizardWindow.xaml Apps step to match new flat-tier + card design
- Use same card templates (toggle top-right, tags, expandable detail)
- Wizard toggle = select for deploy (deferred, no immediate symlink)
- Header shows "X selected" count
- Wizard shows read-only detail in expanded view (what will be linked)

### Design decisions for open questions

If you hit these during implementation, use these defaults:

1. **Nested card detail**: Navigate to a detail screen (not inline expand) to avoid visual nesting overload. Back button returns to parent's expanded state.
2. **Circular dependencies**: Core detects cycles and treats both apps as top-level.
3. **`requires` vs `suggests`**: `requires` hides child from top-level + shows at top of detail with amber styling. `suggests` only shows as "Also consider" links in expanded detail.
4. **Apps not in gallery**: Out of scope. Only gallery-matched apps shown. Users can author their own YAML for unlisted apps.
5. **Toggle for Not Installed**: Disabled. User must install the app first.
6. **App-owned tweaks in expanded detail**: Show as mini tweak cards with same toggle + expandable registry pattern as the Windows Tweaks page.

### Testing

- Run `dotnet test` after each step -- all existing tests must pass
- Add ViewModel tests for new behavior: tier building, dependency graph, toggle logic, search filtering, tag click
- Existing tests in `tests/Perch.Core.Tests/Desktop/GalleryDetectionServiceAppTests.cs` and `AppDetailServiceTests.cs` must continue passing
- Launch the app (`powershell -ExecutionPolicy Bypass -File launch-dev.ps1`) to verify visually after steps 1, 3, 5, 6
- Tests are in `tests/Perch.Core.Tests/Desktop/` -- follow existing patterns

### Constraints

- Don't modify Perch.Core unless strictly necessary (e.g., dependency graph inversion method needed)
- Don't add new NuGet packages
- Follow existing code patterns (CommunityToolkit.Mvvm, ObservableProperty, RelayCommand)
- Keep XAML consistent with existing style (dark theme colors, spacing from ux-design-specification.md)
- Commit after each step with a descriptive message

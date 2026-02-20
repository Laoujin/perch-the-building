# Story 22.4: App Detail Page (Expandable Card)

Status: ready-for-dev

## Story

As a Perch Desktop user,
I want to expand an app card inline to see its full detail (config links, install commands, extensions, tweaks, dependencies, alternatives),
so that I can understand and manage an app's configuration without navigating away from the Apps page.

## Acceptance Criteria

1. **Inline expand/collapse**: Clicking the expand chevron on any AppCard reveals a detail section below the card content. Clicking again (or the collapse chevron) hides it. No page navigation occurs.
2. **Requires section**: When the app has `requires[]`, a prominent amber-styled section appears at the top of the detail showing required dependencies as clickable links (scroll-to/highlight that app on the page).
3. **Config Links section**: Each `config.links[]` entry shows source filename and resolved target path. Per-link symlink status icon: checkmark (linked), X (broken), circle (not linked). Uses existing `DotfileFileStatus` from `AppDetailService`.
4. **Install section**: Shows package manager IDs (winget, choco, dotnet-tool, node-package) as copiable text. Only if `install` exists on the catalog entry.
5. **Extensions section**: Shows bundled and recommended extension IDs in separate sub-lists. Only if `extensions` exists on the catalog entry.
6. **App Tweaks section**: App-owned tweaks rendered as mini tweak cards with individual toggles and expandable registry detail (key/name/value). Only if the catalog entry has `tweaks[]`.
7. **Alternatives section**: "Alternatives: X, Y" as clickable links that scroll to and highlight those apps. Only if `alternatives[]` exists.
8. **Also Consider section**: "Also consider: X, Y" from `suggests[]` as clickable links. Only if `suggests[]` exists.
9. **Footer**: License badge + OS platform pills (windows/linux/macos). Always shown.
10. **Related Apps section**: When the app has `DependentApps` (reverse requires — other apps that depend on this one), show a "Related Apps (N)" section with compact nested cards (160px wide) grouped by SubCategory. Nested cards show: name, status badge, primary tag, toggle. No stars/description/expand chevron.
11. **Nested card click**: Clicking a compact nested card sets it as `SelectedApp` and opens the existing detail panel (right side). Back button returns to the parent card's expanded state.
12. **Detail data loading**: Expanding a card triggers `AppDetailService.LoadDetailAsync()` if not already cached on the model. Show a loading spinner inside the expanded area while loading.
13. **All existing tests pass** after implementation. New ViewModel tests cover: expand/collapse toggle, detail loading, requires rendering, file status display.

## Tasks / Subtasks

- [ ] Task 1: Replace side-panel detail with inline card expansion (AC: #1, #12)
  - [ ] Add `IsExpanded` observable property to `AppCardModel`
  - [ ] Add expand/collapse chevron button to `AppCard.xaml` (bottom-right, toggles `IsExpanded`)
  - [ ] Add `ExpandedContent` area in `AppCard.xaml` that shows/hides based on `IsExpanded`
  - [ ] Wire `ConfigureClicked` or new `ExpandClicked` routed event to toggle `IsExpanded` on ViewModel
  - [ ] On expand: call `AppDetailService.LoadDetailAsync()` if `card.Detail == null`; set `IsLoadingDetail` during load
  - [ ] Show `ProgressRing` in expanded area while `IsLoadingDetail` is true
  - [ ] Remove or repurpose the right-side detail panel from `AppsPage.xaml` (the grid split)

- [ ] Task 2: Requires section (AC: #2)
  - [ ] In expanded detail XAML, add a `Requires` ItemsControl at the top
  - [ ] Style with amber background (#F59E0B at 15% opacity), amber text
  - [ ] Each item is a clickable TextBlock/Hyperlink with the required app's display name
  - [ ] Click handler: find the required app in the ViewModel's app list, scroll to it, highlight it briefly

- [ ] Task 3: Config Links section (AC: #3)
  - [ ] In expanded detail, add a "Config Links" section
  - [ ] ItemsControl bound to `Detail.FileStatuses` (already computed by `AppDetailService`)
  - [ ] Each row: status icon (checkmark/X/circle) + source filename + arrow + resolved target path
  - [ ] Status icon mapping: `Linked` → green checkmark, `Broken/Drift` → red X, `NotInstalled/Detected` → gray circle

- [ ] Task 4: Install section (AC: #4)
  - [ ] Show install IDs from `card.CatalogEntry.Install` (winget, choco, dotnet-tool, node-package)
  - [ ] Each line: label + copiable text (click-to-copy or selectable TextBox)
  - [ ] Only render section if Install has any non-null values

- [ ] Task 5: Extensions section (AC: #5)
  - [ ] Show bundled and recommended extension IDs from `card.CatalogEntry.Extensions`
  - [ ] Two sub-lists: "Bundled" and "Recommended"
  - [ ] Only render section if Extensions exists and has entries

- [ ] Task 6: App Tweaks section (AC: #6)
  - [ ] Show app-owned tweaks from catalog entry's `tweaks[]`
  - [ ] Each tweak: name + toggle + expandable registry detail (key, name, value, type)
  - [ ] Tweak toggle follows same pattern as System Tweaks page cards
  - [ ] Only render section if tweaks exist

- [ ] Task 7: Alternatives and Also Consider sections (AC: #7, #8)
  - [ ] "Alternatives: X, Y" line with clickable app names
  - [ ] "Also consider: X, Y" line with clickable app names from `suggests[]`
  - [ ] Click scrolls to and highlights the target app on the page
  - [ ] Only render each line if the respective array is non-empty

- [ ] Task 8: Footer with license and platform pills (AC: #9)
  - [ ] License badge from `card.CatalogEntry.License` (if present)
  - [ ] OS pills: small colored pills for each platform in `card.CatalogEntry.Platforms`
  - [ ] Always rendered at the bottom of expanded detail

- [ ] Task 9: Related Apps / Dependency Tree (AC: #10, #11)
  - [ ] "Related Apps (N)" section using `card.DependentApps`
  - [ ] Group by `SubCategory` with sub-headers
  - [ ] Compact card template (160px): name + StatusRibbon + primary tag + toggle
  - [ ] Clicking a compact card calls `ConfigureAppCommand` (opens detail panel or navigates)
  - [ ] Preserve parent card's expanded state when navigating back

- [ ] Task 10: Tests (AC: #13)
  - [ ] ViewModel test: expand card sets `IsExpanded = true` and triggers detail load
  - [ ] ViewModel test: collapse card sets `IsExpanded = false`
  - [ ] ViewModel test: expand with cached detail does not re-call service
  - [ ] ViewModel test: Requires section populated from catalog entry
  - [ ] ViewModel test: FileStatuses mapped to correct status icons
  - [ ] Ensure all existing `AppDetailServiceTests` and `GalleryDetectionServiceAppTests` pass

## Dev Notes

### Current Architecture (What Exists)

**Detail loading is already built.** `AppDetailService.LoadDetailAsync()` returns an `AppDetail` record with:
- `OwningModule`, `Manifest`, `ManifestYaml`, `ManifestPath`
- `Alternatives` (same-category apps from catalog)
- `FileStatuses` (per-link symlink detection with `DotfileFileStatus` records: FileName, FullPath, Exists, IsSymlink, Status, Error)

**Dependency graph is already computed.** `AppsViewModel.BuildDependencyGraph()` populates `AppCardModel.DependentApps` (reverse requires) and `ComputeTopPicks()`. `EcosystemGroups` are already built for the current side-panel detail view.

**The current detail view is a side panel** — `AppsPage.xaml` uses a grid split: left = card grid, right = detail panel (shown when `SelectedApp != null`). This story replaces the side panel with inline expansion on the card itself.

### Key Files to Modify

| File | Change |
|------|--------|
| `src/Perch.Desktop/Views/Controls/AppCard.xaml` | Add expanded detail XAML sections below existing card content |
| `src/Perch.Desktop/Views/Controls/AppCard.xaml.cs` | Add `IsExpanded`, `IsLoadingDetail` DependencyProperties; wire expand/collapse; add routed events for scroll-to-app |
| `src/Perch.Desktop/Models/AppCardModel.cs` | Add `[ObservableProperty] bool _isExpanded`; ensure `Detail` triggers PropertyChanged for binding |
| `src/Perch.Desktop/Views/Pages/AppsPage.xaml` | Remove right-side detail panel grid column; handle scroll-to-app for Requires/Alternatives clicks |
| `src/Perch.Desktop/Views/Pages/AppsPage.xaml.cs` | Add handler for scroll-to-app routed event; remove detail panel toggle logic |
| `src/Perch.Desktop/ViewModels/AppsViewModel.cs` | Replace `ConfigureAppAsync` → `ToggleExpandAsync`; remove `ShowGrid`/`ShowDetail` split; add scroll-to-app command |
| `src/Perch.Desktop/Models/AppDetail.cs` | No changes expected — already has all needed data |
| `src/Perch.Desktop/Services/AppDetailService.cs` | No changes expected — already loads FileStatuses and Alternatives |
| `tests/Perch.Desktop.Tests/` | New test file for expand/collapse ViewModel behavior |

### Design Constraints

- **Card width stays 280px.** Expanded detail renders full-width below the card content within the same card border.
- **Use hardcoded opaque colors for backgrounds on Win10.** `#1A1A2E` for card surface, `#16162A` for footer/section backgrounds. Do NOT use `DynamicResource` theme brushes (they're semi-transparent, render white on Win10).
- **Amber for Requires:** `#F59E0B` at 15% opacity background, `#F59E0B` text.
- **Status icon colors:** Green `#34D399` (linked checkmark), Red `#EF4444` (broken X), Muted `#888888` (not linked circle).
- **Compact nested cards:** 160px wide, minimal content. These are a separate `DataTemplate` from the full `AppCard`.
- **CommunityToolkit.Mvvm patterns:** `[ObservableProperty]` requires `partial class`. `IsExpanded` on AppCardModel (which is already `ObservableObject`).
- **No new NuGet packages.** Use existing WPF-UI 4.2.0 components (`Card`, `CardExpander`, `SymbolIcon`, `Badge`, `ToggleSwitch`).

### What NOT to Build

- No separate app detail page/window — everything is inline expansion.
- No install/uninstall functionality — just display install IDs.
- No extension install functionality — just display extension IDs.
- No tweak apply/revert — just display tweak info with toggle state. (Actual tweak application is Epic 25.)
- No edit/modify of config links — read-only display of symlink status.

### Testing Notes

- Desktop ViewModel tests go in `tests/Perch.Desktop.Tests/` (Windows-only, `net10.0-windows`).
- Follow existing patterns: `[TestFixture] public sealed class`, `Method_Scenario_ExpectedResult` naming, `NSubstitute` for mocking `IAppDetailService`.
- Existing tests in `tests/Perch.Desktop.Tests/AppDetailServiceTests.cs` (5 tests) must continue passing.
- Existing tests in `tests/Perch.Core.Tests/Desktop/GalleryDetectionServiceAppTests.cs` (349 lines) must continue passing.

### Project Structure Notes

- All Desktop code in `src/Perch.Desktop/` — this story touches only Desktop project.
- No Core changes needed — `AppDetailService` and data models already provide all required data.
- Compact nested card template can live in `AppCard.xaml` as a nested `DataTemplate` or as a separate `CompactAppCard.xaml` in `Views/Controls/`.
- Desktop tests in `Perch.Desktop.Tests` (included only in `Perch.slnx`, not `Perch.CrossPlatform.slnx`).

### References

- [Source: _bmad-output/planning-artifacts/ux-pages/apps.md#Section 6.2] — Expanded card detail layout
- [Source: _bmad-output/planning-artifacts/ux-pages/apps.md#Section 6.3] — Related Apps dependency tree
- [Source: _bmad-output/planning-artifacts/ux-pages/apps.md#Section 6.4] — Sub-group ordering
- [Source: _bmad-output/planning-artifacts/ux-pages/apps-implementation-prompt.md#Step 4-5] — Implementation steps for detail + dependency grouping
- [Source: _bmad-output/planning-artifacts/ux-design-specification.md] — Card design system, colors, spacing
- [Source: _bmad-output/planning-artifacts/gallery-schema.md] — App YAML fields (requires, suggests, alternatives, extensions, tweaks)
- [Source: _bmad-output/project-context.md] — Critical implementation rules, testing patterns, Win10 color gotchas

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

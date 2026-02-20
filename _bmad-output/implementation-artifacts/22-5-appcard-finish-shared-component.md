# Story 22.5: Finish AppCard Shared Component

Status: review

## Story

As a Perch Desktop user,
I want app cards to use clear "Add to Perch" / "Remove from Perch" action buttons instead of toggle switches, with a 5-state badge model,
so that the intent of each action is unambiguous and the card works consistently across Apps, Dotfiles, Languages, and Wizard pages.

## Design Source

`_bmad-output/design-thinking-2026-02-19.md` — Concept 2 (Configure vs Deploy split) and prototype wireframes.

## Key Design Decisions

1. **Button replaces ToggleSwitch.** "Add to Perch" on unmanaged cards, "Remove from Perch" on managed cards. Buttons feel like deliberate actions; toggles cause "did something happen?" confusion.
2. **5-state badge model** replaces the current 7-value `CardStatus` enum. The old `NotInstalled`, `Selected`, `Error` values are retired.
3. **Configure and deploy are always separate actions.** "Add to Perch" only changes perch-config (adds item). "Sync Everything" on the dashboard applies changes to the machine. No "Track & Install Now" shortcut.
4. **Pending state** is new: indicates config has changed but machine hasn't been synced yet. Two sub-flavors: pending-add (green-tinted) and pending-remove (red-tinted).
5. **Accent border** driven by new state model, not by `IsManaged` boolean.

## New State Model

| State | Badge Color | Meaning | Button Text | Border |
|-------|------------|---------|-------------|--------|
| *(none/Unmanaged)* | No badge | Gallery item, not managed, not detected | "Add to Perch" | Default |
| **Detected** | Blue | On system, not in Perch | "Add to Perch" | Default |
| **Pending** | Green-tinted (add) / Red-tinted (remove) | Config changed, waiting for sync | Opposite of pending action | Accent |
| **Synced** | Green | In Perch, matches system | "Remove from Perch" | Accent |
| **Drifted** | Amber | In Perch, system doesn't match | "Remove from Perch" | Accent |

## Acceptance Criteria

1. **ToggleSwitch removed.** The `ui:ToggleSwitch` in `AppCard.xaml` is replaced with a `ui:Button` whose content is "Add to Perch" or "Remove from Perch" based on card state.
2. **CardStatus enum updated.** New values: `Unmanaged`, `Detected`, `PendingAdd`, `PendingRemove`, `Synced`, `Drifted`. Old values (`NotInstalled`, `Selected`, `Linked`, `Broken`, `Error`) removed.
3. **StatusRibbon updated.** Maps new states to badge text/color: Detected (blue `#3B82F6`), Pending (green `#047857` or red `#DC2626`), Synced (green `#34D399`), Drifted (amber `#F59E0B`). Unmanaged state hides the ribbon entirely.
4. **Button action raises routed event.** Replace `ToggleChangedEvent` with `ActionClickedEvent` (or rename). The parent page/ViewModel handles the actual add/remove logic.
5. **Accent border driven by state.** Cards in `PendingAdd`, `PendingRemove`, `Synced`, `Drifted` show accent border. `Unmanaged` and `Detected` show default border.
6. **IsManaged derived property updated.** `IsManaged` returns true for `Synced`, `Drifted`, `PendingAdd` (already added). Returns false for `Unmanaged`, `Detected`, `PendingRemove` (removal pending).
7. **CanToggle removed.** No longer needed — the button is always visible. Its text/enabled state is driven by the card state.
8. **All consuming pages updated.** Apps, Dotfiles, Languages pages and Wizard views bind correctly to the new state model. No compile errors.
9. **All existing tests pass.** Update any tests that reference old `CardStatus` values.

## Tasks / Subtasks

- [x] Task 1: Update `CardStatus` enum (AC: #2)
  - [x] Replace enum values: `Unmanaged`, `Detected`, `PendingAdd`, `PendingRemove`, `Synced`, `Drifted`
  - [x] Update all references across codebase (find-and-replace old values)
  - [x] Update `AppCardModel.IsManaged` to use new states
  - [x] Remove `CanToggle` property from `AppCardModel`

- [x] Task 2: Update `StatusRibbon` (AC: #3)
  - [x] Update `UpdateVisual` switch expression for new states
  - [x] `Unmanaged` → hide ribbon (Collapsed)
  - [x] `Detected` → "Detected" blue `#3B82F6`
  - [x] `PendingAdd` → "Pending" green `#047857`
  - [x] `PendingRemove` → "Pending" red `#DC2626`
  - [x] `Synced` → "Synced" green `#34D399`
  - [x] `Drifted` → "Drifted" amber `#F59E0B`

- [x] Task 3: Replace ToggleSwitch with action Button in `AppCard.xaml` (AC: #1, #4, #5)
  - [x] Remove `ui:ToggleSwitch` (lines 100-106)
  - [x] Add `ui:Button` with content bound to new `ActionButtonText` property
  - [x] Button appearance: `Appearance="Primary"` for "Add to Perch", `Appearance="Secondary"` for "Remove from Perch"
  - [x] Replace `ToggleChangedEvent` with `ActionClickedEvent` in code-behind
  - [x] Update border style trigger: accent border for managed states, default for unmanaged

- [x] Task 4: Add computed properties to `AppCardModel` (AC: #6, #7)
  - [x] Add `ActionButtonText` → "Add to Perch" / "Remove from Perch" based on state
  - [x] Add `IsActionAdd` → true for Unmanaged/Detected/PendingRemove, false for others
  - [x] Update `OnStatusChanged` to notify new computed properties
  - [x] Remove `CanToggle`

- [x] Task 5: Remove `CanToggle` and `ToggleSwitch` dependency properties from `AppCard.xaml.cs` (AC: #7)
  - [x] Remove `CanToggleProperty` DependencyProperty
  - [x] Remove `CanToggle` property
  - [x] Rename `ToggleChangedEvent` → `ActionClickedEvent`
  - [x] Rename `OnToggleChanged` → `OnActionClick`

- [x] Task 6: Update consuming pages (AC: #8)
  - [x] Update `AppsPage.xaml` / `AppsViewModel.cs` — handle `ActionClicked` event instead of `ToggleChanged`
  - [x] Update `DotfilesPage.xaml` / `DotfilesViewModel.cs` — same
  - [x] Update `LanguagesPage.xaml` / `LanguagesViewModel.cs` — same (if using AppCard)
  - [x] Update any Wizard views that use AppCard
  - [x] Map old status assignment logic to new enum values

- [x] Task 7: Update tests (AC: #9)
  - [x] Fix all tests referencing old `CardStatus` values
  - [x] Add test: `ActionButtonText` returns correct text for each state
  - [x] Add test: `IsManaged` returns correct value for each state
  - [x] Build passes with zero warnings

## File List

| File | Change |
|------|--------|
| `src/Perch.Desktop/Models/CardStatus.cs` | Replaced 7-value enum with 6-value: Unmanaged, Detected, PendingAdd, PendingRemove, Synced, Drifted |
| `src/Perch.Desktop/Models/AppCardModel.cs` | Added ActionButtonText, IsActionAdd; updated IsManaged; removed CanToggle; updated OnStatusChanged |
| `src/Perch.Desktop/Models/EcosystemCardModel.cs` | Updated UpdateCounts to use Synced/Drifted |
| `src/Perch.Desktop/Views/Controls/AppCard.xaml` | Replaced ToggleSwitch with ui:Button; Style trigger for Primary/Secondary appearance |
| `src/Perch.Desktop/Views/Controls/AppCard.xaml.cs` | Removed CanToggleProperty DP; added ActionButtonTextProperty, IsActionAddProperty DPs; renamed ToggleChangedEvent → ActionClickedEvent, OnToggleChanged → OnActionClick |
| `src/Perch.Desktop/Views/Controls/StatusRibbon.xaml.cs` | Updated state-to-color mapping; Unmanaged hides ribbon; new colors per spec |
| `src/Perch.Desktop/Views/Pages/AppsPage.xaml` | Replaced CanToggle/ToggleChanged bindings with ActionButtonText/IsActionAdd/ActionClicked |
| `src/Perch.Desktop/Views/Pages/AppsPage.xaml.cs` | Renamed OnToggleChanged → OnActionClicked |
| `src/Perch.Desktop/Views/Pages/DotfilesPage.xaml` | Same as AppsPage |
| `src/Perch.Desktop/Views/Pages/DotfilesPage.xaml.cs` | Renamed OnToggleChanged → OnActionClicked |
| `src/Perch.Desktop/Views/Pages/LanguagesPage.xaml` | Same as AppsPage |
| `src/Perch.Desktop/Views/Pages/LanguagesPage.xaml.cs` | Renamed OnToggleChanged → OnActionClicked |
| `src/Perch.Desktop/ViewModels/AppsViewModel.cs` | Updated CardStatus refs to new enum; removed CanToggle guard from ToggleApp |
| `src/Perch.Desktop/ViewModels/DotfilesViewModel.cs` | Updated CardStatus.Linked → Synced |
| `src/Perch.Desktop/ViewModels/LanguagesViewModel.cs` | Updated CardStatus refs; removed CanToggle guard |
| `src/Perch.Desktop/ViewModels/SystemTweaksViewModel.cs` | Updated Drift → Drifted, NotInstalled → Unmanaged |
| `src/Perch.Desktop/Services/GalleryDetectionService.cs` | Updated all CardStatus refs: Linked→Synced, NotInstalled→Unmanaged, Drift→Drifted |
| `src/Perch.Desktop/Services/ApplyChangesService.cs` | Linked → Synced |
| `src/Perch.Desktop/Services/AppDetailService.cs` | Updated Linked→Synced, NotInstalled→Unmanaged, Drift→Drifted |
| `tests/Perch.Desktop.Tests/ViewModelTests.cs` | Updated all old enum refs; added AppCardModelTests (ActionButtonText, IsManaged, IsActionAdd per state); updated ToggleApp_NotInstalled_NoOp → ToggleApp_Unmanaged_AddsLinkChange |
| `tests/Perch.Desktop.Tests/ApplyChangesServiceTests.cs` | Updated Linked→Synced, NotInstalled→Unmanaged |
| `tests/Perch.Desktop.Tests/GalleryDetectionServiceAppTests.cs` | Updated Linked→Synced, NotInstalled→Unmanaged |
| `tests/Perch.Desktop.Tests/GalleryDetectionServiceDotfileTests.cs` | Updated Linked→Synced, Drift→Drifted |
| `tests/Perch.Desktop.Tests/GalleryDetectionServiceTweakTests.cs` | Updated NotInstalled→Unmanaged, Drift→Drifted |
| `tests/Perch.Desktop.Tests/GalleryDetectionServiceFontTests.cs` | Updated NotInstalled→Unmanaged |
| `tests/Perch.Desktop.Tests/WizardViewModelTests.cs` | Updated Linked→Synced |
| `tests/Perch.Desktop.Tests/DashboardViewModelTests.cs` | Updated NotInstalled→Unmanaged |
| `tests/Perch.Desktop.Tests/PendingChangesServiceTests.cs` | Updated NotInstalled→Unmanaged |

## Dev Agent Record

### Implementation Notes

- **Enum mapping**: NotInstalled→Unmanaged, Detected→Detected (unchanged), Selected→PendingAdd, Linked→Synced, Drift→Drifted, Broken→Drifted (merged), Error→Drifted (merged)
- **StatusRibbon**: Now hides entirely for Unmanaged state (Collapsed visibility) instead of showing "Not Installed" badge
- **Button appearance**: Uses WPF Style with DataTrigger on IsActionAdd — Primary when true (Add), Secondary when false (Remove). No custom converter needed.
- **CanToggle removal**: Removed from AppCardModel, AppCard.xaml.cs DP, all XAML bindings, and ViewModel guards. The action button is always visible/clickable.
- **Wizard views**: WizardWindow.xaml uses AppCard but never bound CanToggle or ToggleChanged — no changes needed there.

### Completion Notes

All 7 tasks completed. Build passes with 0 warnings, 0 errors. All 290 Desktop tests and 956 Core tests pass. 18 new parameterized test cases added for ActionButtonText, IsManaged, and IsActionAdd across all 6 CardStatus values.

## Change Log

- 2026-02-20: Implemented 5-state badge model, replaced ToggleSwitch with action Button, updated all consuming pages and tests

## Constraints

- **No new NuGet packages.** Use existing WPF-UI 4.2.0 `Button`.
- **No functional behavior change.** The add/remove action still raises a routed event for the parent to handle. This story is purely about the card's visual model and interaction pattern.
- **Win10 safe.** No DynamicResource theme brushes for backgrounds — use hardcoded opaque colors per existing convention.

# Desktop App Specification

Living spec for the Perch Desktop app. Documents the **current real state** as implemented in code, organized by screen. Each page/step uses a consistent format: Layout, Data, Interactions, States, Done When.

Last updated: 2026-02-17

---

## 1. Overview

**Purpose:** WPF GUI for the Perch dotfiles manager. Provides a wizard for first-run setup and a dashboard for ongoing management. Shares the Perch.Core engine with the CLI.

**Tech stack:**

| Component | Package | Version |
|-----------|---------|---------|
| UI framework | WPF UI (lepoco/wpfui) | 4.2.0 |
| Extra controls | HandyControl | 3.5.1 |
| MVVM | CommunityToolkit.Mvvm | 8.4.0 |
| DI host | Microsoft.Extensions.Hosting | 10.0.0-preview.1 |
| Runtime | .NET 10 | — |

**Theme:** Dark Fluent, forest green accent (#10B981), Mica backdrop.

**Startup routing** (`App.xaml.cs`):
- No `ConfigRepoPath` in settings → open `WizardWindow`
- Has `ConfigRepoPath` → open `MainWindow` (dashboard)
- `GalleryDetectionService.WarmUpAsync()` fires on startup (fire-and-forget)
- Unhandled exceptions → `CrashWindow` (dashboard mode) or wizard crash overlay (wizard mode)

**Desktop-specific services:** `GalleryDetectionService`, `AppDetailService`, `DotfileDetailService`. Core services via `AddPerchCore()`.

**Navigation shell** (`MainWindow.xaml`):
- `FluentWindow` — 1200x850, MinWidth=900, MinHeight=600
- `NavigationView` sidebar with `PaneDisplayMode="Left"`
- PaneToggleButton in TitleBar for sidebar collapse
- Menu items: Home, Dotfiles, Apps, System Tweaks, Startup
- Footer: Settings

---

## 2. Wizard

**Window:** `WizardWindow.xaml` — 960x720, MinWidth=960, **MinHeight=800** (bug: exceeds Height), CenterScreen, no resize grip.

**Step indicator:** ListBox styled as step bar (not HandyControl StepBar). Click-to-navigate: can go back freely, forward only via `CanNavigateToStep()` which validates prior steps.

**ViewModel:** `WizardShellViewModel` (691 lines). Dependencies: `IDeployService`, `ISettingsProvider`, `IGalleryDetectionService`, `IFontOnboardingService`.

**Footer buttons:** Back, Next, Deploy, Open Dashboard — visibility toggles per step.

### 2.1 Profile Selection

**Layout:** Hero image at top + 4 `ProfileCard` controls in a `UniformGrid`.

**Data:** Four boolean properties: `IsDeveloper` (default true), `IsPowerUser`, `IsGamer`, `IsCasual`. Multi-select allowed.

**Interactions:** Clicking a ProfileCard toggles its `IsSelected`. SelectionChanged routed event updates ViewModel booleans.

**States:** At least one profile should be selected (IsDeveloper starts true). Profile selection determines which wizard steps appear and which gallery categories surface.

**Done when:** User has selected at least one profile and clicked Next. Step list dynamically rebuilds — Developer/PowerUser adds a Dotfiles step.

### 2.2 Config Repo

**Layout:** TextBox for folder path + Browse button + "Clone from URL" option.

**Data:** `ConfigRepoPath` bound to TextBox. Validation checks path is a git repo.

**Interactions:**
- Browse opens folder dialog
- Clone option: enter git URL, clones to selected folder
- Next triggers `RunDetectionAsync()` — parallel `DetectApps`, `DetectTweaks`, `DetectDotfiles`, `DetectFonts`

**States:** Valid path → Next enabled. Invalid/empty → Next disabled. Detection runs with loading state.

**Done when:** Valid git repo path saved to settings. Detection completed for all four categories.

### 2.3 Dotfiles (conditional)

Only shown for Developer or PowerUser profiles.

**Layout:** WrapPanel of inline card templates. Each card: StatusRibbon + name + file count + ToggleSwitch.

**Data:** `Dotfiles` collection from detection results. Each `DotfileGroupCardModel` with `IsSelected` binding.

**Interactions:** Toggle selects/deselects a dotfile group. Selection counts update in real-time.

**States:** Cards show detection status via StatusRibbon. Toggle reflects selection state.

**Done when:** User has reviewed dotfiles and toggled desired selections. `SelectedDotfileCount` reflects choices.

### 2.4 Apps

**Layout:** Category drill-in. First level: `AppCategoryCardModel` cards showing category name + icon + item count. Second level: `AppCategoryGroup` with `TierSectionHeader` dividers (YourApps/Suggested/Other) + `AppCard` per app.

**Data:** `AppCategories` from detection. Cards show three-way join result (system + gallery + config).

**Interactions:**
- Click category → drill into subcategory view with Back button
- Toggle app cards to select/deselect
- StatusRibbon shows detection status

**States:** Category view ↔ subcategory detail view. Cards show CardStatus (Detected, Linked, NotInstalled, etc.).

**Done when:** User has browsed categories and selected desired apps. `SelectedAppCount` reflects choices.

### 2.5 System Tweaks

**Layout:** Category drill-in, same pattern as Apps. Fonts category gets special treatment with dual panel (Nerd Fonts + Installed Fonts).

**Data:** `Categories` from detection. Non-font tweaks: `TweakCardModel` with name + description + registry count + toggle. Fonts: `FontCardModel` with font-rendered name + toggle.

**Interactions:**
- Click category → detail view
- Fonts category: font search, family grouping, sample text preview, expand/collapse families
- Toggle to select/deselect

**States:** Category view ↔ detail view. Font detail has nested expansion for font families.

**Done when:** User has selected desired tweaks and fonts. `SelectedTweakCount` + `SelectedFontCount` reflect choices.

### 2.6 Review

**Layout:** Summary with badge counts: dotfiles / apps / tweaks / fonts selected. Deploy button.

**Data:** `SelectedDotfileCount`, `SelectedAppCount`, `SelectedTweakCount`, `SelectedFontCount`, `TotalSelectedCount`.

**Interactions:** Deploy button triggers deployment. Back button returns to previous steps.

**States:** Shows aggregated selection counts. Deploy button enabled when `TotalSelectedCount > 0`.

**Done when:** User clicks Deploy, initiating the deploy step.

### 2.7 Deploy

**Layout:** Progress ring + deploy results list. Each result shows level-specific icon (Success=green check, Warning=yellow warning, Error=red X, Info=blue info).

**Data:** `IDeployService.DeployAsync()` with `IProgress<DeployResult>`. Uses `BeforeModule` callback to deploy selected modules only. Font onboarding via `IFontOnboardingService.OnboardAsync()`.

**Interactions:** No user interaction during deploy. "Open Dashboard" button appears on completion.

**States:**
- Deploying: progress ring visible, results accumulate
- Completed: progress ring hidden, Open Dashboard button visible
- Failed: crash overlay with exception details

**Done when:** All selected items deployed. Results list shows per-item outcome. User can open the main dashboard.

### 2.8 Crash Overlay

**Layout:** Semi-transparent overlay with error message and exception details.

**Data:** `HasCrashed`, `CrashMessage` on ViewModel. `ShowCrash(Exception)` sets state.

**Interactions:** Disables all navigation. No recovery action beyond closing the window.

**Done when:** N/A — error state. User closes and restarts.

---

## 3. Dashboard Pages

### 3.1 Dashboard (Home)

**ViewModel:** `DashboardViewModel`. Dependencies: `IStatusService`, `ISettingsProvider`.

**Layout:** `DriftHeroBanner` at top → Refresh button + loading spinner → attention items list. "Setup Wizard" button always visible. No-config-repo fallback message when `HasConfigRepo=false`.

**Data:**
- `LinkedCount`, `AttentionCount`, `BrokenCount` — aggregated from `StatusResult`
- `HealthPercent` — computed health score
- `StatusMessage` — auto-generated from counts
- `AttentionItems` — `ObservableCollection<StatusItemViewModel>` for drift/error items

**Interactions:**
- Refresh button → `RefreshCommand` → `CheckAsync()` with progress reporting
- Attention items are **display-only** — no one-click fix actions (no Link/Fix/Unlink commands)
- "Setup Wizard" button opens wizard window

**States:**
- Loading: spinner visible
- Loaded: hero banner + attention list
- No config repo: fallback message, hero hidden

**Done when:** Shows accurate drift counts from `IStatusService`. Attention items list all non-OK status items with level-specific icons (Warning24 amber, DismissCircle24 red). Health percentage reflects real symlink state.

### 3.2 Apps

**ViewModel:** `AppsViewModel` (311 lines). Dependencies: `IGalleryDetectionService`, `IAppLinkService`, `IAppDetailService`, `IStartupService`.

**Layout:** Three-level navigation:
1. **Categories** — card grid of `AppCategoryCardModel` (icon + name + count)
2. **Category detail** — `FilteredCategoryApps` grouped by `AppCategoryGroup` with `TierSectionHeader` (YourApps/Suggested/Other) + `AppCard` per app
3. **App config detail** — full detail view with manifest, startup toggle

**Data:**
- `AppCategories` — from `DetectAllAppsAsync()` with category grouping
- `SelectedApp` — current app for detail view
- `AppDetail` — loaded via `AppDetailService.LoadDetailAsync()`
- Search: `SearchText` filters cards

**Interactions:**
- Category card click → drill into category
- AppCard Link/Unlink/Fix → `IAppLinkService` operations
- Configure button → loads `AppDetail` (manifest, alternatives, startup status)
- Startup toggle → `IStartupService` enable/disable
- External link buttons (Website/GitHub) → open in browser
- Back buttons at each level
**States:**
- `ShowAppCategories` — category grid
- `ShowAppDetail` — category drill-in with grouped apps
- `ShowAppConfigDetail` — individual app config
- `IsLoadingAppDetail` — loading spinner during detail fetch
- Per-card: `CardStatus` drives StatusRibbon + button visibility (CanLink/CanUnlink/CanFix)

**Done when:** Three-level drill-in works. Link/Unlink/Fix operations succeed and update card status in real-time. App detail shows manifest info. Search filters across all levels.

### 3.3 Dotfiles

**ViewModel:** `DotfilesViewModel` (169 lines). Dependencies: `IGalleryDetectionService`, `IDotfileDetailService`.

**Layout:** Two views toggled by visibility:
1. **Card grid** — WrapPanel of dotfile group cards (icon + name + file count + toggle + settings gear)
2. **Detail view** — selected dotfile with file list, manifest sections

Header shows "X of Y linked" via MultiBinding.

**Data:**
- `Dotfiles` — from `DetectDotfilesAsync()`
- `SelectedDotfile` — current group for detail
- `DotfileDetail` — loaded via `DotfileDetailService.LoadDetailAsync()`
- Search: `SearchText` filters cards
- `LinkedCount`, `TotalCount` — header stats

**Interactions:**
- Toggle switches select/deselect dotfile groups
- Settings gear → detail view with file-level status
- File status icons: Linked (green CheckmarkCircle24), Drift (orange Warning24), Unlinked (gray Document24)
- Shows owning module name or "No module found"
**States:**
- `ShowCardGrid` — card grid view
- `ShowDetailView` — detail view
- Per-group: computed status from file statuses (Linked/Drift/Broken)

**Done when:** Card grid shows all detected dotfile groups with accurate status. Detail view shows per-file status. Linked/Total counts correct.

### 3.4 System Tweaks

**ViewModel:** `SystemTweaksViewModel` (202 lines). Dependencies: `IGalleryDetectionService` only.

**Layout:** Category drill-in (same as Apps pattern). Fonts category gets special dual panel.

Non-font category detail: `FilteredTweaks` — cards with StatusRibbon + name + toggle + description + registry entry count.

Font category detail:
- Font search box (`FontSearchText`)
- Nerd Fonts section: gallery fonts with toggle
- Installed Fonts section: system fonts grouped by family, expandable
  - Group header: disclosure chevron + family name + variant count + toggle
  - Variants: name (rendered in actual font) + "Try font" / "View Font" buttons + toggle + editable sample text

**Data:**
- `Categories` — from `DetectTweaksAsync()`
- `FilteredTweaks` — non-font tweaks for selected category
- `FilteredNerdFonts` — gallery fonts
- `FilteredInstalledFontGroups` — system fonts grouped by `FontFamilyGroupModel`
- Profiles loaded from `PerchSettings.Profiles` (falls back to Developer + PowerUser if not yet saved)

**Interactions:**
- Category card click → detail view
- Toggle selects/deselects tweaks or fonts
- Font family expand/collapse
- Font sample text editing
**States:**
- `ShowCategories` — category grid
- `ShowCategoryDetail` — tweak or font detail
- Font detail has nested expand/collapse per family

**Done when:** Category drill-in works. Tweak cards show registry info. Font preview renders in actual font. Profile filtering uses saved profiles from settings.

### 3.5 Startup

**ViewModel:** `StartupViewModel`. Dependencies: `IStartupService`.

**Layout:** Single scrollable list with search. Each item: name + toggle + command path + source badge ("Registry"/"User") + delete button.

**Data:**
- `FilteredItems` — startup entries from `IStartupService`
- Search: `SearchText` filters by name
- Source badge shows entry origin

**Interactions:**
- Toggle enable/disable startup item (code-behind: `OnToggleChecked`/`OnToggleUnchecked`)
- Delete button removes entry (`OnRemoveClick`)
- Empty state text when no items

**States:**
- Items loaded from registry + startup folders
- Toggle reflects current enabled state
- Empty state when no startup items match filter

**Note:** This page is not in the original UX spec or architecture doc — added during implementation.

**Done when:** Lists all startup entries. Toggle enable/disable works. Delete removes entry. Search filters correctly. Source badge shows correct origin.

### 3.6 Settings

**ViewModel:** `SettingsViewModel`. Dependencies: `ISettingsProvider`.

**Layout:** ScrollViewer with MaxWidth=600. Three sections: Config Repo, Save, About.

**Data:**
- `ConfigRepoPath` — editable text field
- `StatusMessage` — save feedback
- `AppVersion` — displayed as "vX.Y.Z"

**Interactions:**
- Browse button → folder dialog
- Save button → `SaveCommand`
- `IsSaving` disables save button during operation

**Missing vs Epic 11.3 spec:**
- No "Re-run Wizard" button
- No profile selection display/edit
- No density preference toggle

**Done when:** Config repo path can be changed and saved. Version displays correctly. Missing features from Epic 11.3 are out of scope for this spec (current state only).

### 3.7 Crash Window

**Window:** `CrashWindow.xaml` — 700x500, MinWidth=500, MinHeight=350, Mica backdrop. Standalone window (no ViewModel).

**When shown:** Unhandled exception in dashboard mode (caught by `App.xaml.cs` global handler). In wizard mode, the wizard's inline crash overlay (section 2.8) is used instead.

**Layout:** Error icon (wizard-error.png) + "Something went wrong" heading + exception message + scrollable stack trace (monospace: Cascadia Code/Consolas) + Copy to clipboard button + Close button.

**Data:** Constructor takes `Exception`. Sets `ErrorMessageText.Text = exception.Message` and `ErrorDetailsBox.Text = exception.ToString()`.

**Interactions:** Copy button → `Clipboard.SetText()`. Close button → closes window.

**Done when:** Shows exception message and full stack trace. Copy to clipboard works. Window closes cleanly.

---

## 4. Shared Components

### 4.1 AppCard

**File:** `Views/Controls/AppCard.xaml` + `.cs`

**Purpose:** Card control for displaying an app with status, actions, and metadata.

**Dependency properties:**
- `DisplayLabel`, `Description`, `Category` — text display
- `Status` (CardStatus) — drives StatusRibbon
- `Website`, `GitHub` — external link URLs
- `CanLink`, `CanUnlink`, `CanFix` — action button visibility
- `IsSelected` — selection state

**Events:** `LinkRequested`, `UnlinkRequested`, `FixRequested`, `ConfigureRequested`

**Layout:** StatusRibbon → Icon + Name + Configure gear → Description → Website/GitHub links → Link/Unlink/Fix buttons. Fixed width: 320px.

**Note:** Uses generic `Apps24` SymbolIcon — no real app icons loaded.

**Used in:** Wizard Apps step, Apps page category detail.

### 4.2 StatusRibbon

**File:** `Views/Controls/StatusRibbon.xaml` + `.cs`

**Purpose:** Colored status indicator strip on cards.

**Dependency properties:** `Status` (CardStatus)

**Status → display mapping:**

| Status | Text | Color |
|--------|------|-------|
| Linked | "Linked" | #059669 (green) |
| Detected | "Detected" | #B45309 (amber) |
| Selected | "Selected" | #047857 (teal) |
| Drift | "Drift" | #D97706 (orange) |
| Broken | "Broken" | #DC2626 (red) |
| Error | "Error" | #DC2626 (red) |
| Default | "Not installed" | #2563EB (blue) |

**Used in:** AppCard, wizard dotfile/tweak cards, dashboard page cards.

### 4.3 DriftHeroBanner

**File:** `Views/Controls/DriftHeroBanner.xaml` + `.cs`

**Purpose:** Dashboard hero showing health overview.

**Dependency properties:** `LinkedCount`, `AttentionCount`, `BrokenCount`, `HealthPercent`, `StatusMessage`

**Layout:** Status message + colored count badges (linked green / attention amber / broken red) + health percentage circle.

**Auto-updates:** `StatusMessage` recomputes when counts change.

**Used in:** DashboardPage only.

### 4.4 ProfileCard

**File:** `Views/Controls/ProfileCard.xaml` + `.cs`

**Purpose:** Wizard profile selection card.

**Dependency properties:** `ProfileName`, `Tagline`, `IsSelected`, `HeroImageSource`

**Events:** `SelectionChanged` (routed)

**Layout:** Hero image + gradient overlay + name/tagline + selection badge + dim overlay when not selected. Click toggles `IsSelected`.

**Used in:** Wizard Profile Selection step only.

### 4.5 DeployBar

**File:** `Views/Controls/DeployBar.xaml` + `.cs`

**Purpose:** Bottom bar showing selection count with deploy/clear actions.

**Dependency properties:** `SelectedCount`, `IsDeploying`

**Events:** `DeployRequested`, `ClearRequested`

**Visibility:** Auto-shows when `SelectedCount > 0` (Collapsed/Visible toggle).

**Layout:** Count display + Clear button + Deploy button.

**Used in:** Wizard only (removed from DotfilesPage and SystemTweaksPage — see section 7.3).

### 4.6 TierSectionHeader

**File:** `Views/Controls/TierSectionHeader.xaml` + `.cs`

**Purpose:** Collapsible section divider for tiered card lists.

**Dependency properties:** `Title`, `ItemCount`, `IsCollapsed`

**Interactions:** Click toggles collapse state.

**Used in:** Wizard Apps step, Apps page category detail — separates YourApps / Suggested / Other tiers.

---

## 5. Desktop Services

### 5.1 GalleryDetectionService

**File:** `Services/GalleryDetectionService.cs` (459 lines)

**Purpose:** The three-way join engine. Combines system state (installed packages, existing symlinks) with gallery catalog and user config to produce detection results.

**Dependencies:** `ICatalogService`, `IFontScanner`, `IPlatformDetector`, `ISymlinkProvider`, `ISettingsProvider`, `IEnumerable<IPackageManagerProvider>`

**Key methods:**

| Method | Returns | Used by |
|--------|---------|---------|
| `WarmUpAsync()` | void | App.xaml.cs startup |
| `DetectAppsAsync(profiles)` | `GalleryDetectionResult` (YourApps/Suggested/Other) | Wizard |
| `DetectAllAppsAsync()` | flat app list | Apps page |
| `DetectTweaksAsync(profiles)` | tweak categories | Wizard + SystemTweaks page |
| `DetectDotfilesAsync()` | dotfile groups | Wizard + Dotfiles page |
| `DetectFontsAsync()` | `FontDetectionResult` (Installed/Nerd) | Wizard + SystemTweaks page |

**Detection logic:**
- Apps: package manager scan (Winget/Choco) + file existence → three-tier sorting by profile category mapping
- Dotfiles: symlink status check per link → file-level drift detection
- Tweaks: profile-filtered catalog entries
- Fonts: system font scan + gallery font cross-reference with package managers

**Profile-to-category mapping:** Hardcoded `_profileCategoryMap` dictionary.

**Test coverage:** 36 tests across 4 files (Apps: 12, Dotfiles: 10, Fonts: 8, Tweaks: 6). Tests in `Perch.Core.Tests/Desktop/`.

### 5.2 AppDetailService

**File:** `Services/AppDetailService.cs`

**Purpose:** Load manifest and alternatives when the user opens app config detail.

**Dependencies:** `IModuleDiscoveryService`, `ICatalogService`, `ISettingsProvider`

**Flow:**
1. Load `ConfigRepoPath` from settings
2. Discover modules in config repo
3. Find module matching app by `GalleryId` (search manifest.yaml files)
4. Parse manifest YAML
5. Find alternatives: same-category catalog entries excluding current app

**Returns:** `AppDetail(Card, OwningModule?, Manifest?, ManifestYaml?, ManifestPath?, Alternatives[])`

**Test coverage:** 5 tests — no config repo, no modules, alternatives filtering, self-exclusion.

### 5.3 DotfileDetailService

**File:** `Services/DotfileDetailService.cs`

**Purpose:** Load owning module and manifest for a dotfile group.

**Dependencies:** `IModuleDiscoveryService`, `ICatalogService`, `ISettingsProvider`, `IPlatformDetector`

**Flow:**
1. Load `ConfigRepoPath` from settings
2. Discover modules
3. Find owning module:
   - Primary: match first file's path against module link targets (platform-aware path normalization)
   - Fallback: match by `GalleryId` on module name
4. Load manifest YAML
5. Find alternatives in same category

**Returns:** `DotfileDetail(Group, OwningModule?, Manifest?, ManifestYaml?, ManifestPath?, Alternatives[])`

**Edge cases:** Cross-platform link target matching, forward slash normalization on Windows.

**Test coverage:** 9 tests — path matching, platform targets, normalization, GalleryId fallback, no config repo, empty modules.

---

## 6. Models

### Enums

| Enum | Values | Purpose |
|------|--------|---------|
| `CardStatus` | NotInstalled, Detected, Selected, Linked, Drift, Broken, Error | Universal card status driving StatusRibbon + action buttons |
| `CardTier` | YourApps, Suggested, Other | Three-tier app sorting in detection |
| `UserProfile` | Developer, PowerUser, Gamer, Casual | Wizard profile selection, category filtering |

### Display Models

| Model | Key Properties | Observable Props |
|-------|---------------|-----------------|
| `AppCardModel` | Id, Name, DisplayName, Category, Status, IsSelected, IsExpanded, Tier, CanLink, CanUnlink, CanFix, BroadCategory, SubCategory | Status, IsSelected, IsExpanded |
| `AppCategoryCardModel` | CategoryName, Icon, Items, ItemCount | — |
| `DotfileGroupCardModel` | Id, Name, DisplayLabel, Category, Files (DotfileFileStatus[]), Status, IsSelected | Status, IsSelected |
| `TweakCardModel` | Name, Description, Status, IsSelected, TotalCount | Status, IsSelected |
| `TweakCategoryCardModel` | CategoryName, Icon, Items, ItemCount | — |
| `FontCardModel` | Name, Description, Status, IsSelected, SampleText, FullPath?, IsExpanded | Status, IsSelected, SampleText, IsExpanded |
| `FontFamilyGroupModel` | FamilyName, Fonts[], IsExpanded, IsSelected | IsExpanded, IsSelected |
| `FontCardSource` | (enum or type for font origin) | — |
| `StartupCardModel` | Name, Command, SourceLabel, IsEnabled | — |

### Detail Records (immutable)

| Record | Fields |
|--------|--------|
| `AppDetail` | Card, OwningModule?, Manifest?, ManifestYaml?, ManifestPath?, Alternatives[] |
| `DotfileDetail` | Group, OwningModule?, Manifest?, ManifestYaml?, ManifestPath?, Alternatives[] |
| `DotfileFileStatus` | FileName, Status, SourcePath, TargetPath |

### ViewModel-level Models

| Model | Fields | Used in |
|-------|--------|---------|
| `StatusItemViewModel` | Name, Level, Message | Dashboard attention list |
| `AppCategoryGroup` | SubCategory, Apps[] | Apps page grouped view |

---

## 7. Dead UI Inventory

All dead UI identified below has been removed.

### 7.1 Drag-Drop Zones (Apps + Dotfiles pages) -- REMOVED

Drop zone UI, drag event handlers, and `AddDroppedFiles` stub commands removed from AppsPage and DotfilesPage.

### 7.2 YAML Editor Toggle (Apps + Dotfiles pages) -- REMOVED

Toggle buttons, `ShowRawEditor`/`ShowEditorView` properties, YAML TextBox panels removed. Structured view now bound directly to `HasModule`.

### 7.3 DeployBar on Dashboard Pages (Dotfiles + SystemTweaks) -- REMOVED

DeployBar removed from DotfilesPage and SystemTweaksPage. Associated `SelectedCount`, `UpdateSelectedCount`, `ClearSelection`, and `OnClearRequested` handlers removed.

### 7.4 Alternatives Section (Apps + Dotfiles detail views) -- REMOVED

Alternatives UI sections removed from AppsPage and DotfilesPage detail views. `HasAlternatives` property removed from ViewModels.

---

## 8. Gallery Data Requirements

What each card type needs from the gallery catalog to render correctly.

### App Cards

| Field | Required | Impact if Missing |
|-------|----------|-------------------|
| `name` (GalleryId) | Yes | Card cannot be created |
| `displayName` | Yes | Falls back to `name`, but Alternatives section shows blank |
| `category` | Yes | Cannot group into categories; card appears uncategorized |
| `description` | No | Empty description area |
| `tags` | No | Profile matching less accurate |
| `install.winget` | No* | Cannot detect via Winget |
| `install.choco` | No* | Cannot detect via Chocolatey |
| `install.file` | No* | Cannot detect via file existence |
| `website` | No | Website link hidden |
| `github` | No | GitHub link hidden |

*At least one install method needed for detection to work.

### Dotfile Groups

| Field | Required | Impact if Missing |
|-------|----------|-------------------|
| `name` (GalleryId) | Yes | Group cannot be created |
| `displayName` | Yes | Falls back to `name` |
| `category` | Yes | Cannot group by category |
| `description` | No | Empty description |
| `links` (in module manifest) | Yes | No files to show; group appears empty |

### Tweaks

| Field | Required | Impact if Missing |
|-------|----------|-------------------|
| `name` | Yes | Card cannot be created |
| `displayName` | No | Falls back to `name` |
| `category` | Yes | Cannot assign to tweak category |
| `description` | No | Empty description |
| `registry` entries | No | Registry count shows 0 |
| `profiles` | No | Not filtered by profile (shown to all) |

### Fonts

| Field | Required | Impact if Missing |
|-------|----------|-------------------|
| `name` | Yes | Font cannot be listed |
| `displayName` | No | Falls back to `name` |
| `install.choco` or `install.winget` | No | Cannot detect if installed via package manager |

System fonts are detected independently via `IFontScanner` — no gallery entry needed for installed font display.

---

## 9. Known Issues

### 9.1 Wizard MinHeight > Height -- FIXED

`Height` set to `800` to match `MinHeight="800"`.

### 9.2 Null DisplayName on Catalog Entries -- FIXED

`AppCardModel.DisplayLabel` and `DotfileGroupCardModel.DisplayLabel` already fall back to `Name`. The alternatives section (which bound `DisplayName` directly) was removed in section 7.4.

### 9.3 Silent Error States on Catalog Failure -- FIXED

Apps, Dotfiles, and SystemTweaks pages now show an error banner when detection fails. `ErrorMessage` property on each ViewModel, cleared on retry.

### 9.4 Hardcoded Profiles in SystemTweaksViewModel -- FIXED

`SystemTweaksViewModel` now loads profiles from `PerchSettings.Profiles` (saved by the wizard). Falls back to `{ Developer, PowerUser }` when no profiles are saved yet (pre-wizard state).

### ~~9.5 Gamer/Casual Profile Sparsity~~ REMOVED

Content gap, not a code bug. Removed from known issues.

### 9.6 No Error Recovery in Dashboard -- FIXED

Dashboard already has a refresh button; error handling follows the same pattern as the other pages after the 9.3 fix.

### 9.7 Startup Page Not in Spec

StartupPage was added during implementation but is not referenced in the UX Design Specification or Architecture document. It works correctly but lacks spec backing.

### 9.8 No "Add to Startup" Entry Point

`AddToStartupCommand` concept referenced in brainstorm. StartupViewModel has toggle/remove but no "Add new" UI. No Add button or dialog exists in StartupPage.xaml — users can only manage existing entries. Feature not started.

---

## 10. Test Strategy

### Current Coverage

| Area | Tests | Files | Notes |
|------|-------|-------|-------|
| GalleryDetectionService — Apps | 12 | `GalleryDetectionServiceAppTests.cs` | Winget/Choco/file detection, status transitions, profile matching |
| GalleryDetectionService — Dotfiles | 10 | `GalleryDetectionServiceDotfileTests.cs` | Symlink status, drift detection, file-level status |
| GalleryDetectionService — Fonts | 8 | `GalleryDetectionServiceFontTests.cs` | System font scan, gallery cross-reference |
| GalleryDetectionService — Tweaks | 6 | `GalleryDetectionServiceTweakTests.cs` | Profile filtering, category grouping |
| AppDetailService | 5 | `AppDetailServiceTests.cs` | Module matching, alternatives, edge cases |
| DotfileDetailService | 9 | `DotfileDetailServiceTests.cs` | Path matching, platform targets, normalization |
| ViewModels (Apps, Dotfiles, SystemTweaks) | 20 | `ViewModelTests.cs` | Navigation state, refresh/error, search filtering, profile loading |
| WizardShellViewModel | 46 | `WizardViewModelTests.cs` | Step building, navigation guards, detection flow, deploy, crash, font onboarding |
| **Total Desktop** | **116** | **8 files** | All in `Perch.Core.Tests/Desktop/` |

Tests are gated behind the `#if DESKTOP_TESTS` preprocessor directive and `[Platform("Win")]` + `[SupportedOSPlatform("windows")]` attributes.

### Gaps

| Gap | Priority | Rationale |
|-----|----------|-----------|
| `smoke-test.ps1` integration | Low | Exists at `tests/integration/smoke-test.ps1`, covers deploy + symlink verify. Manual run only |

### Recommendations

1. **Integration test expansion** — `smoke-test.ps1` only covers CLI deploy. Add Desktop smoke test that launches app, verifies wizard appears on first run, completes a profile selection.


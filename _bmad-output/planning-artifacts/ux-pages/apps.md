# Apps Page - UX Specification

**Status:** Draft
**Last updated:** 2026-02-18

---

## 1. Purpose

Browse, discover, and manage application configurations. Perch detects installed apps, matches them against the gallery, and lets users toggle config management on/off via symlinks.

Two contexts:

- **Wizard step:** Select which apps to manage during initial deploy
- **Dashboard page:** Ongoing management with drift detection, link/unlink

---

## 2. Page Layout

Three tiers displayed vertically on a single scrollable page. No drill-in navigation -- everything is flat or expands in place.

```
┌──────────────────────────────────────────────────────────────┐
│  Apps                   [Search name or tag...] [↻]          │
│  12 linked · 2 drifted · 3 detected              (dashboard)│
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ── Your Apps ──────────────────────────────────── 17 apps ──│
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐         │
│  │ VS Code      │ │ Git          │ │ Win Terminal │         │
│  │ ● Linked     │ │ ● Linked     │ │ ● Drifted    │         │
│  │ editor, ide  │ │ vcs, scm     │ │ terminal     │         │
│  │ ⭐ 168k [⏻] │ │ ⭐ 53k  [⏻] │ │ ⭐ 96k  [⏻] │         │
│  └──────────────┘ └──────────────┘ └──────────────┘         │
│                                                              │
│  ── Suggested for You ─────────────────────────── 8 apps ───│
│  ┌──────────────┐ ┌──────────────┐                          │
│  │ Obsidian     │ │ Postman      │                          │
│  │ ● Detected   │ │ ● Not Inst.  │                          │
│  │ notes, md    │ │ api, rest    │                          │
│  │ ⭐ 28k  [⏻] │ │         [⏻] │                          │
│  └──────────────┘ └──────────────┘                          │
│                                                              │
│  ── Browse All ────────────────────────────────── 150 apps ──│
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐       │
│  │ 💻       │ │ 🌐       │ │ 🎮       │ │ 🔧       │       │
│  │ Developm.│ │ Browsers │ │ Gaming   │ │ Utilities│       │
│  │ 45 apps  │ │ 8 apps   │ │ 12 apps  │ │ 22 apps  │       │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘       │
│                                                              │
│  ▼ Development ──────────────────────────────────────────── │
│    IDEs                                                      │
│    ┌──────────────┐ ┌──────────────┐                        │
│    │ Visual Studio│ │ Rider        │                        │
│    │ ...          │ │ ...          │                        │
│    └──────────────┘ └──────────────┘                        │
│    Tools                                                     │
│    ┌──────────────┐ ┌──────────────┐                        │
│    │ ILSpy        │ │ LINQPad      │                        │
│    │ ...          │ │ ...          │                        │
│    └──────────────┘ └──────────────┘                        │
│  ▶ Browsers ─────────────────────────────────────────────── │
│  ▶ Gaming ───────────────────────────────────────────────── │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

### 2.1 Header

| Element | Behavior |
|---------|----------|
| Page title | "Apps" |
| Search box | Filters by name AND tags. Placeholder: "Search name or tag...". When active, all three tiers filter simultaneously. Category cards in Browse All that have 0 matches hide. |
| Refresh button | Re-runs detection. Disabled during load. |
| Status summary (dashboard) | "X linked, Y drifted, Z detected" -- aggregate counts. Hidden in wizard. |

---

## 3. Tier 1: Your Apps

Apps detected on the system (installed via winget/choco/file check) that have status Linked, Drifted, Broken, or Detected.

### Ordering

Cards sorted by status priority, then alphabetically:
1. Drifted / Broken (needs attention -- amber/red)
2. Detected (installed but not managed -- blue)
3. Linked (healthy -- green)

### What appears here

- Apps where `IsDetected = true` (package manager match or config file exists)
- Apps with status Linked, Drifted, Broken, Detected
- **Dependency rule (Option C):** Apps whose `requires` points to another app that is ALSO in this tier are hidden from Tier 1. They appear as nested items when the parent app is expanded. Example: ILSpy requires .NET SDK → ILSpy hidden from Tier 1, shown under .NET SDK's expanded view.
- Parent apps that have dependents show a badge: "5 related apps"

### Empty state

Hidden entirely if no apps detected. Tier 2 becomes the first visible section.

---

## 4. Tier 2: Suggested for You

Apps matching user's selected profiles that are NOT already in Tier 1.

### What appears here

- Apps where `profiles` intersects with user's selected profiles
- Excludes apps already shown in Tier 1 (detected/linked)
- Excludes apps hidden by dependency rule (shown under parent instead)
- Sorted alphabetically

### Empty state

Hidden if no suggestions (no profiles set or all suggestions already detected).

---

## 5. Tier 3: Browse All

All remaining gallery apps organized by category. Displayed as collapsible category cards.

### Category Cards (collapsed)

```
┌─────────────────────────┐
│ 💻                      │
│ Development              │
│ 45 apps                  │
└─────────────────────────┘
```

| Element | Description |
|---------|-------------|
| Icon | Mapped from broad category name (same mapping as current `AppCategoryCardModel.GetIcon`) |
| Category name | Broad category (first segment of `category` path) |
| App count | Total apps in this broad category (excluding those in Tier 1 and 2) |
| Click | Expands inline to show apps grouped by sub-category. Does NOT navigate away. |
| Expanded state | Chevron ▼, apps shown below the category header |

### Category Expanded

When a category card is clicked, it expands in place. Apps within are grouped by sub-category (second+ segment of `category` path):

```
▼ Development ─────────────────────────────────────
  IDEs
  ┌──────────────┐ ┌──────────────┐
  │ Visual Studio│ │ Rider        │
  └──────────────┘ └──────────────┘
  Tools
  ┌──────────────┐ ┌──────────────┐
  │ ILSpy        │ │ LINQPad      │
  └──────────────┘ └──────────────┘
  CLI Tools
  ┌──────────────┐ ┌──────────────┐
  │ dotnet-ef    │ │ dotnet-outd. │
  └──────────────┘ └──────────────┘
▶ Browsers ────────────────────────────────────────
```

Sub-categories with `kind: cli-tool` or `install.dotnet-tool` apps are grouped under a "CLI Tools" sub-heading if no sub-category path exists.

### Dependency Hiding in Browse All

Same Option C rule: if ILSpy `requires: [dotnet-sdk]` and .NET SDK is in the same category, ILSpy is hidden from the sub-category list and shown under .NET SDK's expanded card instead.

---

## 6. App Card Design

Visually similar to tweak cards (same design language) but different DataTemplate.

### 6.1 Collapsed Card

```
┌──────────────────────────────────┐
│ Suggested · Linked         [⏻]  │  Badges + Toggle (top-right)
│                                  │
│ Visual Studio Code               │  Name
│ Lightweight but powerful editor  │  Description (2 lines max)
│                                  │
│ editor · ide · microsoft         │  Tags
│ ⭐ 168k                    ▶    │  GitHub stars (clickable) + expand
└──────────────────────────────────┘
```

| Element | Description |
|---------|-------------|
| Toggle | Top-right. **Wizard:** select for deploy. **Dashboard:** ON = Perch manages this app's config. |
| Status ribbon | Linked (green) / Detected (amber) / Drifted (amber) / Not Installed (blue). Inline with badges. |
| Suggested badge | Shown if app matches user profiles AND is in Tier 2. Green outline. |
| Kind badge | `cli-tool` / `runtime` / `dotfile` shown as subtle pill. `app` is the default, not shown. |
| Name | `display-name` if set, else `name`. `13px SemiBold`. |
| Description | `11px #888`. Max 2 lines. |
| Tags | Tag pills. `10px #666` on `#222240` background. Max ~3 visible, "+N" overflow. Clickable: clicking a tag fills the search box with that tag. |
| GitHub stars | ⭐ count. Clickable → opens GitHub URL. `10px #888`. Hidden if no `links.github`. |
| Website link | Small 🔗 icon next to stars. Opens `links.website`. Hidden if no website. |
| Expand chevron | Bottom-right ▶. Click expands detail inline. |
| Related apps badge | If app has dependents (other apps `requires` this one): "5 related apps" badge. `10px`. |
| Width | 280px fixed. |
| Border | Toggle ON: green #10B981. Default: #2A2A3E. |

### 6.2 Expanded Card - App Detail

Expand reveals config details, dependencies, and app-owned tweaks.

```
┌──────────────────────────────────────┐
│ Linked                         [⏻]  │
│                                      │
│ Visual Studio Code                   │
│ Lightweight but powerful editor      │
│ editor · ide · microsoft             │
│ ⭐ 168k  🔗                         │
│                                      │
│ ┌ Requires ──────────────────────┐   │
│ │ ⚠ .NET SDK                    │   │  (only if requires[] exists)
│ └────────────────────────────────┘   │
│                                      │
│ ┌ Config Links ────────────────────┐ │
│ │ settings.json                    │ │
│ │   → %APPDATA%/Code/User/        │ │
│ │ keybindings.json                 │ │
│ │   → %APPDATA%/Code/User/        │ │
│ └──────────────────────────────────┘ │
│                                      │
│ ┌ Install ─────────────────────────┐ │
│ │ winget: Microsoft.VisualStudio.. │ │
│ │ choco:  vscode                   │ │
│ └──────────────────────────────────┘ │
│                                      │
│ ┌ Extensions ──────────────────────┐ │
│ │ Bundled: ms-dotnettools.csharp   │ │
│ │ Recommended: prettier            │ │
│ └──────────────────────────────────┘ │
│                                      │
│ ┌ App Tweaks ──────────────────────┐ │
│ │ Disable Telemetry          [⏻]  │ │
│ │ 1 registry key              ▶   │ │
│ └──────────────────────────────────┘ │
│                                      │
│ Alternatives: Sublime Text, Notepad++│
│ Also consider: Git, Windows Terminal │
│                                      │
│ MIT · windows, linux, macos          │
│                              ▼       │
└──────────────────────────────────────┘
```

| Section | Content | Visibility |
|---------|---------|------------|
| **Requires** | Shown at the **top** of expanded detail. Lists required dependencies as clickable links. Prominent styling (amber/warning tone) so it's immediately clear. E.g., "Requires: .NET SDK". | Only if `requires[]` exists |
| **Config Links** | Source file → target path (platform-resolved). Each link on its own line. Shows symlink status icon per link (✓ linked, ✗ broken, ○ not linked). | Always (if `config.links` exists) |
| **Install** | Package manager IDs: winget, choco, dotnet-tool, node-package. Copiable text. | Always (if `install` exists) |
| **Extensions** | Bundled + Recommended extension IDs. | Only if `extensions` exists |
| **App Tweaks** | App-owned tweaks as mini tweak cards with toggle + expandable registry detail. Same design as tweak cards but nested. | Only if `tweaks[]` exists |
| **Alternatives** | Clickable links to alternative apps (bidirectional via `alternatives[]`). Click scrolls to / highlights that app. | Only if alternatives exist |
| **Also consider** | Clickable links from `suggests[]`. Soft recommendation. Click scrolls to that app. | Only if suggests exist |
| **Footer** | License badge + OS badges (windows/linux/macos pills) | Always |

### 6.3 Expanded Card - Related Apps (Dependency Tree)

When an app has dependents (other apps that `requires` this one), expanding reveals them grouped by sub-category:

```
┌──────────────────────────────────────┐
│ Linked                         [⏻]  │
│                                      │
│ .NET SDK                             │
│ .NET development platform            │
│ dotnet · runtime · sdk               │
│ ⭐ 14k                              │
│                                      │
│ ┌ Config Links ──────────────...   │ │
│                                      │
│ ┌ Related Apps ────────── 8 apps ──┐ │
│ │                                  │ │
│ │ Editors                          │ │
│ │ ┌────────────┐ ┌────────────┐   │ │
│ │ │ Visual St. │ │ Rider      │   │ │
│ │ │ ● Linked   │ │ ● Detected │   │ │
│ │ │ ide  [⏻]  │ │ ide  [⏻]  │   │ │
│ │ └────────────┘ └────────────┘   │ │
│ │                                  │ │
│ │ Tools                            │ │
│ │ ┌────────────┐ ┌────────────┐   │ │
│ │ │ ILSpy      │ │ LINQPad    │   │ │
│ │ │ ● Not Inst │ │ ● Detected │   │ │
│ │ │ decompiler │ │ query [⏻] │   │ │
│ │ └────────────┘ └────────────┘   │ │
│ │                                  │ │
│ │ CLI Tools                        │ │
│ │ ┌────────────┐ ┌────────────┐   │ │
│ │ │ dotnet-ef  │ │ dotnet-outd│   │ │
│ │ │ ● Linked   │ │ ● Not Inst │   │ │
│ │ │ orm  [⏻]  │ │ nuget [⏻] │   │ │
│ │ └────────────┘ └────────────┘   │ │
│ └──────────────────────────────────┘ │
│                              ▼       │
└──────────────────────────────────────┘
```

| Element | Description |
|---------|-------------|
| "Related Apps" section | Only shown on apps that are `requires` targets of other apps. |
| Sub-group headers | Derived from dependent apps' `SubCategory`. E.g., `Development/IDEs` → "IDEs", `Development/Tools` → "Tools". Apps with `kind: cli-tool` or `install.dotnet-tool` group under "CLI Tools" if they lack a more specific sub-category. |
| Nested app cards | Compact variant: name, status badge, primary tag, toggle. No stars, no description, no expand chevron. Width: 160px. |
| Nested card click | Navigates to a detail screen for that app (full detail: config links, install, extensions, app tweaks, alternatives, suggests). Back button returns to parent card's expanded state. |
| Nested toggle | Same behavior as parent toggle: ON = manage config |

### 6.4 Sub-Group Ordering

Within any grouping (category expanded, related apps), sub-groups are sorted:
1. By gallery `sort` value if defined on the category
2. Alphabetically as fallback

Within sub-groups, apps are sorted:
1. Linked / Drifted / Broken first (status priority)
2. Detected
3. Not Installed
4. Alphabetically within same status

---

## 7. Search & Filtering

### Search Box

- Matches against: `name`, `display-name`, `tags[]`, `description`
- Case-insensitive
- Filters all three tiers simultaneously
- Category cards in Tier 3 hide when 0 apps match within them
- Related/nested apps also searched (parent shows if any child matches)

### Tag Click

- Clicking a tag pill on any card fills the search box with that tag text
- Effectively filters to all apps sharing that tag across all tiers

### Filter Behavior by Tier

| Tier | Search active | Search empty |
|------|--------------|-------------|
| Your Apps | Show only matching detected/linked apps | Show all detected/linked |
| Suggested | Show only matching suggested apps | Show all suggested |
| Browse All | Show matching apps inline (categories auto-expand if they have matches). Non-matching apps hidden. | Show collapsed category cards |

---

## 8. Status Model

Apps use the same terminology as the rest of the app:

| Status | Condition | Color | Ribbon Text |
|--------|-----------|-------|-------------|
| Linked | Config symlinks active and correct | Green #34D399 | "Linked" |
| Drifted | Symlink points to wrong target or files modified | Amber #F59E0B | "Drifted" |
| Broken | Symlink target missing | Red #EF4444 | "Broken" |
| Detected | App installed but config not managed | Blue #3B82F6 | "Detected" |
| Not Installed | App in gallery but not found on system | Muted #888888 | "Not Installed" |
| Error | Link/unlink operation failed | Red #EF4444 | "Error" |

---

## 9. Toggle Behavior

Toggle replaces the previous Link / Unlink / Fix buttons. Single switch, consistent with tweaks.

| Toggle State | Wizard | Dashboard |
|-------------|--------|-----------|
| **ON** | Select for deploy (symlinks created at deploy time) | Perch manages this app's config. If not yet linked: creates symlinks. If already linked: keeps tracking. |
| **OFF** | Deselect from deploy | Perch stops managing. **Does NOT delete symlinks** -- just stops tracking. Status reverts to Detected. |
| **Disabled** | N/A for Not Installed apps | Toggle disabled when app is Not Installed. User must install the app first. |

### Dashboard Toggle Actions

| From Status | Toggle ON | Toggle OFF |
|-------------|-----------|------------|
| Detected | Create symlinks → Linked | N/A (already off) |
| Not Installed | Toggle disabled | Toggle disabled |
| Linked | N/A (already on) | Stop tracking → Detected. Symlinks remain. |
| Drifted | Fix symlinks → Linked | Stop tracking → Detected |
| Broken | Attempt re-link → Linked or Error | Stop tracking → Detected |

### Snackbar Feedback

| Action | Message |
|--------|---------|
| Toggle ON (detected) | "VS Code config linked. 2 files symlinked." |
| Toggle ON (not installed) | N/A -- toggle disabled for Not Installed apps |
| Toggle OFF | "No longer managing VS Code config." |
| Toggle ON fails | "Failed to link VS Code config: [error]" (red InfoBar) |

---

## 10. Wizard vs Dashboard Differences

| Aspect | Wizard | Dashboard |
|--------|--------|-----------|
| Header status | "X selected" | "X linked, Y drifted, Z detected" |
| Toggle meaning | Select for batch deploy | Link/unlink immediately |
| Toggle effect | Deferred (deploy in Review step) | Immediate (symlinks created/tracked on toggle) |
| Tier 1 label | "Detected on Your System" | "Your Apps" |
| Tier 2 label | "Suggested for You" | "Suggested for You" |
| Tier 3 label | "Browse All" | "Browse All" |
| DeployBar | Visible when selections > 0 | Not shown |
| Expanded detail | Read-only (shows what will be linked) | Actionable (shows link status per file) |

---

## 11. States

### Loading

- Indeterminate ProgressRing centered in content area
- Header visible, search functional, refresh disabled
- Progressive: Tier 1 (detection) loads first, Tier 2/3 (gallery) can load after

### Empty States

| Condition | Display |
|-----------|---------|
| No apps detected | Tier 1 hidden. Tier 2 becomes first section. |
| No profiles set | Tier 2 hidden. Only Tier 1 (if any detected) and Tier 3 shown. |
| No gallery data | Tier 2 and 3 hidden. Tier 1 shows detected apps only. InfoBar: "Gallery unavailable. Showing detected apps only." |
| Search no results | All tiers hidden. "No apps matching '[query]'." inline. |

### Error State

- Dark red banner below header
- Error icon + message + dismiss
- Stale data visible below
- Refresh retries

---

## 12. Data Flow

```
Page Load / Refresh
    → LoadProfilesAsync()
    → DetectAllAppsAsync() → AppCardModel[]
    → For each app:
        → IsDetected? (winget/choco/file check)
        → IsLinked? (symlink check)
        → Compute status (Linked/Drifted/Broken/Detected/NotInstalled)
    → Build dependency graph:
        → For each app, find all apps where requires contains this app's ID
        → Those apps become "children" (hidden from top-level, shown nested)
    → Split into tiers:
        → Tier 1: detected/linked/drifted/broken (minus dependency-hidden)
        → Tier 2: profile match, not in Tier 1 (minus dependency-hidden)
        → Tier 3: everything else, grouped by BroadCategory

Toggle ON (dashboard)
    → IAppLinkService.LinkAppAsync(app)
    → Re-detect status
    → Update card StatusRibbon
    → Snackbar confirmation

Toggle OFF (dashboard)
    → Stop tracking (remove from config)
    → Status → Detected (symlinks remain)
    → Snackbar confirmation

Tag Click
    → Set SearchText to tag value
    → All tiers filter
```

---

## 13. Accessibility

| Concern | Implementation |
|---------|---------------|
| Status not color-only | Ribbon includes text label |
| Tags | AutomationProperties: "Tags: editor, ide, microsoft" |
| GitHub stars | AutomationProperties: "168 thousand stars on GitHub, opens GitHub page" |
| Expand/collapse | Announce "Details expanded" / "collapsed" |
| Nested related apps | AutomationProperties on section: "Related apps for .NET SDK, 8 apps" |
| Category expand | Announce: "Development category expanded, 45 apps" |
| Search | AutomationProperties.LiveSetting: announce result count on filter |
| Toggle | "Manage VS Code configuration, currently linked" |

---

## 14. Resolved Decisions

1. **Toggle replaces Link/Unlink/Fix.** Single switch. ON = manage. Consistent with tweaks. Status ribbon communicates the actual state.

2. **Flat tiers, not drill-in.** Tier 1 and 2 are flat card lists on the page. Tier 3 uses expandable category cards (no page navigation).

3. **Dependency grouping: Option C.** Apps that `requires` another app are hidden from the top-level and shown nested when the parent expands. Grouped by sub-category within the parent.

4. **Visually similar cards, different DataTemplates.** App cards and tweak cards share design language (status ribbon, toggle top-right, expandable detail, tags) but are separate XAML DataTemplates because their expanded content differs.

5. **Tags displayed on cards.** Pill-style, max ~3 visible. Clickable to filter by tag.

6. **GitHub stars = GitHub link.** Star count is clickable, opens GitHub URL. No separate GitHub button needed.

7. **Search matches name + tags.** Also matches description and display-name.

8. **Sub-groups in expanded categories** use existing `SubCategory` (from `category` path split). Apps with `kind: cli-tool` or `install.dotnet-tool` get grouped under "CLI Tools" heading.

9. **Toggle disabled for Not Installed apps.** Toggle OFF by default, ON = installed and managed. No "will manage when installed" deferred intent.

10. **Nested card detail on new screen.** Clicking a compact nested card navigates to a detail screen (not inline expand) to avoid visual nesting overload.

11. **`requires` shown prominently at top** of expanded detail. Amber/warning styling. `suggests` shown as soft "Also consider" links at the bottom.

12. **Circular dependencies = both top-level.** Core detects and breaks cycles.

13. **Apps not in gallery = out of scope.** Only gallery-matched apps shown. Users can author their own YAML for unlisted apps.

---

## 15. Resolved Questions

1. **Nested card depth:** Clicking a nested related app (e.g., ILSpy under .NET SDK) navigates to a **detail screen** for that app, rather than expanding inline. Back button returns to the parent card's expanded state. Avoids visual nesting overload.

2. **Circular dependencies:** Both apps treated as top-level. Core should detect circular `requires` and break the cycle by showing both at the top level.

3. **`suggests` vs `requires` display:** `requires` hides the child from top-level (Option C) and shows it nested under the parent. `requires` is also displayed **at the top** of the child's expanded detail with prominent styling. `suggests` does NOT hide -- it only appears as "Also consider" links in the expanded detail.

4. **Apps not in gallery:** Out of scope. Only gallery-matched apps are shown. Users wanting to manage unlisted apps can create their own YAML catalog entries and select the config files to sync.

5. **Toggle for "Not Installed" apps:** Toggle is **disabled** for Not Installed apps. Toggle default is OFF. Toggle ON = app is installed and Perch manages its config. Users must install the app first before toggling on.

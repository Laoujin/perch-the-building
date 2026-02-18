# Windows Tweaks Page - UX Specification

**Status:** Draft v2
**Last updated:** 2026-02-18

---

## 1. Purpose

Central management page for everything Windows-specific that Perch manages beyond app config files. Encompasses multiple distinct management areas unified under one page: registry tweaks, startup programs, fonts, context menus, default apps, file associations, and more.

Used in two contexts:

- **Wizard step:** Select tweaks, fonts, startup items for initial deploy
- **Dashboard page:** Monitor health, detect drift, apply/revert changes

The sidebar menu item for this page is **"Windows Tweaks"** (renamed from "System Tweaks").

---

## 2. Top-Level Categories

The page opens to a category grid. These are distinct management areas, not just groupings of registry tweaks:

| Category | Icon | Description | Content Type |
|----------|------|-------------|--------------|
| **System Tweaks** | Wrench24 | Registry tweaks grouped by area (Explorer, Privacy, etc.) | Registry tweak cards |
| **Startup** | RocketLaunch24 | Programs that run at login | Startup entry cards |
| **Fonts** | TextFont24 | Nerd fonts + installed system fonts | Font cards (special layout) |
| **Context Menus** | ContextMenu24 | Right-click menu entries | Tweak cards (future) |
| **Default Apps** | AppGeneric24 | Default programs for file types | Association cards (future) |
| **File Associations** | DocumentLink24 | File extension → app mappings | Association cards (future) |

Future categories (when gallery content exists):
- **Services** -- Windows services to disable/configure
- **Scheduled Tasks** -- Telemetry tasks, maintenance tasks

Empty categories (no gallery data) are hidden from the grid.

### Category Card

```
┌─────────────────────────┐
│ 🔧                      │
│ System Tweaks            │
│ 24 items                 │
│ 18 adjusted · 2 drifted │  ← dashboard
│ 8 selected              │  ← wizard
└─────────────────────────┘
```

| Element | Description |
|---------|-------------|
| Icon | From table above |
| Category name | Display name |
| Item count | Total items in category |
| Status summary | **Dashboard:** "X adjusted, Y drifted" counts. **Wizard:** "X selected" count. |
| Click | Drills into category-specific detail view |
| Hover | Border highlight (#333350 → #444470) |

---

## 3. Page Structure

```
┌──────────────────────────────────────────────────────────┐
│  Windows Tweaks         [Search...]  [↻]                 │  Header
│  18 adjusted · 2 drifted · 4 system default   (dashboard)│  Status summary
├──────────────────────────────────────────────────────────┤
│                                                          │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐   │  Category grid
│  │ 🔧       │ │ 🚀       │ │ Aa       │ │ 📋       │   │
│  │ System   │ │ Startup  │ │ Fonts    │ │ Context  │   │
│  │ Tweaks   │ │          │ │          │ │ Menus    │   │
│  │ 24 items │ │ 12 items │ │ 42 items │ │ 6 items  │   │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘   │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

### Header

| Element | Behavior |
|---------|----------|
| Page title | "Windows Tweaks" |
| Search box | Filters across all categories (except fonts -- use font search). Replaces category grid with flat filtered list. |
| Refresh button | Re-runs detection. Disabled during load. |
| Status summary (dashboard) | Aggregate counts across all categories. Hidden in wizard. |

### Navigation Flow

```
Category Grid → Category Detail → (Back) → Category Grid
                    ↓
            System Tweaks: sub-category grid → tweak cards
            Startup: startup entry list
            Fonts: dual-panel font layout
            Context Menus: tweak cards (future)
```

---

## 4. Status Terminology

Tweaks are not "installed" -- they are configuration changes. Status labels reflect this:

| Status | Condition | Color | Ribbon Text |
|--------|-----------|-------|-------------|
| Adjusted | All values match desired (tweak is active) | Green #34D399 | "Adjusted" |
| Drifted | Was adjusted, values changed externally | Amber #F59E0B | "Drifted" |
| Partial | Some values match desired, some don't | Amber #F59E0B | "Partial" |
| System Default | Values match Windows defaults (tweak not active) | Blue #3B82F6 | "System Default" |
| Error | Registry unreadable / access denied | Red #EF4444 | "Error" |

### Additional Badges (alongside status ribbon)

| Badge | Condition | Color | Text |
|-------|-----------|-------|------|
| Suggested | Tweak is recommended for user's selected profiles | Accent #10B981 outline | "Suggested" |
| Restart | `restart_required: true` | Muted #888 | 🔄 icon + tooltip |
| Win 10 only | `windows-versions: [10]` | Muted #888 | "Win 10" |
| Win 11 only | `windows-versions: [11]` | Muted #888 | "Win 11" |

---

## 5. System Tweaks (sub-category)

When the user clicks "System Tweaks" from the top-level category grid, they see a second-level category grid grouping tweaks by area.

### 5.1 Sub-Category Grouping

Sub-categories come from the tweak's `category` field in the gallery. Current categories:

| Sub-Category | Icon | Examples |
|--------------|------|----------|
| Explorer | FolderOpen24 | Show file extensions, show hidden files, expand to open folder |
| Privacy | Shield24 | Disable telemetry, advertising ID, activity history |
| Taskbar | AppsList24 | Hide search box, hide task view, disable news |
| Performance | TopSpeed24 | Disable animations, SSD optimizations |
| Input | Keyboard24 | Sticky keys, mouse acceleration |
| Appearance | PaintBrush24 | Dark mode, accent color, transparency |
| Gaming | GameController24 | Game bar, game mode, captures |
| Power | Battery24 | Power plan, sleep settings |

When a sub-category has fewer than 3 items, it is merged into a catch-all "Other" sub-category to avoid sparse groups.

### 5.2 Profile-Based Filtering

Each tweak in the gallery declares `profiles: [developer, power-user, ...]`. This drives a filter bar at the top of the sub-category grid:

```
┌──────────────────────────────────────────────────────────┐
│  ← Back   System Tweaks                                  │
│  [All] [Suggested ✨] [Developer] [Power User] [Gamer]  │  ← Filter chips
├──────────────────────────────────────────────────────────┤
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐   │
│  │ Explorer │ │ Privacy  │ │ Taskbar  │ │ etc.     │   │
```

| Filter | Behavior |
|--------|----------|
| All | Show all sub-categories and tweaks |
| Suggested | Show only tweaks matching user's selected profiles. "Suggested" badge on each card. Sub-categories with 0 matching tweaks are hidden. |
| Developer / Power User / etc. | Filter to tweaks tagged for that specific profile |

**Default filter:** "Suggested" when user has profiles set. "All" when no profiles configured.

### 5.3 Tweak Card

```
┌──────────────────────────────────────┐
│ Suggested · System Default      [⏻] │  Badges + Toggle (top-right)
│                                      │
│ Show File Extensions           🔄   │  Name + restart icon
│ Show file extensions in              │  Description (2 lines max)
│ Windows Explorer                     │
│                                      │
│ [1 registry key ▶]                   │  Expandable button
└──────────────────────────────────────┘
```

| Element | Description |
|---------|-------------|
| Toggle | Top-right corner. **Wizard:** select for deploy. **Dashboard:** track this tweak (ON = Perch monitors for drift). |
| Status ribbon | "Adjusted" / "Drifted" / "System Default" etc. Inline with badges row. |
| Suggested badge | Shown if tweak matches user profiles. Green outline badge. |
| Name | `13px SemiBold`. |
| Restart icon | 🔄 next to name if `restart_required: true`. Tooltip: "Requires restart." |
| Description | `11px #888`. Max 2 lines. |
| Registry key button | Clickable. Shows count: "1 registry key ▶" / "3 registry keys ▶". Click expands to show registry detail inline. |
| Width | 280px fixed. |
| Border | Tracked (toggle ON): green #10B981. Default: #2A2A3E. |

### 5.4 Expanded Registry Detail

Clicking the registry key button expands the card inline:

**Wizard context:**

```
│ ┌ Registry ────────────────────────┐ │
│ │ HKCU\...\Explorer\Advanced      │ │
│ │                                  │ │
│ │ HideFileExt (DWORD)             │ │
│ │   Current:  1                    │ │
│ │   Will set: 0                    │ │
│ └──────────────────────────────────┘ │
```

**Dashboard context:**

```
│ ┌ Registry ────────────────────────┐ │
│ │ HKCU\...\Explorer\Advanced      │ │
│ │                                  │ │
│ │ HideFileExt (DWORD)             │ │
│ │ ┌────────┬────────┬────────┐    │ │
│ │ │Current │Desired │Default │    │ │
│ │ │ 1  ⚠  │ 0      │ 1      │    │ │
│ │ └────────┴────────┴────────┘    │ │
│ └──────────────────────────────────┘ │
│                                      │
│ [Apply]  [Revert]  [📂 Regedit]     │
```

| Element | Description |
|---------|-------------|
| Key path | Abbreviated (last 2-3 segments). Full path in tooltip. `10px Mono #888`. |
| Value name + type | `11px SemiBold`. Type in parentheses. |
| Current value | Amber highlight + ⚠ if ≠ desired. Green ✓ if = desired. |
| Desired value | Accent green text. |
| Default value | Windows default. `#888` muted. |
| Apply | Primary button. Sets desired values. Hidden when status = Adjusted. |
| Revert | Secondary. Restores captured (pre-Perch) values. Only shown after tweak has been applied at least once. |
| Restore Default | Link style. Shown only when desired ≠ default. |
| Open Location | Opens regedit at the key. Icon button labeled "Regedit". Always visible. |

### 5.5 Additional Tweak Metadata

Information available from the gallery that should be surfaced:

| Field | Display | Where |
|-------|---------|-------|
| `reversible` | If false, show warning badge: "Not reversible" | Next to status ribbon |
| `windows-versions` | Badge: "Win 10" / "Win 11" / both (hidden) | Badges row |
| `suggests` | "Related tweaks" links in expanded view | Expanded detail |
| `requires` | "Requires: [tweak name]" in expanded view. Disable Apply if prerequisite not adjusted. | Expanded detail |
| `tags` | Used for search matching, not displayed | Search logic |

---

## 6. Startup Section

When user clicks "Startup" from the top-level category grid. Manages programs that run at Windows login.

### 6.1 Startup Layout

Full-width list layout (not card grid). Cards are wide to show full paths.

```
┌──────────────────────────────────────────────────────────────────┐
│  ← Back   Startup Programs                                      │
│           [Search startup...]              [Track all new ▶]    │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│ ┌────────────────────────────────────────────────────────────┐   │
│ │ [icon] Discord                         Drifted       [⏻]  │   │
│ │ C:\Users\wouter\AppData\Local\Discord\Update.exe --start  │   │
│ │ 📋 Registry (User)                               [🗑️]    │   │
│ └────────────────────────────────────────────────────────────┘   │
│                                                                  │
│ ┌────────────────────────────────────────────────────────────┐   │
│ │ [icon] Spotify                         Adjusted      [⏻]  │   │
│ │ C:\Users\wouter\AppData\Roaming\Spotify\Spotify.exe       │   │
│ │ 📋 Registry (User)                               [🗑️]    │   │
│ └────────────────────────────────────────────────────────────┘   │
│                                                                  │
│ ┌────────────────────────────────────────────────────────────┐   │
│ │ [icon] Microsoft Teams                 System Default [⏻]  │   │
│ │ C:\Program Files\Teams\Teams.exe --background             │   │
│ │ 📋 Registry (Machine)                            [🗑️]    │   │
│ └────────────────────────────────────────────────────────────┘   │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### 6.2 Startup Entry Card

| Element | Description |
|---------|-------------|
| App icon | Extracted from the exe path. Fallback: generic app icon. `24px`. |
| Name | Program name. `13px SemiBold`. |
| Status ribbon | See status rules below. Inline, top-right area. |
| Toggle | Top-right. **Default OFF.** ON = Perch tracks this entry. When tracked, drift detection applies. |
| Command path | Full path, always visible. Rendered as a selectable/copiable input field (read-only TextBox, no border, monospace). Scrolls horizontally if needed. `11px Mono`. |
| Source badge | "Registry (User)" / "Registry (Machine)" / "Startup Folder". Pill badge with clipboard icon for the source key/path. Vertically centered. |
| Delete button | 🗑️ icon, right side. Moves entry to `.backup` (restorable, not permanent delete). Confirmation dialog: "Remove [name] from startup? The entry will be backed up and can be restored." |
| Card width | Full content width minus margins. No fixed width -- stretches. |

### 6.3 Startup Status Rules

| Condition | Status | Color |
|-----------|--------|-------|
| Entry is tracked (toggle ON) and matches config | Adjusted | Green |
| Entry is tracked but was modified externally | Drifted | Amber |
| Entry is NOT tracked (toggle OFF) | System Default | Blue |
| Entry is from an app NOT on user's install list | Drifted | Amber + "Not in install list" sub-label |
| Entry was deleted (.backup exists) | Shows in a "Removed" section, dimmed | Muted |

**Key insight:** If a startup entry belongs to an app that the user hasn't included in their managed apps list, it's flagged as "Drifted" -- something appeared on the system that Perch doesn't expect. This cross-references the Apps page data.

### 6.4 Startup Actions

| Action | Behavior | Confirmation |
|--------|----------|-------------|
| Toggle ON | Start tracking. Perch records this entry in config. Status → Adjusted. | None |
| Toggle OFF | Stop tracking. Perch removes from config. Does NOT delete the entry. Status → System Default. | None |
| Delete | Move registry key/file to `.backup`. Entry moves to "Removed" section. | Dialog: "Remove from startup? Entry will be backed up." |
| Restore (from Removed section) | Restore from `.backup`. Entry reappears in main list. | None |
| Track all new | Bulk action: toggle ON for all currently untracked entries. | None |

---

## 7. Fonts Section

When user clicks "Fonts" from the top-level category grid. Dedicated two-section layout.

### 7.1 Font Section Layout

```
┌──────────────────────────────────────────────────────────────────┐
│  ← Back   Fonts                                                  │
│           [Filter fonts...]                  [Track all ▶]       │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Nerd Fonts                                           12 fonts   │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐            │
│  │ Cascadia     │ │ FiraCode NF  │ │ JetBrains    │            │
│  │ Code NF      │ │              │ │ Mono NF      │            │
│  │              │ │ ● Installed  │ │              │            │
│  │ ● Installed  │ │ ⭐ 2.3k      │ │ ● Not Inst.  │            │
│  │ ⭐ 5.1k  🔗  │ │ 🔗      [⏻] │ │ ⭐ 1.8k  🔗  │            │
│  │         [⏻] │ │              │ │         [⏻] │            │
│  └──────────────┘ └──────────────┘ └──────────────┘            │
│                                                                  │
│  Installed Fonts                              284 fonts          │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ ▶ Arial                              4 variants   [⏻]   │   │
│  ├──────────────────────────────────────────────────────────┤   │
│  │ ▼ Cascadia Code — "code with class"  6 variants   [⏻]   │   │
│  │   ┌──────────────────────────────────────────────────┐   │   │
│  │   │ CascadiaCode-Regular.ttf                    [⏻] │   │   │
│  │   │ Cascadia Code Regular         [Aa] [📂] [👁]    │   │   │
│  │   │ The quick brown fox jumps over the lazy dog      │   │   │
│  │   ├──────────────────────────────────────────────────┤   │   │
│  │   │ CascadiaCode-Bold.ttf                       [⏻] │   │   │
│  │   │ Cascadia Code Bold            [Aa] [📂] [👁]    │   │   │
│  │   └──────────────────────────────────────────────────┘   │   │
│  ├──────────────────────────────────────────────────────────┤   │
│  │ ▶ Consolas                           1 variant    [⏻]   │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### 7.2 Nerd Font Cards (Gallery Fonts)

```
┌──────────────────────────┐
│ Cascadia Code NF    [⏻] │  Name + Toggle
│ Microsoft's Cascadia     │  Description
│ Code with Nerd patches   │
│                          │
│ ● Installed              │  StatusRibbon
│ ⭐ 5.1k           [🔗]  │  GitHub stars + link
└──────────────────────────┘
```

| Element | Description |
|---------|-------------|
| Name | Rendered in the font if installed. Fallback to Segoe UI. `13px SemiBold`. |
| Toggle | Top-right. **Wizard:** install during deploy. **Dashboard:** track (install on new machines). |
| Description | From gallery. `11px #888`. 2 lines max. |
| StatusRibbon | Installed (green) / Not Installed (blue). Bug: detection must match by family name, not just package ID -- fixes the "Fira Code shows Not Installed" issue. |
| GitHub stars | ⭐ count from `catalog/metadata/github-stars.yaml`. `10px #888`. |
| GitHub link | 🔗 icon button. Opens font's GitHub repo. From gallery `links.github`. |
| Width | 260px |

### 7.3 Installed Fonts Section

System fonts grouped by family. Full-width list.

**Section header:**
```
│  Installed Fonts                   [Track all installed ▶]  284 fonts │
```

"Track all installed" button: bulk toggles ON for all installed font families.

**Group Header (collapsed):**

```
│ ▶ Cascadia Code — "ligatures for days"    6 variants   [⏻] │
```

| Element | Description |
|---------|-------------|
| Chevron | ▶/▼ toggle. Click expands. |
| Family name | Rendered in the font. `14px SemiBold`. |
| Quirky specimen | Short whimsical phrase after the family name, rendered in the font. Shows the font's personality. `13px Italic #888`. Separator: em dash. Examples: "ligatures for days", "serious business", "monospace with soul". Auto-generated from a pool or from gallery `preview-text`. |
| Variant count | "X variants". `11px #666`. |
| Toggle | Track this font family (include in config for new machines). |

**Group Expanded - Font Variant:**

```
│   ┌──────────────────────────────────────────────────┐   │
│   │ CascadiaCode-Regular.ttf                    [⏻] │   │  Filename + toggle
│   │ Cascadia Code Regular         [Aa] [📂] [👁]    │   │  Display name + actions
│   │ The quick brown fox jumps over the lazy dog      │   │  Sample text (when Aa toggled)
│   └──────────────────────────────────────────────────┘   │
```

| Element | Description |
|---------|-------------|
| Filename | Actual font filename (e.g., `CascadiaCode-Regular.ttf`). `11px Mono #666`. Solves the "what is this klingon font" problem. |
| Display name | Font's registered display name. Rendered in the font. `13px`. |
| Toggle | Per-variant selection. Top-right of variant card. |
| Try font [Aa] | Toggle sample text area. Icon button, vertically centered with name. |
| Open location [📂] | Opens Explorer at the font's containing folder. Tooltip shows the full path. |
| View font [👁] | Opens in Windows Font Viewer. Only if `FullPath` exists. |
| Sample text | Editable TextBox. Rendered in font. Multi-line, 80-150px. Default: "The quick brown fox jumps over the lazy dog". Visible when [Aa] toggled on. |
| All buttons | Vertically center-aligned with the display name row. |
| Indent | 24px from group header. |

### 7.4 Font Search

- Dedicated search box in the Fonts section header
- Filters both Nerd Fonts and Installed Font groups
- Matches on: family name, variant name, filename, description
- Clear shows all

### 7.5 Font Status

| Status | Condition | Ribbon |
|--------|-----------|--------|
| Installed | Font detected on system | Green "Installed" |
| Not Installed | Gallery font not found on system | Blue "Not Installed" |
| Tracked | Toggle ON, font is in config | No extra ribbon (toggle state is sufficient) |

---

## 8. Wizard vs Dashboard Differences

| Aspect | Wizard | Dashboard |
|--------|--------|-----------|
| Page title | "Windows Tweaks" (same) | "Windows Tweaks" (same) |
| Header status | "X selected" | "X adjusted, Y drifted, Z system default" |
| Toggle meaning | Select for deploy | Track (monitor for drift) |
| Toggle default | OFF (user opts in) | OFF (user opts in) |
| Suggested filter | Default ON (show suggested for profiles) | Available as filter chip |
| Expanded tweak card | "Current" + "Will set" values | Three-value table + action buttons |
| Action buttons | None (deploy batched in Review) | Apply, Revert, Restore Default, Regedit |
| DeployBar | Bottom bar when selections > 0 | Not shown (per-card actions) |
| Startup section | Simplified: toggle to include/exclude entries | Full: status, cross-ref with apps, delete/restore |
| Pre-selection | Tweaks already Adjusted get `IsSelected = true` | N/A |

---

## 9. Action Behavior

### System Tweaks Actions (Dashboard)

| Action | Behavior | Confirmation |
|--------|----------|-------------|
| Apply | Write desired values to registry. Status → Adjusted. Snackbar: "[Tweak name] applied." If `restart_required`: "Restart required for changes to take effect." | None (safe, reversible) |
| Revert | Restore captured (pre-Perch) values. Status → System Default. | Dialog: "Restore your previous values for '[name]'?" |
| Restore Default | Write Windows defaults. Status → System Default. | Dialog: "Restore Windows defaults for '[name]'?" |
| Open Regedit | `Process.Start("regedit", "/m HKCU\...")`. | None |
| Toggle OFF | Stop tracking. Does NOT revert values. | Snackbar: "No longer tracking '[name]'." |

### Startup Actions

| Action | Behavior | Confirmation |
|--------|----------|-------------|
| Toggle ON | Record in config. Track for drift. | None |
| Toggle OFF | Remove from config. Stop tracking. Entry stays in Windows. | None |
| Delete | Move to `.backup`. Entry moves to "Removed" section at bottom. | Dialog: "Remove [name] from startup? Entry will be backed up." |
| Restore | Restore from `.backup`. Entry returns to main list. | None |
| Track all new | Bulk toggle ON for all untracked entries. | None |

### Font Actions

| Action | Behavior | Confirmation |
|--------|----------|-------------|
| Toggle nerd font | Include in config (will install on new machines) | None |
| Toggle font family | Propagate to all variants | None |
| Track all installed | Bulk toggle ON for all installed font families | None |
| Try font [Aa] | Show/hide sample text area | None |
| Open location [📂] | Open Explorer at font folder | None |
| View font [👁] | Open in Windows Font Viewer | None |

---

## 10. States

### Loading
- Indeterminate ProgressRing over content area
- Header visible, search functional, refresh disabled

### Empty States

| Condition | Display |
|-----------|---------|
| No gallery data | "No tweaks available. Check your gallery connection." + Refresh. |
| Category empty | Hidden from grid (never shown empty). |
| Sub-category < 3 items | Merged into "Other" catch-all. |
| Search no results | "No tweaks matching '[query]'." inline. |
| No startup entries | "No startup programs detected." |
| No fonts | Fonts category hidden. |

### Error State
- Dark red banner below header
- Error icon + message + dismiss X
- Stale data visible below
- Refresh retries

### No Config Repo (Dashboard)
- Read-only mode (no toggles, no actions)
- InfoBar: "No config repo configured. Set up in Settings to manage tweaks."

---

## 11. Data Flow

```
Page Load / Refresh
    → LoadProfilesAsync() (from settings, fallback: Developer + PowerUser)
    → Parallel:
        → DetectTweaksAsync(profiles) → TweakCardModel[]
        → DetectFontsAsync() → FontDetectionResult
        → DetectStartupAsync() → StartupCardModel[]   (new)
    → RebuildCategories()
    → BuildFontGroups()

System Tweaks → Sub-category Click
    → SelectSubCategory(name)
    → Filter tweaks by sub-category
    → Apply profile filter if active

Apply (dashboard)
    → ITweakService.ApplyAsync(tweak)
    → Capture current values (if first apply)
    → Write desired values
    → Re-detect → update card

Startup Toggle ON
    → IStartupService.TrackAsync(entry)
    → Record in config
    → Enable drift detection for this entry

Startup Delete
    → IStartupService.BackupAndRemoveAsync(entry)
    → Move to .backup
    → Entry moves to "Removed" list
```

---

## 12. Accessibility

| Concern | Implementation |
|---------|---------------|
| Status not color-only | Ribbon always includes text ("Adjusted", "Drifted", etc.) |
| Keyboard nav | Tab between cards, Enter/Space on toggle, Enter on expand |
| Three-value table | AutomationProperties per cell: "Current value: 0" |
| Category cards | AutomationProperties: "System Tweaks, 24 items, 18 adjusted" |
| Startup paths | Copiable text field, screen reader reads full path |
| Font sample text | Label: "Sample text for [font name]" |
| Startup app icons | AutomationProperties.Name: "[app name] icon" |

---

## 13. Resolved Decisions

1. **Page name:** "Windows Tweaks" (encompasses all Windows-specific management areas).

2. **Status terminology:** "Adjusted" / "System Default" / "Drifted" instead of "Applied" / "Not Installed" / "Drifted". Tweaks aren't installed, they're configuration changes.

3. **Registry key display:** Clickable button showing count ("3 registry keys ▶") that expands inline. Not static text.

4. **Toggle position:** Top-right corner of every card, consistent across all sections.

5. **Toggle default:** OFF everywhere. ON = "Perch tracks this." Consistent semantic across tweaks, startup, and fonts.

6. **Profile suggestions:** Filter chips at top of System Tweaks sub-category view. "Suggested" badge on matching tweak cards.

7. **Startup integrated:** Startup is a category within Windows Tweaks, not a separate sidebar page. Remove the standalone Startup page from the sidebar.

8. **Startup cards:** Wide (full-width), copiable paths, app icons extracted from exe, cross-reference with install list for drift detection.

9. **Startup delete:** Moves to `.backup`, not permanent. Restorable from a "Removed" section.

10. **Font detection fix:** Match installed fonts by family name (not just package ID) to fix false "Not Installed" on fonts like Fira Code.

11. **Font metadata:** Show GitHub stars + link on nerd font cards. Show filename on installed font variants.

12. **Font specimen:** Quirky phrase after family name in group header, rendered in the font.

13. **Font bulk action:** "Track all installed" button in Installed Fonts section header.

---

## 14. Open Questions

1. **App-owned tweaks:** Should app-owned tweaks (e.g., VS Code telemetry) appear in System Tweaks under their category AND under the parent app in the Apps page? Architecture says "Core aggregates into cross-cutting views" -- implies yes, shown in both places with a link to the parent app.

2. **PowerShell script tweaks:** No registry keys to show. Expanded view should show script description + "Runs a PowerShell script" notice + Apply/Revert. No three-value display, no Regedit button.

3. **Batch actions:** Should "Apply all drifted" exist at the sub-category level? Useful when many tweaks drift at once.

4. **Quirky font specimens:** Should these come from gallery `preview-text` field, or auto-generated from a pool? Gallery field is more flexible but requires manual curation. A fallback pool of generic specimens could work.

5. **Context Menus / Default Apps / File Associations:** These are listed as future categories. What gallery schema changes are needed to support them? Context menus might be a subset of registry tweaks with a specific category path. Default apps and file associations are different mechanisms entirely.

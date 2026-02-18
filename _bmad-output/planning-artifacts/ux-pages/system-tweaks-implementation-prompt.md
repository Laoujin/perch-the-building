# Implementation Prompt: Windows Tweaks Page

Paste this into a new Claude Code session in the `dotfiles/Perch` directory.

---

## Prompt

I need you to implement the Windows Tweaks page UX redesign for the Perch Desktop WPF app.

### Context

Perch is a Windows dotfiles manager with a WPF Desktop app (wizard + dashboard). Read these files first:

1. **`CLAUDE.md`** -- project conventions, build/test commands
2. **`_bmad-output/planning-artifacts/ux-pages/system-tweaks.md`** -- the UX spec you're implementing (READ THIS FULLY)
3. **`_bmad-output/planning-artifacts/desktop-spec.md`** -- current implementation state (what exists today)
4. **`_bmad-output/planning-artifacts/ux-design-specification.md`** -- global design system (colors, typography, spacing)

### What exists today

- `src/Perch.Desktop/Views/Pages/SystemTweaksPage.xaml` -- current page with category drill-in
- `src/Perch.Desktop/ViewModels/SystemTweaksViewModel.cs` -- current ViewModel
- `src/Perch.Desktop/Views/Pages/StartupPage.xaml` -- standalone startup page (to be absorbed)
- `src/Perch.Desktop/ViewModels/StartupViewModel.cs` -- startup ViewModel
- `src/Perch.Desktop/Models/TweakCardModel.cs`, `TweakCategoryCardModel.cs`, `FontCardModel.cs`, `FontFamilyGroupModel.cs` -- display models
- `src/Perch.Desktop/Views/Controls/StatusRibbon.xaml` -- status ribbon control
- `src/Perch.Desktop/Views/WizardWindow.xaml` -- wizard with System Tweaks step (~lines 480-780)
- `src/Perch.Desktop/ViewModels/Wizard/WizardShellViewModel.cs` -- wizard ViewModel

### Implementation plan

Work in small commits on master. Each step should build and tests should pass.

**Step 1: Rename and restructure**
- Rename "System Tweaks" to "Windows Tweaks" in sidebar (`MainWindow.xaml`), page title, and all references
- Update `CardStatus` enum: rename `NotInstalled` → add a display mapping for "System Default", `Applied` display → "Adjusted"
- Update `StatusRibbon` display mappings to use new terminology
- The CardStatus enum values can stay as-is in code if needed -- just change the display text in StatusRibbon

**Step 2: Move Startup into Windows Tweaks**
- Make Startup a category within the Windows Tweaks page (clicking "Startup" category card shows the startup list)
- Remove StartupPage from sidebar NavigationView in MainWindow.xaml
- Move startup detection and display logic into SystemTweaksViewModel (renamed or kept)
- Startup cards: full-width, copiable path (read-only monospace TextBox), app icon extraction from exe path
- Delete button moves to .backup (not permanent delete)

**Step 3: System Tweaks becomes a sub-category**
- "System Tweaks" is now one category card in the Windows Tweaks grid
- Clicking it shows a second-level sub-category grid (Explorer, Privacy, Taskbar, etc.)
- Add profile filter chips: [All] [Suggested] [Developer] [Power User] etc.
- Add "Suggested" badge on tweak cards matching user's profiles
- Merge sub-categories with < 3 items into "Other"

**Step 4: Tweak card redesign**
- Move toggle to top-right corner of card
- Replace static "X registry entries" text with clickable expand button: "X registry keys ▶"
- Expand shows registry detail inline (wizard: Current + Will set; dashboard: three-value table)
- Add restart required icon next to name when `restart_required: true`
- Card width: 280px

**Step 5: Dashboard actions (TweakDetailPanel)**
- In expanded dashboard cards: Apply, Revert, Restore Default, Open Regedit buttons
- Apply: write desired values, update status
- Revert: restore captured values (only available after first apply)
- Open Regedit: `Process.Start("regedit", ...)`
- Wire to ITweakService (already exists in Core)

**Step 6: Font improvements**
- Fix detection: match by family name not just package ID (Fira Code bug)
- Add GitHub stars + link on nerd font cards (from `catalog/metadata/github-stars.yaml`)
- Show filename on installed font variants
- Add [📂] open font location button per variant
- Add quirky specimen phrase in group headers (from gallery `preview-text` or fallback pool)
- Add "Track all installed" bulk button
- Fix vertical alignment on action buttons

**Step 7: Startup improvements**
- Toggle default OFF (ON = tracked by Perch)
- Cross-reference with Apps data: if startup entry is from an app not in user's managed list → "Drifted" status
- "Track all new" bulk action button
- "Removed" section at bottom for .backup'd entries with restore action
- Source badge vertical alignment fix

**Step 8: Wizard sync**
- Update WizardWindow.xaml System Tweaks step to match new card design
- Use same card templates (toggle top-right, expandable registry, suggested badges)
- Wizard shows "Current + Will set" in expanded view (not three-value table)

### Design decisions for open questions

If you hit these during implementation, use these defaults:

1. **App-owned tweaks**: Yes, show in both places (Windows Tweaks page under category AND under parent app in Apps page). Add a subtle "Part of [App Name]" link in the tweak card.
2. **PowerShell script tweaks**: Show "Runs a PowerShell script" in expanded view. Apply/Revert buttons only. No three-value table, no Regedit button.
3. **Batch actions**: Skip for now. Per-card actions only.
4. **Font specimens**: Use gallery `preview-text` if available, otherwise pick from a hardcoded pool of ~10 generic specimens.
5. **Future categories** (Context Menus, Default Apps, File Associations): Don't implement. Just ensure the architecture supports adding them later (category list is data-driven, not hardcoded).

### Testing

- Run `dotnet test` after each step -- all 116+ existing tests must pass
- Add ViewModel tests for new behavior (startup integration, profile filtering, status terminology)
- Launch the app (`powershell -ExecutionPolicy Bypass -File launch-dev.ps1`) to verify visually after steps 1, 2, 4, 6
- Tests are in `tests/Perch.Core.Tests/Desktop/` -- follow existing patterns

### Constraints

- Don't modify Perch.Core unless strictly necessary (e.g., new service method needed)
- Don't add new NuGet packages
- Follow existing code patterns (CommunityToolkit.Mvvm, ObservableProperty, RelayCommand)
- Keep XAML consistent with existing style (dark theme colors, spacing from ux-design-specification.md)
- Commit after each step with a descriptive message

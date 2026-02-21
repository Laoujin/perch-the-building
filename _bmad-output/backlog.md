# Perch Backlog

## Auto-skip modules for uninstalled apps

**Priority:** Medium
**Date added:** 2026-02-21

When deploying, modules for apps that aren't installed should be automatically skipped.

**Current behavior:** Ditto registry settings are deployed even if Ditto isn't installed.

**Desired behavior:**
- Check if the app is installed using the gallery's install definition (winget/choco ID)
- If not installed, skip the module with message "Skipped (not installed)"
- This applies to registry entries, config links, etc.

**Implementation notes:**
- Look up gallery entry to get install info (winget ID, choco ID)
- Use existing package manager detection to check if installed
- Skip entire module if app not detected
- Could be opt-in via manifest flag if needed (e.g., `require-installed: true`)

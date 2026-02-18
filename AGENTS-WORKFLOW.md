# Perch Agent Workflow: Issue-to-PR Pipeline

Step-by-step workflow for autonomous Claude sessions working on Perch issues.
Use the `/fix-issue` skill to run this automatically.

---

## Prerequisites

- Working directory: the `Perch/` repo root (or a worktree of it)
- `dotnet` CLI, `gh` CLI, and `git` available
- Perch Desktop builds successfully: `dotnet build src/Perch.Desktop/Perch.Desktop.csproj`

---

## The Loop

Repeat this cycle for each issue:

### 1. Pick an Issue

```bash
gh issue list --state open --json number,title,labels --limit 20
```

Pick the lowest-numbered unblocked issue. Read it fully:

```bash
gh issue view <NUMBER>
```

### 2. Create a Worktree Branch

Always branch from `master`. Use the pattern `issue-<NUMBER>-<short-slug>`:

```bash
git checkout master
git pull origin master
git worktree add ../perch-issue-<NUMBER> -b issue-<NUMBER>-<short-slug>
cd ../perch-issue-<NUMBER>
```

### 3. Implement the Fix

- Read the relevant code before changing it
- Follow `CLAUDE.md` and `~/.claude/AGENTS-DOTNET-STYLE.md` conventions
- Run `dotnet build` after changes -- zero warnings
- Run `dotnet test` -- all tests pass
- Commit small logical units as you go (one concern per commit)

### 4. Smoke Test with Screenshots

If the change touches Perch.Desktop, run smoke tests:

```bash
dotnet test tests/Perch.SmokeTests/Perch.SmokeTests.csproj --filter PageScreenshotTests -v q
```

Screenshots are saved to `tests/Perch.SmokeTests/screenshots/`.

**Review the screenshots yourself** using the Read tool (it displays images). Verify:
- The page renders correctly
- The fix is visually present
- No obvious regressions on other pages

If something looks wrong, fix it and re-screenshot.

**Do NOT commit screenshots.** They are for local review only.

### 5. Show Screenshots to the User

Display the relevant screenshot(s) using the Read tool. Summarize what you see.

### 6. Create the PR

```bash
git push -u origin issue-<NUMBER>-<short-slug>

gh pr create \
  --title "<imperative summary, max 72 chars>" \
  --body "$(cat <<'EOF'
## Summary
- <what changed and why -- 2-4 bullets>
- Closes #<NUMBER>

## Test Plan
- [x] `dotnet build` -- zero warnings
- [x] `dotnet test` -- all pass
- [x] Smoke test screenshots reviewed
EOF
)"
```

### 7. Upload Screenshots to PR

Upload relevant screenshots as release assets and add them as a PR comment:

```bash
# Copy with PR-unique name and upload
cp tests/Perch.SmokeTests/screenshots/03-apps.png /tmp/pr-<NUMBER>-apps.png
gh release upload screenshots /tmp/pr-<NUMBER>-apps.png --clobber

# Add PR comment with embedded images
BASE="https://github.com/Laoujin/Perch/releases/download/screenshots"
gh pr comment <PR_NUMBER> --body "$(cat <<EOF
## Screenshots
![apps]($BASE/pr-<NUMBER>-apps.png)
EOF
)"
```

Only include pages affected by the change.

### 8. Open in Browser

```bash
start <PR_URL>
```

Report the PR URL and what was changed.

### 9. Clean Up and Repeat

After the PR is approved/merged:

```bash
cd ../Perch
git checkout master
git pull origin master
git worktree remove ../perch-issue-<NUMBER>
git branch -d issue-<NUMBER>-<short-slug>
```

Optionally clean up screenshot assets: `gh release delete-asset screenshots pr-<NUMBER>-apps.png --yes`

Then go back to step 1.

---

## Writing Smoke Tests for Specific Issues

When an issue involves UI changes, add a targeted test in `tests/Perch.SmokeTests/`:

```csharp
[TestFixture]
[Platform("Win")]
[NonParallelizable]
public sealed class Issue42_DescriptiveNameTests
{
    private PerchApp _perch = null!;

    [OneTimeSetUp]
    public void Launch()
    {
        _perch = new PerchApp();
        _perch.Launch();
    }

    [OneTimeTearDown]
    public void TearDown() => _perch?.Dispose();

    [Test]
    public void NavigateToApps_DetectedBadge_ShowsCorrectCount()
    {
        _perch.NavigateTo("Apps");
        Thread.Sleep(2000);
        var badge = _perch.MainWindow.FindFirstByXPath("...");
        Assert.That(badge, Is.Not.Null);
        _perch.ScreenshotWindow("issue-42-apps-badge");
    }
}
```

Use the `PerchApp` helper:
- `NavigateTo("PageName")` -- clicks nav items
- `ScreenshotWindow("name")` -- captures the app window
- `MainWindow` -- FlaUI Window object for finding elements
- `Automation` -- UIA3 automation instance for advanced queries

---

## Commit Message Format

```
<imperative verb> <what changed>

<optional body explaining why, if not obvious from the diff>
```

Examples:
- `Fix pending badge double-increment on app toggle`
- `Add detected count badge to collapsed app categories`

No `Co-Authored-By` trailers. No tool attribution.

---

## Labels to Use

| Label | When |
|-------|------|
| `bug` | Something broken |
| `enhancement` | New feature / improvement |
| `area:apps` | Apps page changes |
| `area:home` | Dashboard changes |
| `area:dotfiles` | Dotfiles page changes |
| `area:wizard` | Setup wizard changes |
| `area:detected` | Detected page changes |
| `area:gallery` | Catalog/gallery changes |
| `area:settings` | Settings page changes |
| `area:cli` | CLI command changes |
| `area:deploy` | Deploy functionality |

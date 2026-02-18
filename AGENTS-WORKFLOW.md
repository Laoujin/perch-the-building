# Perch Agent Workflow: Issue-to-PR Pipeline

Step-by-step workflow for autonomous Claude sessions working on Perch issues.

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
gh issue list --state open --label "bug" --json number,title
gh issue list --state open --label "enhancement" --json number,title
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

If a worktree slot (like `perch-2`) is available and empty, reuse it:

```bash
git worktree remove ../perch-2 --force  # only if stale
git worktree add ../perch-2 -b issue-<NUMBER>-<short-slug>
cd ../perch-2
```

### 3. Implement the Fix

- Read the relevant code before changing it
- Follow `CLAUDE.md` and `~/.claude/AGENTS-DOTNET-STYLE.md` conventions
- Run `dotnet build` after changes -- zero warnings
- Run `dotnet test` -- all tests pass
- Commit small logical units as you go (one concern per commit)

### 4. Smoke Test with Screenshots

After the implementation is complete, take screenshots to verify the UI:

```bash
# Run the full page screenshot suite
dotnet test tests/Perch.SmokeTests --filter PageScreenshotTests

# Or run a targeted smoke test if you wrote one for this issue
dotnet test tests/Perch.SmokeTests --filter <TestName>
```

Screenshots are saved to `tests/Perch.SmokeTests/screenshots/`.

**Review the screenshots yourself** using the Read tool (it can display images). Verify:
- The page renders correctly
- The fix is visually present
- No obvious regressions on other pages

If something looks wrong, fix it and re-screenshot.

### 5. Embed Screenshots in the Branch

To make screenshots visible in the PR on GitHub:

```bash
# Force-add screenshots to the branch (they're .gitignored)
git add -f tests/Perch.SmokeTests/screenshots/*.png
git commit -m "Add smoke test screenshots for PR"
```

Build the image URLs using the branch name:
```
https://raw.githubusercontent.com/Laoujin/Perch/<branch>/tests/Perch.SmokeTests/screenshots/<name>.png
```

### 6. Create the PR

```bash
# Push the branch
git push -u origin issue-<NUMBER>-<short-slug>
```

Create the PR with embedded screenshot images. Use `![alt](url)` markdown with raw.githubusercontent.com URLs so they render inline:

```bash
BRANCH="issue-<NUMBER>-<short-slug>"
BASE="https://raw.githubusercontent.com/Laoujin/Perch/$BRANCH/tests/Perch.SmokeTests/screenshots"

gh pr create \
  --title "<imperative summary, max 72 chars>" \
  --body "$(cat <<EOF
## Summary
- <what changed and why>
- Closes #<NUMBER>

## Screenshots
![relevant-page]($BASE/<screenshot-name>.png)

## Test Plan
- [x] Smoke test screenshots reviewed
- [x] \`dotnet build\` -- zero warnings
- [x] \`dotnet test\` -- all pass
EOF
)"
```

### 7. Show Results to the User

After creating the PR:
1. Show the PR URL
2. Display the relevant screenshots using the Read tool
3. Summarize what was changed
4. Open the PR in the browser: `start <PR_URL>` (Windows)

### 8. Clean Up and Repeat

After the PR is approved/merged:

```bash
cd ../Perch
git checkout master
git pull origin master
git worktree remove ../perch-issue-<NUMBER>
git branch -d issue-<NUMBER>-<short-slug>
```

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
        // Interact with elements via FlaUI
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

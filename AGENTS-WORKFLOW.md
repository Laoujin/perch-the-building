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

### 5. Create the PR

```bash
# Push the branch
git push -u origin issue-<NUMBER>-<short-slug>

# Create PR with screenshot
gh pr create \
  --title "<imperative summary, max 72 chars>" \
  --body "$(cat <<'EOF'
## Summary
- <what changed and why>
- Closes #<NUMBER>

## Screenshots
<paste screenshot descriptions -- attach PNGs via gh pr edit or reference paths>

## Test Plan
- [ ] Smoke test screenshots reviewed
- [ ] `dotnet build` -- zero warnings
- [ ] `dotnet test` -- all pass
EOF
)"
```

To attach screenshots to the PR, upload them:

```bash
# Upload screenshot and get URL for PR body
gh pr edit <PR_NUMBER> --add-label "<area:label>"
```

Note: GitHub CLI doesn't support direct image upload in PR bodies. Instead:
1. Reference the screenshot path in the PR description
2. Show the screenshots to the user via the Read tool for approval
3. The user can view them locally at `tests/Perch.SmokeTests/screenshots/`

### 6. Show Results to the User

After creating the PR:
1. Show the PR URL
2. Display the relevant screenshots using the Read tool
3. Summarize what was changed
4. Wait for approval before moving on

### 7. Clean Up and Repeat

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

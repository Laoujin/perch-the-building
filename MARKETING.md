# Perch — Marketing Copy & Strategy

## Tagline

> Make yourself at home with Perch.

## One-liner

> Windows-first dotfiles manager. App settings, packages, registry — managed in git, symlinked into place.

---

## Short Pitch (Reddit / HN / social — 3-4 sentences)

### Option A — pain-first

Every dotfiles tool assumes you live in bash. On Windows, you're an afterthought — no registry, no app settings, no GUI. Perch is built for Windows from day one: it symlinks your configs from a git repo into place, so any setting change is already in git. Desktop app for getting started, CLI for when you know what you're doing.

### Option B — benefit-first

Perch manages your Windows dotfiles, app settings, registry tweaks, and packages — all from a git repo, all deployed via symlinks. Change a setting in VS Code or Windows Terminal, and `git diff` picks it up immediately. New machine? Clone, deploy, done. A desktop app walks you through onboarding; a CLI handles the rest.

### Option C — contrast

chezmoi, Stow, yadm — great tools, all built for Unix. Perch is the dotfiles manager Windows power users have been waiting for. Symlink-first. App-aware. Registry-capable. Desktop wizard for onboarding, CLI for daily driving. One repo, every machine, your settings everywhere.

---

## Medium Pitch (blog intro / website hero — 2-3 paragraphs)

### Option A — storytelling

You just set up a new Windows PC. You've got your apps installed, but every single one opens with factory defaults. Your editor keybindings are gone. Your terminal theme is gone. Your PowerShell profile, your carefully tweaked app settings — gone. You're starting from scratch. Again.

Perch fixes this. It manages your dotfiles, application settings, registry tweaks, fonts, and packages from a single git repository. Your config files are symlinked into place — when you change a keybinding in VS Code, that change is immediately visible in git. Commit, push, and your other machines are one `git pull` away from catching up. A desktop app with a guided wizard walks you through setup; when you're ready, the CLI does exactly what you'd expect.

Most dotfiles tools are built for Linux and treat Windows as a second-class citizen. Perch is the opposite. It's built for Windows first — with native support for registry management, Windows-specific paths, and a desktop app — while also working on Linux and macOS for configs that cross platforms.

### Option B — direct / technical

Perch is a dotfiles and application settings manager built in .NET. It symlinks config files from a git-tracked repository into their expected locations on the filesystem. Change a setting in any managed app, and it's immediately in git — no manual re-sync needed.

What makes it different: Perch thinks in applications, not files. It discovers modules by convention (folder name = app name), manages registry tweaks declaratively, handles package lists across Scoop/Chocolatey/winget, and provides drift detection so you know when something changed behind your back. The CLI handles deployment, status checks, and config diffs. The desktop app provides a visual onboarding wizard and a dashboard for ongoing management.

Windows-first. Symlink-first. App-aware. One repo for every machine.

---

## Website Copy Ideas (Astro)

### Hero Section

**Headline:** Make yourself at home with Perch

**Subhead:** The dotfiles manager built for Windows. App settings, registry tweaks, packages, fonts — managed in git, symlinked into place.

**CTA:** Get Started | View on GitHub

### "Why Perch" Feature Grid (4 cards)

**Symlink-first, zero friction**
Change a setting in any app — it's already in git. No re-add, no re-run, no sync command. Symlinks mean your repo is always up to date.

**Windows-native, not an afterthought**
Registry tweaks, Windows-specific paths, a WPF desktop app. Perch is built for Windows power users, with cross-platform support for configs that travel.

**Desktop app for getting started, CLI for everything else**
A guided wizard detects your installed apps and walks you through setup. Once you're running, the CLI handles deployment, status, and diffs.

**App-aware, not file-aware**
Perch thinks in applications. Folder name = app name. Auto-discovery, drift detection, package management. Your dotfiles repo becomes your system's source of truth.

### "How it works" (3 steps)

1. **Organize** — Create a folder per app in your config repo. Drop in the settings files and a small manifest.
2. **Deploy** — Run `perch deploy`. Perch symlinks every config into its expected location. Existing files are backed up.
3. **Live** — Change settings in your apps normally. Symlinks mean every change is instantly in git. Commit when ready.

### Comparison blurb (for a comparison page)

> Most dotfiles tools are built for Unix terminals and treat Windows as a bolt-on. Perch flips this: Windows is the primary platform, with registry management, Windows-specific paths, and a native desktop app. Cross-platform support handles the configs that need to travel.

---

## WPF Desktop App Copy

### Welcome / First Run

**Heading:** Welcome to Perch
**Subtext:** Let's set up your configs. We'll detect what's installed, show you what can be managed, and symlink everything into place.

### Profile Selection

**Heading:** What kind of user are you?
**Subtext:** This helps us surface the right apps and settings. Pick as many as apply.

### Deploy Complete

**Heading:** You're all set.
**Subtext:** {count} configs linked. Changes you make in your apps will show up in git automatically — no sync step needed.

### Dashboard — All Healthy

**Status:** All configs linked. Everything looks good.

### Dashboard — Drift Detected

**Status:** {count} config(s) changed outside of your repo. Review and re-link, or commit the changes.

---

## Reddit Post Drafts

### r/dotfiles

**Title:** Show-off: Perch — a Windows-first dotfiles manager with a desktop app

I've been working on a dotfiles manager that takes Windows seriously. Most tools in this space are built for Unix and treat Windows as an afterthought. Perch is the opposite.

**What it does:**
- Symlinks your config files from a git repo to their target locations
- Manages app settings, registry tweaks, fonts, and packages
- Change a setting in any app → it's already in `git diff`
- Desktop app with a wizard for first-time setup
- CLI for power users

**What makes it different from chezmoi/Stow/yadm:**
- Windows-first: native registry management, Windows paths, WPF desktop app
- Symlink-first: no copy-on-apply, no re-add step — settings changes flow to git instantly
- App-aware: thinks in applications, not individual files
- Cross-platform: same config repo works on Linux/macOS too

Built in .NET / C#. Open source.

[screenshot/gif] | [GitHub link]

### r/dotnet / r/csharp

**Title:** Built a dotfiles & system config manager in .NET 10 — open source

Most dotfiles tools are written in Go/Rust/Python and built for Unix. I built one in C# that's designed for Windows power users.

**Perch** manages your app settings, registry tweaks, packages, and fonts — all from a git repo, deployed via symlinks. It has a CLI (Spectre.Console) and a WPF desktop app (WPF UI / Fluent 2 design) for visual onboarding and drift monitoring.

Tech stack: .NET 10, NUnit, NSubstitute, Spectre.Console, WPF UI, CommunityToolkit.Mvvm, GitHub Actions CI.

It's an unusual problem space for .NET — most similar tools are CLI-only and Unix-first. The .NET ecosystem made the WPF desktop app natural to include.

[GitHub link]

---

## Blog Post Outline (Dev.to / Medium / itenium)

**Title ideas:**
- "Why I built a dotfiles manager for Windows (and what I learned)"
- "Perch: dotfiles management that doesn't assume you use bash"
- "Your Windows config deserves version control too"

**Structure:**
1. The problem — new machine, factory defaults, manual setup pain
2. Why existing tools don't cut it for Windows users
3. What Perch does differently (symlink-first, app-aware, desktop onboarding)
4. Quick demo (screenshots / GIF of deploy + git diff)
5. Architecture choices (.NET, WPF, why not Electron)
6. What's next (gallery, Scoop integration, community)
7. Link to repo

**Cross-post to:** Dev.to, Medium, itenium blog, LinkedIn article

---

## Distribution Channels

| Channel | Content | Timing |
|---------|---------|--------|
| GitHub README | Hero section + quick start | Before any promotion |
| r/dotfiles | Show-off post with screenshots | Launch day |
| r/dotnet, r/csharp | .NET angle post | Launch day or day after |
| Hacker News | "Show HN" with direct benefit pitch | Launch week |
| Dev.to | Full blog post (cross-posted) | Launch week |
| Medium | Same blog post | Cross-post same day |
| itenium socials | Short pitch + link | Launch week |
| Twitter/X | Thread: problem → solution → screenshots | Launch day |
| LinkedIn | Professional angle article | Launch week |
| dotfiles.github.io | Submit to utilities list | After initial traction |
| awesome-dotfiles | Submit PR | After initial traction |

---

## Demo Assets Needed

- [ ] Terminal recording of `perch deploy` (asciinema or vhs)
- [ ] Screenshot of Desktop wizard (profile selection)
- [ ] Screenshot of Desktop dashboard (healthy state)
- [ ] Screenshot of Desktop dashboard (drift detected)
- [ ] GIF: change setting in app → `git diff` shows it immediately
- [ ] Before/after: factory defaults vs after `perch deploy`

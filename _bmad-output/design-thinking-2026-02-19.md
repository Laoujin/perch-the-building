# Design Thinking Session: Perch Desktop — Onboarding Apps, Dotfiles & Languages

**Date:** 2026-02-19
**Facilitator:** VANSCHWO
**Design Challenge:** How do we design the onboarding experience in the Perch WPF Desktop app so that both casual users/gamers and developers/power users can effortlessly bring their installed apps, dotfiles, and language toolchain ecosystems under Perch management — going from "new laptop with stuff on it" to "fully configured, git-tracked, deploy-ready" in a single session?

---

## Design Challenge

**The Scenario:** A user has a new Windows laptop. Apps are already installed, dotfiles exist in various locations, and language toolchains (.NET, Node, Python) have their own runtimes, global tools, and config files scattered across the system. The user opens Perch Desktop, points it at their perch-config repo, and expects the app to *detect* what's already there, let them *curate* what to manage, and *deploy* the result — making this machine match their preferences.

**Three Onboarding Pillars:**

1. **Apps** — Detect installed applications (via winget/choco), cross-reference against the gallery catalog, present them in tiered groups (Your Apps / Suggested / Other), and let users toggle what Perch should manage. Each app may carry its own tweaks (context menu cleanup, startup entries, file associations).

2. **Dotfiles** — Detect existing config files in known locations, show their link status (Linked / Broken / Not Linked / Modified), and let users bring unmanaged dotfiles under symlink-based git tracking. Dotfiles are the heart of Perch's "change a setting, it's immediately in git" promise.

3. **Languages (New Concept)** — Promote language ecosystems (.NET, Node, Java, C++, Python, Ruby) beyond "just more apps" into first-class organizational units. Each ecosystem bundles: runtimes/SDKs, version managers, editors/IDEs, global CLI tools (installed via the language's own package manager), and language-specific dotfiles (`.npmrc`, `nuget.config`, `global.json`, `bunfig.toml`). This is a new concept that needs UX design from scratch.

**The Dual Audience Challenge:**
- **Casual users / gamers:** Want simplicity. "Just make my new PC feel like my old one." Don't know what dotfiles or symlinks are. Care about apps and game settings.
- **Devs / power users:** Want control. Know exactly what `.gitconfig` is, want to manage their entire .NET toolchain, and will be the early adopters who discover edge cases.

**Constraints:**
- Solo developer, side project
- Windows 10+ WPF app (Fluent 2 design, dark theme, forest green accent)
- Existing 7-step wizard already built (Profile → Config Repo → Dotfiles → Apps → System Tweaks → Review → Deploy)
- Detect-first philosophy: always show what exists before offering to change anything
- Must feel like a polished product, not a dev tool with a window on it

---

## EMPATHIZE: Understanding Users

### User Insights

**Primary Persona: Developer / Power User (early adopter, primary audience)**

- **App installation is the first thing that happens on a new machine** — before any config, before any tweaks. The gallery serves two roles: discovery ("what do I need?") and memory ("what did I have before?"). Perch should handle both.
- **System tweaks happen within the first hour** — mouse sensitivity, pointer visibility, power/lid settings, Explorer show-extensions. These are reflexive, pre-conscious actions. Users don't think "I'll configure my system now" — they hit friction and fix it immediately.
- **Dotfiles that ARE managed (Git, Node, PowerShell) feel good** — the symlink model works. The "change it and it's in git" promise delivers real value once set up.
- **Dotfiles that WERE managed but drifted are a pain point** — Node packages, PowerShell modules, git-lfs. The maintenance burden exceeded the benefit, so they diverged. This creates a category of "things I know I should manage but don't." Guilt + friction = avoidance.
- **New dotfiles emerge and need fast onboarding** — `.claude` is a current example. The tooling landscape shifts; Perch must make it trivial to say "this new thing? Manage it too."
- **Language toolchain setup is a compound task** — installing .NET means: SDK versions 6-10, Visual Studio 2022 & 2026, ReSharper, ILSpy, and a set of global dotnet tools (`ef`, `format`, `outdated`, etc.). These are interrelated. Installing them individually misses the point — they're an ecosystem.
- **Switching tools within an ecosystem is a real scenario** — moving from node/npm to bun should be a Perch operation, not a manual adventure. The "alternatives" relationship model directly serves this.
- **Variable/dynamic paths are a real blocker** — VS 2026's settings path includes a hash, breaking static symlink targets. If Perch can't handle this, users fall back to manual management, which defeats the purpose.
- **No competing tools exist on Windows** — users discovering Perch are learning an entirely new mental model. The onboarding UX is doing double duty: teaching *what Perch is* while also *doing the thing*.

**Secondary Persona: Casual User / Gamer (future audience, deprioritized)**

- Language ecosystems and dotfiles are irrelevant to this persona — they don't know what `.gitconfig` is and don't want to.
- Their concerns are: apps (install my games, Discord, OBS, browser), system tweaks (mouse, display, power), and maybe fonts.
- The wizard should feel like a "new PC setup assistant" for them, not a "dotfiles manager."
- Decision: **focus on dev/power user first.** The casual/gamer experience is a simplification of the dev experience (fewer steps, fewer categories), not a fundamentally different product.

### Key Observations

1. **Onboarding is not just first-time setup — it's re-onboarding.** The most emotionally loaded scenario isn't "I've never managed this." It's "I used to manage this, it drifted, and now I need to get it back under control." Perch must make re-onboarding frictionless, not just initial onboarding.

2. **Three distinct onboarding motions exist:**
   - **Apps:** detect what's installed → match against gallery → toggle what to manage → install missing ones → symlink configs
   - **Dotfiles:** detect existing config files → show link status → bring under management (symlink + git)
   - **Languages:** select ecosystem → install runtime/SDK → install tooling → install global packages → symlink language-specific dotfiles — this is a *compound workflow*, not a single toggle

3. **The "Languages" concept is really "Ecosystem Onboarding"** — it bundles apps + tools + dotfiles + package manager operations into a coherent unit. It's not a fourth pillar alongside Apps/Dotfiles/Tweaks — it's a *higher-level orchestration* that composes the other three.

4. **Profile gating works for complexity management** — Developer/PowerUser profiles surface Languages and Dotfiles. Casual/Gamer profiles hide them entirely. This isn't dumbing down — it's respecting that these concepts are genuinely irrelevant to non-dev users.

5. **System tweaks are the universal onboarding moment** — mouse, power, explorer settings matter to EVERY persona. They're also the fastest wins (immediate gratification). This suggests tweaks should be prominent early in onboarding regardless of profile.

6. **The gallery is doing two jobs:** catalog (what exists in the world) and memory (what I had on my last machine). For first-time users it's a catalog. For returning users it's a checklist. The UI needs to serve both modes.

7. **No prior art means every UX choice is a teaching moment.** There's no "oh this is like Homebrew but for Windows" shortcut. The onboarding must explain the mental model (symlinks, git-tracking, detect-then-configure) without feeling like a tutorial.

### Empathy Map Summary

#### Developer / Power User

| Dimension | Insights |
|-----------|----------|
| **SAYS** | "I want everything managed in git." / "I switched to bun, I should've done that through Perch." / "My VS settings aren't symlinked because the path has a hash in it." / "Node packages drifted, it was too much hassle." |
| **THINKS** | "I know the RIGHT way to do this but the friction stops me." / "If I don't set this up now, I never will." / "I wish I could just declare my entire toolchain and have it appear." / "This new tool (.claude) is changing fast, I need to capture its config before it gets complicated." |
| **DOES** | Installs apps first. Tweaks mouse/power/explorer within minutes. Manually recreates configs they know by heart. Postpones managing things that are "too much hassle." Lets dotfiles drift rather than fight the tooling. |
| **FEELS** | **Frustrated** by tools that were supposed to be managed but drifted. **Guilty** about configs they know they should track but don't. **Excited** about the possibility of declaring an entire ecosystem and having it materialize. **Anxious** about edge cases (variable paths, hash-based directories) that might break the magic. |

#### Casual User / Gamer (secondary, future)

| Dimension | Insights |
|-----------|----------|
| **SAYS** | "Just make my new PC like my old one." / "I don't know what a dotfile is." / "Why is my mouse sensitivity wrong again?" |
| **THINKS** | "Setting up a new PC shouldn't take a whole day." / "I just want my apps and my settings." / "I don't want to learn a system, I want results." |
| **DOES** | Installs apps from memory (forgets some). Manually tweaks settings one by one as they notice problems. Never touches a terminal. Asks a tech-savvy friend for help. |
| **FEELS** | **Overwhelmed** by too many options. **Relieved** when something "just works." **Suspicious** of tools that seem too technical. **Satisfied** when their PC feels familiar. |

---

## DEFINE: Frame the Problem

### Point of View Statement

**Primary POV — Cross-Machine Sync:**
A developer with multiple Windows machines needs to declare their desired machine state once and have it sync everywhere — with the ability to intentionally diverge per machine — because manually replicating changes (like switching from npm to bun) across machines is tedious, error-prone, and something they inevitably forget, causing machines to silently drift apart.

**Apps POV:**
A developer setting up a new machine (or syncing an existing one) needs Perch to detect what's already installed, show what's expected but missing, and let them reconcile the difference — because remembering every app they use and manually installing them is unreliable, and keeping app configs symlinked is the only way to guarantee settings stay in git.

**Dotfiles POV:**
A power user managing config files across machines needs friction-free onboarding of new dotfiles (like `.claude`) and re-onboarding of drifted ones (like PowerShell modules) — because the maintenance burden of keeping configs in sync currently exceeds the perceived benefit, causing them to abandon management of things they know they should track.

**Languages POV:**
A developer working across multiple language ecosystems (.NET, Node/Bun, Python, Ruby) needs to declare their entire toolchain as a unit — runtimes, editors, tools, global packages, and language-specific dotfiles — because these are interrelated and installing them as individual disconnected apps loses the coherence of the ecosystem and the relationships between components (like alternatives: npm vs bun, VS vs Rider).

**Machine Overrides POV:**
A developer who uses bun everywhere except on the projectX machine (which requires Yarn) needs controlled, per-machine drift — because a one-size-fits-all config that ignores machine-specific requirements is worse than no config at all.

### How Might We Questions

**Core:**
1. How might we make "git pull = my machine is updated" feel as natural and reliable as it sounds?
2. How might we let users declare intentional per-machine differences without it feeling like fighting the system?
3. How might we make drift visible and actionable — not anxiety-inducing?

**Apps:**
4. How might we make onboarding a new app into Perch as simple as "it's installed, toggle it on"?
5. How might we help users rediscover apps they had on a previous machine but forgot about?
6. How might we handle app configs that live in non-standard or dynamic paths (like VS 2026's hash-based directory)?

**Dotfiles:**
7. How might we make adding a new dotfile (like `.claude`) to Perch management take less than 30 seconds?
8. How might we re-onboard drifted dotfiles (Node packages, PS modules, git extensions) without requiring the user to remember what the "correct" state was?
9. How might we show dotfile link status in a way that's meaningful to someone who doesn't know what symlinks are?

**Languages:**
10. How might we present a language ecosystem as a coherent unit rather than a flat list of unrelated installs?
11. How might we show the relationships between tools within an ecosystem (alternatives, dependencies, suggestions) without overwhelming the user?
12. How might we handle install mechanisms that vary by ecosystem (winget for apps, dotnet tool install for .NET tools, npm/bun for Node packages) behind a unified UX?
13. How might we let users switch between alternatives (npm→bun, VS→Rider) and have that decision propagate across machines?

**Wizard Flow:**
14. How might we order the wizard so that earlier choices (profile, languages) naturally narrow what appears in later steps (dotfiles, apps)?
15. How might we make the wizard feel like a "new PC setup assistant" for casual users while giving devs the depth they want?

### Key Insights

1. **The product is a reconciliation engine, not a setup wizard.** The wizard is the first-run experience, but the *ongoing* value is drift detection and reconciliation. Every UX decision should serve the reconciliation mental model: expected state vs. actual state → user decides → Perch executes.

2. **"Languages" is an orchestration layer.** It doesn't replace Apps or Dotfiles — it composes them. Selecting ".NET" means installing SDKs (package manager operation), installing VS/Rider (app install), installing dotnet tools (language-specific package manager), and symlinking `nuget.config`/`global.json` (dotfile operation). The Languages page orchestrates across all three mechanisms.

3. **Four badge states + one no-badge state drive all UI:** (a) No badge = gallery item, not managed, not on system, (b) **Detected** (blue) = on system but not in Perch, (c) **Pending** (green-tinted for install, red-tinted for removal) = config changed, waiting for sync — covers both directions, (d) **Synced** (green) = in Perch and matches system, (e) **Drifted** (amber) = in Perch but system doesn't match unexpectedly. Machine overrides are managed via the detail page, not badges.

4. **Machine overrides are the escape valve that makes the whole system trustworthy.** Without them, users resist committing to full management because they fear the "one config to rule them all" model will break on their edge-case machine. Per-machine overrides turn a rigid system into a flexible one.

5. **Wizard step ordering is logical, not cascading.** Profile → Config Repo → Languages → Dotfiles → Apps → Tweaks → Deploy (with summary). Steps don't programmatically filter later steps — the ordering reflects importance (ecosystem-level before individual items). Profile gates which steps appear (Languages + Dotfiles hidden for Casual/Gamer). Review is merged into Deploy.

6. **Re-onboarding is emotionally different from first-time onboarding.** First-time: excitement, discovery. Re-onboarding: "ugh, this drifted again, let me fix it." The UX for re-onboarding should be *fast and forgiving* — show what drifted, let me click to fix, don't lecture me about why it drifted.

7. **The gallery serves two temporal modes:** discovery (first time — "what exists?") and memory (returning — "what did I have?"). Perch-config IS the memory. The gallery is the catalog. The tiered view (Detected / Top Choices / Suggestions / Other) serves both modes simultaneously.

8. **Dynamic paths are a category of problem, not a one-off.** VS 2026's hash-based settings path is the first example, but more will follow. Perch needs a path-resolution strategy (glob patterns, environment variable expansion, registry-key lookup) that handles this class of problem generically.

9. **First-time users need context, not a tutorial.** Two lightweight elements: (a) welcome copy on the Profile step explaining what Perch does in plain language ("Perch remembers how you like your computer set up"), (b) one-liner micro-copy on the first card grid ("Choose what Perch should manage. Nothing changes on your machine until you click Deploy."). After the first step, it disappears. No modals, no multi-screen onboarding.

10. **"Sync Everything" is never blind.** The wizard Deploy step shows a compact summary (Install: x, y, z. Remove: a, b. Tweaks: 1, 2, 3). The dashboard shows Pending cards as the preview itself — what you see IS what will happen. Revert mechanics (perch-config stores original values) exist in principle but need separate detailed analysis for chained changes.

11. **Casual users get local-only mode.** Cross-machine sync requires git, which casual users don't know. Wizard step 2 adapts by profile: Dev/PowerUser → "Select folder" (already cloned) or "Clone from URL." Casual/Gamer → auto-creates a local folder, no questions asked. Local-only still delivers value (detect/configure/deploy on this machine). Cross-machine sync for casual users is a future opportunity (Perch Cloud).

12. **The gallery is open source and solo-authored initially.** Community contributions via PRs grow it over time. The "Add unlisted app" escape hatch covers gaps until the gallery is comprehensive. Gallery size is intentionally curated — never a huge unmanageable list.

---

## IDEATE: Generate Solutions

### Selected Methods

**Combined approach:** Analogous Inspiration (Steam library model, VS Code extension tiers) to seed ideas → Brainstorming to expand across all three pillars → SCAMPER to evolve the existing wizard and dashboard. 30 ideas generated, reviewed one-by-one with the user.

### Generated Ideas

**Accepted ideas:**

| # | Idea | Pillar | Notes |
|---|------|--------|-------|
| 1 | Ecosystem cards layout | Languages | Large cards with logo, name, status badges ("5 detected", "3 drifted") |
| 3 | Ecosystem detail as "shopping list" | Languages | Categorized sections: Runtimes/SDKs, Editors/IDEs, Profilers, Decompilers, Global Tools, Dotfiles. Each item shows status + toggle |
| 5 | Machine override inline | Languages / Apps | "This machine only" toggle next to any item. Gray badge: "Override: projectX" |
| 6 | Global tools as compact checklist | Languages | Dotnet tools, node packages etc. as checklist, not full cards. Note: config change vs deploy are distinct actions |
| 9 | Batch install from perch-config | Apps | "12 apps in your config are not installed. Install all?" Reverse direction (detected but unmanaged → remove) deferred to backlog |
| 12 | Steam library mental model | Cross-cutting | perch-config = your library. Each machine installs what it needs. Universal analogy for the product |
| 21 | Template dotfiles with variables | Dotfiles | `{{perch.email}}` placeholders, prompted on first deploy. Already established in brainstorming |
| 25 | Summary merged into Deploy step | Wizard | No separate Review step. Deploy page shows summary at top + deploy button + progress |

**Rejected ideas with reasoning:**

| # | Idea | Why rejected |
|---|------|-------------|
| 2 | Detected-first landing page | Drift lives on the homepage dashboard, not buried in a page |
| 4 | "Switch to" action for alternatives | Alternatives can coexist side-by-side, not mutually exclusive |
| 8 | Ghost/dimmed entries for missing items | Drifted status + badges already handle this |
| 10 | "Capture this app" multi-step onboarding | Onboarding is a toggle switch, deploy does the rest |
| 11 | App-owned tweaks shown inline on cards | Cards stay simple. Tweaks live on the detail page (click into card) |
| 13 | Config diff preview before linking | First time there's nothing to diff. Later, perch-config always wins. Machine backup file is the safety net |
| 14 | "Why is this here?" provenance labels | Sort order IS the provenance — position tells the story |
| 15 | Smart suggestions based on ecosystem | Gallery already defines what belongs to each ecosystem |
| 16 | Quick-filter by status | Already sorted by status, filtering is redundant |
| 17 | "New & Untracked" filesystem scan | Dotfiles page uses curated gallery, not filesystem scan |
| 19 | Dotfile health dashboard | Already part of the homepage drift dashboard |
| 20 | "Re-adopt" flow for drifted items | "Drifted" status + deploy fixes it. No special flow needed |
| 26 | Wizard quick mode | Wizard always starts at Profile, no shortcuts |
| 27 | Universal status bar | Homepage dashboard covers this |
| 29 | Machine identity cards | Not needed |
| 30 | Notification on git pull | Perch runs on-demand, not in the background. Edge case: perch-config changes while app is running — flagged for later |

**New ideas emerged from discussion:**

| Idea | Pillar | Notes |
|------|--------|-------|
| Manual onboarding escape hatch | Cross-cutting | File/folder picker → select files → Perch symlinks + creates perch-config entries. Works for unknown apps, custom dotfiles, anything not in gallery |
| Config vs Deploy action distinction | Cross-cutting | Users must always know: "am I changing my config?" vs "am I changing my machine?" These are separate actions |
| "Track & Install Now" shortcut | Cross-cutting | Optional convenience that bundles config change + deploy for a single item. Power-user shortcut |
| Language-owned dotfiles | Languages | `.npmrc`, `nuget.config`, `global.json` live on the Languages page, NOT on the Dotfiles page. Dotfiles page is cross-cutting only (Git, PowerShell, Claude, SSH) |
| Unified page architecture | Cross-cutting | Languages, Dotfiles, Apps all share the same YAML gallery schema and the same WPF components. Pages are filtered views of the same gallery |
| Curated dotfiles gallery | Dotfiles | No filesystem scanning. Gallery defines which dotfiles exist. If it's not in the gallery, it doesn't show up. Manual onboarding is the escape hatch |
| Perch-config change detection | Cross-cutting | If perch-config changes on disk while the WPF app is running, app state goes stale. Needs a solution (file watcher? reload banner?) — flagged for later |

### Top Concepts

#### Concept 1: Unified Gallery Architecture

**All pages are the same thing.** Languages, Dotfiles, and Apps share:
- The same YAML schema in perch-gallery
- The same WPF card components
- The same status model (Drifted / Detected / Synced)
- The same sort order (Drifted → Detected → Synced → Top Tier → Suggested → Other → CLI Tools)
- The same "Add to Perch" / "Remove from Perch" button (config action, not deploy action)

Each page is a **filtered view** of the gallery scoped to a category. Languages = gallery entries tagged as language ecosystems. Dotfiles = gallery entries tagged as cross-cutting config files. Apps = everything else.

**Unified all the way down — including detail pages.** Every detail page has the same structure:
- **Header:** name, description, onboarding switch, links (docs, website, github)
- **Alternatives:** other items in the same space
- **Sub-categories:** labeled sections containing more app cards. The label varies by context ("Editors" for .NET, "Git UIs" for Git, "Extensions" for an app) but the component is identical.

All items are full app cards — including dotnet tools, node packages, and bun packages. No compact checklists. Every item gets: name, description, github stars, status, toggle. Same treatment everywhere.

If divergence is ever needed for a specific pillar, it can be introduced at that point. No preemptive specialization.

**Why this matters:** One codebase, one component library, one mental model for users. "Everything works the same way" is the most powerful UX pattern available.

*Refined via Critique & Refine elicitation: confirmed that detail pages are structurally identical across pillars, not just cards/grids.*

#### Concept 2: The Two-Action Model (Configure + Deploy)

Every interaction in Perch falls into one of two categories, and the UI must make clear which is happening:

1. **Configure** — change perch-config (git operation). Nothing changes on the machine. Same component used in wizard steps and dashboard pages.
2. **Deploy / "Sync Everything"** — apply perch-config to this machine (system operation). Install, symlink, apply tweaks. The machine changes. One-click from the dashboard.

**The primary UI mechanism is a button, not a toggle switch:**
- **"Add to Perch"** button on unmanaged/detected cards → adds to perch-config, card status changes to **Pending**
- **"Remove from Perch"** button replaces it after adding → removes from perch-config, card status changes to **Pending** (pending removal)
- Buttons, not toggles, because a button feels like a **deliberate action** and avoids the "I flipped a switch, did something happen?" confusion

**The badge/status model:**

| Badge | Color | Meaning | Button shown |
|-------|-------|---------|-------------|
| *(none)* | — | Gallery item, not managed, not on system | "Add to Perch" |
| **Detected** | Blue | On system, not in Perch | "Add to Perch" |
| **Pending** | Green-tinted (install) / Red-tinted (removal) | Config changed, waiting for sync | "Remove from Perch" / "Add to Perch" (opposite of pending action) |
| **Synced** | Green | In Perch, matches system | "Remove from Perch" |
| **Drifted** | Amber | In Perch, system doesn't match (unexpected) | "Remove from Perch" |

**The flow:**
- **Wizard:** click "Add to Perch" on cards across multiple steps, then deploy once at the end (Deploy step shows summary + progress).
- **Dashboard:** drift appears on the homepage. If drift is unintentional → click "Sync Everything" to deploy. If drift is intentional → click "Remove from Perch" on the item to update perch-config.
- **Machine overrides:** available on the detail page only (not on cards), where there is screen real estate to make it clear what's happening. Overrides let you exclude a specific item on this machine only.

No "Track & Install Now" shortcut — configure and deploy are always separate actions.

**Why this matters:** The biggest UX risk is users not knowing whether clicking something changes their config or their machine. The "Add to Perch" / "Sync Everything" split makes this unambiguous. Buttons feel like deliberate actions; the Pending badge confirms something is staged but not yet applied.

*Refined via Critique & Refine elicitation: retired toggle switch in favor of "Add to Perch" / "Remove from Perch" buttons. Retired "Track & Install Now" shortcut. Moved machine overrides to detail page only. Refined via Feynman Technique: established 5-state badge model (none, Detected, Pending, Synced, Drifted), retired "Not Installed" status.*

#### Concept 3: Wizard with Languages as Step 3

**Wizard order for Developer/PowerUser:** Profile → Config Repo (select folder / clone URL) → **Languages** → Dotfiles → Apps → Tweaks → Deploy (with summary)

**Wizard order for Gamer/Casual/Creative:** Profile → Config Repo (auto-created local folder, no git) → Apps → Tweaks → Deploy (with summary)

**Config Repo step adapts by profile:**
- Dev/PowerUser: "Select folder" (already cloned) or "Clone from URL" for git-based cross-machine sync
- Casual/Gamer: auto-creates a local Perch config folder, no questions asked. Local-only mode — still delivers detect/configure/deploy value on this machine, just no cross-machine sync. Future opportunity: Perch Cloud for casual cross-machine sync.

Languages comes before Dotfiles and Apps as a logical ordering (not a dependency cascade — Language selections do not programmatically filter later steps). The ordering reflects importance: ecosystem-level decisions are higher-leverage than individual app decisions.

The Deploy step merges the old Review + Deploy steps. Shows a categorized summary at top ("Languages: 3 ecosystems, 12 tools. Apps: 8. Dotfiles: 4. Tweaks: 15.") with a Deploy button. Progress and results appear below after clicking Deploy.

The wizard shows **all gallery entries** per step, not just detected ones. Detected items sort first, but you can onboard things not yet on the machine. The wizard is both "configure what you have" and "declare what you want."

**Returning users on a new machine** still go through the wizard. This is valuable because:
- The gallery evolves over time — new top choices, new tools, shifted rankings
- The wizard is a discovery surface, not just a checklist
- Power users who want to skip can use `perch sync` from the CLI

*Refined via Critique & Refine elicitation: clarified that "cascading" is logical ordering only (no programmatic dependencies between steps). Confirmed wizard re-use value for returning users.*

#### Concept 4: Manual Onboarding Escape Hatch

For apps not in the gallery, the user can manually onboard them. The entry point is a **button on the Apps page**: "Add unlisted app" (exact wording TBD).

**The flow:**
1. User clicks "Add unlisted app"
2. Perch shows a filtered list of installed apps not in the gallery (Core pre-filters obvious non-apps like Windows updates, runtime redistributables, drivers)
3. User picks the app from the list
4. Guided flow: "Where are FancyApp's settings?" → file/folder picker scoped to likely locations (AppData, program directory, etc.)
5. Perch creates the perch-config entry with selected paths and sets up symlinks on deploy

The resulting entry is barebones (name from package manager, no description/tags/alternatives) but fully functional. If the app is later added to the gallery with full metadata, the user's entry gets enriched automatically.

**Why a button, not a section:** The list of installed-but-unrecognized apps is huge (Windows updates, redistributables, framework patches). Even after Core filtering, it would clutter the main gallery view. This is a deliberate action for users who know they have something Perch doesn't cover.

*Refined via Critique & Refine elicitation: scoped down from raw file picker to guided flow starting from detected apps. Hidden behind a button rather than a permanent section.*

---

## PROTOTYPE: Make Ideas Tangible

### Prototype Approach

**ASCII wireframe prototypes** for the three core pages: Languages, Dotfiles, and Apps. Each page is prototyped at three screen levels: (1) page grid, (2) category/ecosystem detail, (3) item detail. Because the architecture is unified, prototyping Languages effectively prototypes the component library for all pages.

**What's reused vs. new:**
- Reused from existing Apps page: card grid layout, category drill-in navigation, StatusRibbon, TierSectionHeader, BreadcrumbBar
- New: ecosystem card with aggregate status badges (variant of existing category card), "Add to Perch" / "Remove from Perch" button (replaces toggle switch), ⚙ gear icon for drill-down indicator, machine override checkbox on detail page

### Prototype Description

#### Languages Page — Three Screens

**Screen 1: Languages Grid**

```
┌──────────────────────────────────────────────────────────┐
│ Languages                                      [Search]  │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐     │
│  │   ◆ .NET    │  │   ⬡ Node    │  │   🐍 Python │     │
│  │   Cross-plat│  │   JS runtime│  │   General    │     │
│  │   framework │  │   & tooling │  │   purpose    │     │
│  │ 5✓  2⚠  1● │  │ 3✓  1⚠     │  │         2●  │     │
│  └─────────────┘  └─────────────┘  └─────────────┘     │
│                                                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐     │
│  │   ♦ Ruby    │  │   ☕ Java   │  │   ⊕ C++     │     │
│  │   Dynamic   │  │   Enterprise│  │   Systems    │     │
│  │   scripting │  │   platform  │  │   language   │     │
│  │ 2✓         │  │             │  │              │     │
│  └─────────────┘  └─────────────┘  └─────────────┘     │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

- Ecosystem cards: logo, name, short description, aggregate status badges (only non-zero counts shown)
- No buttons on ecosystem cards — click anywhere to drill in
- Cards without badges (Java, C++) = gallery ecosystems not yet on system or in config
- Sorting: ecosystems with drift first, then detected, then synced, then unmanaged

**Screen 2: Ecosystem Detail (.NET)**

```
┌──────────────────────────────────────────────────────────┐
│ ◀ Languages > .NET                             [Search]  │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  [◆]  .NET                                               │
│  Microsoft's cross-platform development framework        │
│  [Docs]  [Website]  [GitHub]                             │
│                                                          │
│ ─── Runtimes & SDKs ──────────────────── 1⚠  2✓ ───── │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │ ⚠ Drifted    │  │ ✓ Synced     │  │              │  │
│  │ [□] .NET 8   │  │ [□] .NET 10  │  │ [□] .NET 6   │  │
│  │     SDK      │  │     SDK      │  │     SDK      │  │
│  │ ★ 12k  MIT   │  │ ★ 14k  MIT   │  │ ★ 12k  MIT  │  │
│  │[Add to Perch]│  │[Remove from  │  │[Add to Perch]│  │
│  │              │  │ Perch]       │  │              │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
│                                                          │
│ ─── Editors & IDEs ──────────────────── 2✓  1● ─────── │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │ ✓ Synced [⚙] │  │ ✓ Synced [⚙] │  │ ● Detect [⚙] │  │
│  │ [□] VS 2026  │  │ [□] Rider    │  │ [□] VS Code  │  │
│  │ Full IDE     │  │ JetBrains    │  │ Lightweight   │  │
│  │ ★ — Prop.    │  │ ★ — Prop.    │  │ ★ 165k MIT   │  │
│  │[Remove from  │  │[Remove from  │  │[Add to Perch]│  │
│  │ Perch]       │  │ Perch]       │  │              │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
│                                                          │
│ ─── Decompilers ─────────────────────── 1● ──────────── │
│ ─── Profilers ───────────────────────── 1✓ ──────────── │
│ ─── Global Tools ────────────────────── 1⚠  1✓ ─────── │
│ ─── Configuration Files ─────────────── 1✓ ──────────── │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

- Primary sort: **sub-categories** (ordered by gallery YAML)
- Secondary sort within sub-category: **status** (Drifted → Detected → Synced → unmanaged)
- Tertiary sort within status: **gallery sort index** (more interesting items first)
- Sub-category headers show aggregate status badges (only non-zero counts)
- ⚙ gear icon on cards that have a detail page (apps with tweaks). No gear = no drill-down (config files)
- Collapsible sub-categories for long sections

**Screen 3: Item Detail (Visual Studio 2026 — via ⚙ gear)**

```
┌──────────────────────────────────────────────────────────┐
│ ◀ Languages > .NET > Editors & IDEs > VS 2026  [Search]  │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  [Logo]  Visual Studio 2026 Community                    │
│  Full-featured IDE for .NET, C++, Python, and web dev    │
│  ✓ Synced                         ★ —    Proprietary     │
│  [Docs]  [Website]                                       │
│  [Remove from Perch]                                     │
│                                                          │
│ ─── App Settings ───────────────────────────────────── │
│  ☐ Add "Open in Visual Studio" to Explorer context menu  │
│  ☑ Set as default app for .sln files                     │
│  ☑ Set as default app for .slnx files                    │
│  ☐ Disable telemetry                                     │
│  ☐ Add to startup                                        │
│                                                          │
│ ─── Alternatives ───────────────────────────────────── │
│  ┌──────────────┐  ┌──────────────┐                     │
│  │ ✓ Synced [⚙] │  │ ● Detect [⚙] │                     │
│  │ [□] Rider    │  │ [□] VS Code  │                     │
│  │ JetBrains IDE│  │ Lightweight   │                     │
│  │ ★ — Prop.    │  │ ★ 165k MIT   │                     │
│  └──────────────┘  └──────────────┘                     │
│                                                          │
│ ─── Extensions ─────────────────────────────────────── │
│  ┌──────────────┐  ┌──────────────┐                     │
│  │ ✓ Synced [⚙] │  │         [⚙] │                     │
│  │ [□] ReSharper│  │ [□] CodeMaid │                     │
│  │ Code analysis│  │ Code cleanup │                     │
│  │ ★ 3.2k Prop. │  │ ★ 1.8k MIT  │                     │
│  │[Remove from  │  │[Add to Perch]│                     │
│  │ Perch]       │  │              │                     │
│  └──────────────┘  └──────────────┘                     │
│                                                          │
│ ─── Machine Override ───────────────────────────────── │
│  [ ] Exclude on this machine                             │
│      Visual Studio 2026 will be skipped during sync      │
│      on DESKTOP-HOME                                     │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

- Header: name, description, status badge, stars (clickable → GitHub), license, links, action button
- **App Settings:** app-owned tweaks as checkboxes (context menu entries, file associations, startup, telemetry). Defined in gallery YAML per app. These are config actions — applied on deploy.
- **Alternatives / Extensions:** same card components as everywhere else, cards all the way down
- **Machine Override:** simple "Exclude on this machine" checkbox with explanatory text. Sufficient because: swap = add both + exclude one; template overrides = `.machineName.config` convention; version overrides = add both + exclude per machine.
- Config files (nuget.config, global.json) have NO detail page — no ⚙ gear, no drill-down. Add/Remove is the only action.

**Design decisions confirmed during prototyping:**
- Ecosystem cards are containers (clickable, no action button) — NOT items you "Add to Perch"
- Sub-category ordering controlled by gallery YAML file order
- Three-level sort: category → status → gallery sort index
- ⚙ gear icon signals drill-down availability; absence means card is the complete experience
- Machine override checkbox is sufficient for all override scenarios
- Stars on cards are clickable links to GitHub

#### Dotfiles Page

Flat grid, no sub-categories. Same card components, same statuses, same buttons.

```
┌──────────────────────────────────────────────────────────┐
│ Dotfiles                                       [Search]  │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │ ⚠ Drifted[⚙] │  │ ✓ Synced [⚙] │  │ ✓ Synced [⚙] │  │
│  │ [□] Git      │  │ [□] Power-   │  │ [□] Claude   │  │
│  │   Config     │  │   Shell      │  │   Code       │  │
│  │ Git settings │  │ Shell profile│  │ AI assistant  │  │
│  │ ★ —    MIT   │  │ ★ —    MIT   │  │ ★ —    Prop. │  │
│  │[Remove from  │  │[Remove from  │  │[Remove from  │  │
│  │ Perch]       │  │ Perch]       │  │ Perch]       │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
│                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │ ● Detected   │  │              │  │              │  │
│  │ [□] SSH      │  │ [□] Editor   │  │ [□] WSL      │  │
│  │   Config     │  │   Config     │  │   Config     │  │
│  │ SSH settings │  │ .editorconfig│  │ .wslconfig   │  │
│  │ ★ —         │  │ ★ —          │  │ ★ —          │  │
│  │[Add to Perch]│  │[Add to Perch]│  │[Add to Perch]│  │
│  └──────────────┘  └──────────────┘  └──────────────┘  │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

- **Cross-cutting dotfiles only** — things not owned by a language ecosystem. Git, PowerShell, Claude, SSH, .editorconfig, .wslconfig.
- Language-specific dotfiles (.npmrc, nuget.config, global.json) live on the Languages page under their ecosystem.
- Flat list — the curated gallery keeps this page intentionally small (~8-10 items).
- Sort: Drifted → Detected → Synced → unmanaged, then gallery sort index within status.
- ⚙ gear icon on dotfiles that have a detail page (e.g., Git config → tweaks like "enable git-lfs", "set default editor"). Simple config files without tweaks have no gear.

#### Apps Page

Same architecture as Languages. Category grid → category detail → item detail.

```
┌──────────────────────────────────────────────────────────┐
│ Apps                                           [Search]  │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐     │
│  │ 🌐 Browsers │  │ 💬 Communic-│  │ 🎮 Gaming   │     │
│  │             │  │   ation     │  │              │     │
│  │ 2✓  1●     │  │ 3✓  1⚠     │  │ 1✓      2●  │     │
│  └─────────────┘  └─────────────┘  └─────────────┘     │
│                                                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐     │
│  │ 🔧 Utilities│  │ 📝 Editors  │  │ 🖥️ Terminal │     │
│  │             │  │             │  │              │     │
│  │ 5✓  1⚠     │  │ 2✓         │  │ 1✓          │     │
│  └─────────────┘  └─────────────┘  └─────────────┘     │
│                                                          │
│  ┌─────────────┐  ┌─────────────┐                       │
│  │ 🎨 Creative │  │ 📁 File Mgmt│                       │
│  │             │  │             │                       │
│  │        1●   │  │ 2✓         │                       │
│  └─────────────┘  └─────────────┘                       │
│                                                          │
│                   [Add unlisted app]                     │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

- **Identical to Languages** in structure: category cards with aggregate status badges → drill into category → item cards sorted by status then gallery index → item detail with app-owned tweaks
- Categories defined by gallery (Browsers, Communication, Gaming, Utilities, Editors, Terminal, Creative, File Management, etc.)
- **"Add unlisted app" button** at bottom — escape hatch for apps not in the gallery. Opens filtered list of installed-but-unrecognized apps (Core pre-filters non-apps), then guided flow to select config files to track.
- All three pages (Languages, Dotfiles, Apps) use the same card components, same status badges, same buttons, same sort logic. Build one, get all three.

#### Unified Architecture Confirmation

All three pages proven to be the same architecture:

| Aspect | Languages | Dotfiles | Apps |
|--------|-----------|----------|------|
| Grid level | Ecosystem cards | Flat card grid | Category cards |
| Drill-in | Sub-categories with cards | N/A (flat) | Sub-categories with cards |
| Item detail | ⚙ gear → tweaks, alternatives, extensions, override | ⚙ gear → tweaks (if any) | ⚙ gear → tweaks, alternatives, extensions, override |
| Card component | Universal | Universal | Universal |
| Status model | Drifted/Detected/Synced/Pending | Same | Same |
| Sort logic | Category → Status → Gallery index | Status → Gallery index | Category → Status → Gallery index |
| Action button | Add/Remove from Perch | Same | Same |
| Special | — | Language-owned dotfiles excluded | "Add unlisted app" escape hatch |

### Key Features to Test

1. **"Add to Perch" / "Remove from Perch" button** — Does the button feel deliberate enough? Does the user understand they're changing config, not their machine?
2. **Status badges on category/ecosystem cards** — Are aggregate counts (5✓ 2⚠ 1●) scannable at a glance? Do users understand what the numbers mean?
3. **Category-first sorting with status within** — Does organizing by Runtimes/Editors/Tools feel natural? Or would users prefer status-first (all drifted items together)?
4. **⚙ gear icon for drill-down** — Is it discoverable? Do users know to click it for app settings?
5. **Machine override on detail page** — Is the explanatory text clear enough? Do users find it when they need it?
6. **First-time micro-copy** — Does "Choose what Perch should manage. Nothing changes until you click Deploy." land with new users?
7. **Pending badge direction via color** — Do green-tinted (install) and red-tinted (removal) communicate direction without text?
8. **Config files without drill-down** — Do users expect to click into nuget.config? Or is the card sufficient?
9. **Dotfiles page size** — Is ~8-10 items too sparse? Does the page feel useful or vestigial?
10. **"Add unlisted app" discoverability** — Do users find the button when they need it? Is the filtered list of unrecognized apps manageable after Core filtering?

---

## TEST: Validate with Users

### Testing Plan

{{testing_plan}}

### User Feedback

{{user_feedback}}

### Key Learnings

{{key_learnings}}

---

## Next Steps

### Refinements Needed

{{refinements}}

### Action Items

{{action_items}}

### Success Metrics

{{success_metrics}}

---

_Generated using BMAD Creative Intelligence Suite - Design Thinking Workflow_

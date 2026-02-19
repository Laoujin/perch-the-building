# Sprint Change Proposal — Desktop-First Pivot

**Date:** 2026-02-19
**Author:** Wouter + AI (Correct Course workflow)
**Scope:** Major — strategic pivot affecting project classification, scope ordering, and epic structure

## Section 1: Issue Summary

### Problem Statement

Perch was planned and documented as a CLI-first tool (project type: `cli_tool`). The CLI and core engine were built first (Epics 1-8). Then the WPF Desktop app was built (Epics 10-11) and proved to be the better primary experience. The project has pivoted to Desktop-first, but the planning artifacts (PRD, architecture doc, epics, sprint status) were never updated to reflect:

1. The strategic pivot from CLI-first to WPF Desktop-first
2. The actual implementation progress — 11 epics completed but sprint-status showed only Epic 1 in-progress
3. The new priority ordering: Languages > Dotfiles/Apps > Dashboard/Wizard > Tweaks (analysis) > CLI (evaluation)

### Discovery Context

Identified during a course-correction review on 2026-02-19 when the gap between planning artifacts and reality became apparent.

### Evidence

- PRD classified project as `cli_tool` — Desktop app already built with 6 pages, 7 controls, 10 ViewModels
- Sprint-status.yaml showed Epic 1 "in-progress" at story 1-1 — code has 14 CLI commands, full Desktop app, 180+ Core files, 91 test files
- Scope ordering placed Desktop in Scope 3 — Desktop is the actual primary interface
- Phase D5 (Ecosystem/Languages onboarding) was lowest priority — user identifies it as P1

## Section 2: Impact Analysis

### Epic Impact

| Impact | Detail |
|--------|--------|
| Epics 1-8 | Mark as **done** — CLI and Core fully implemented |
| Epics 10-11 | Mark as **done** — Desktop foundation complete |
| Epic 14 | Mark as **done** — Gallery schema implemented |
| Epics 9, 12, 13, 15 | Remain backlog, deprioritized |
| New Epic 20 | **P1** — Languages Page + Gallery (was D5, lowest priority) |
| New Epic 21 | **P2** — Dotfiles Page + Gallery |
| New Epic 22 | **P2** — Apps Page + Gallery (sorting, categories, alternatives) |
| New Epic 23 | **P3** — Dashboard & Wizard polish |
| New Epic 24 | As needed — Desktop structural fixes |
| New Epic 25 | Deferred — Tweaks expansion (needs brainstorm) |
| New Epic 26 | Deferred — CLI scope evaluation |

### Artifact Conflicts

| Artifact | Conflict | Resolution |
|----------|----------|------------|
| PRD classification | `projectType: cli_tool` | Changed to `desktop_app_with_cli` |
| PRD Executive Summary | CLI-first framing | Rewritten as Desktop-first |
| PRD Business Success | Scope 1 = CLI MVP | Scope 1 = done, Scope 2 = Desktop polish |
| PRD Measurable Outcomes | CLI-centric metrics | Updated with Desktop-first metrics |
| PRD Phase sections | Phase 1/2/3 CLI-first ordering | Replaced with Scope 1 (done) + P1/P2/P3 structure |
| Architecture doc | "CLI primary" narrative | Reframed as "Desktop primary" |
| Architecture status | "READY FOR IMPLEMENTATION" | Changed to "IMPLEMENTED" |
| Epics doc | Only old CLI-first epics | Added pivot note + new epic summary at top |
| Sprint-status.yaml | Epic 1 in-progress, rest backlog | 11 epics marked done, new epics 20-26 added |

### Technical Impact

**None.** The architecture is sound — Core/CLI/Desktop separation is clean, `AddPerchCore()` shared DI works correctly, rendering boundaries are respected. The pivot is about priorities and documentation, not code restructuring.

## Section 3: Recommended Approach

### Selected Path: Direct Adjustment + Scope Redefinition

**Rationale:**
- Architecture requires zero changes — it already supports Desktop-first
- Code requires zero changes — Desktop app works
- Only planning artifacts needed updating
- No rollback, no MVP redefinition beyond acknowledging what's done

**Effort:** Low — document updates only
**Risk:** Low — no code changes, no architectural risk
**Timeline Impact:** None — this unblocks work by clarifying priorities

## Section 4: Detailed Change Proposals

All changes below have been **applied**.

### 4.1 sprint-status.yaml
- Marked Epics 1-8, 10-11, 14 as `done`
- Added pivot note explaining the change
- Created new Epics 20-26 with P1/P2/P3 priority structure
- Story-level breakdown for Epics 20-22

### 4.2 PRD — Classification (frontmatter)
- `projectType: cli_tool` → `desktop_app_with_cli`
- `complexity: low` → `medium`
- Scope definitions rewritten to reflect done work + new priorities

### 4.3 PRD — Executive Summary
- Reframed from "cross-platform dotfiles manager" to "Windows-first Desktop app"
- Core differentiator updated to emphasize visual Desktop experience
- Target user broadened from "developer with CLI" to "developer or power user with Desktop app"
- Technology section reordered: Desktop listed first, CLI second

### 4.4 PRD — Business Success
- Scope 1 marked as done
- Scopes 2-5 rewritten to match P1/P2/P3/P4/P5 priorities

### 4.5 PRD — Measurable Outcomes
- Done outcomes moved to Scope 1
- New Desktop-first measurable outcomes for Scopes 2-4

### 4.6 PRD — Phase sections (major rewrite)
- Phases 1/2/3 collapsed into "Scope 1: Foundation (Done)" with summary
- Phase D (D1-D6) replaced with priority-based sections:
  - Scope 2: P1 (Languages) + P2 (Dotfiles, Apps) with full specifications
  - Scope 3: P3 (Dashboard, Wizard, Persist to Config)
  - Scope 4: Tweaks brainstorm + CLI evaluation
  - Structural Fixes (as needed)
  - Deferred items consolidated

### 4.7 Architecture doc — Narrative updates
- Primary domain: "CLI tool" → "WPF Desktop app + CLI"
- Technology domain updated
- Initialization note updated (all projects working)
- Status: "READY FOR IMPLEMENTATION" → "IMPLEMENTED"

### 4.8 Epics doc — Pivot note
- Added pivot summary at top with completed/new epic tables
- Original content retained as reference

## Section 5: Implementation Handoff

### Change Scope: Minor

All changes are document updates — already applied during this workflow. No code changes required.

### Next Steps

1. **Begin Epic 20 (P1):** Languages page scaffold + first language gallery entry (.NET or Node)
2. **Address structural fixes** from Epic 24 as they block P1/P2 work (e.g., shared page scaffolding before adding Languages page)
3. **Create story files** for Epic 20 stories when ready to start implementation

### Success Criteria

- Planning artifacts accurately reflect the Desktop-first reality
- New priorities (P1/P2/P3) are clear and actionable
- Sprint-status.yaml is the single source of truth for progress tracking
- No confusion about what's done vs what's next

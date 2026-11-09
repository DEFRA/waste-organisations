---
name: spec-breakdown
description: >
  Split a spec in specs/ whose work is too big for one pull request into child specs, one per
  slice, each within the repo's PR size guide and independently testable, with dependencies and
  an order. Writes the child specs in the repo's spec format and updates the parent spec's Work
  breakdown table; never implements and never edits a delivered spec. Use whenever asked to split,
  slice, break down or decompose a spec, story or epic, for example "/spec-breakdown
  specs/0007-REGEN-150-....md", "/breakdown", "this spec is too big", "split REGEN-150 into
  smaller PRs". For writing a spec from a story or idea, use spec-clarify.
---

# Spec breakdown

Adapted for this repo from the Equal Experts llm-toolkit `commands/breakdown.md`
(https://github.com/EqualExperts/llm-toolkit, commit a46825d, 22 April 2026). Last synced
9 November 2026. The upstream command splits a generic work item into tracked items; this skill
splits a spec into child specs, because in this repo a spec is the unit of review and each slice
becomes one pull request (specs 0003 and 0005 are two slices of one story, REGEN-141).

## Usage

`/spec-breakdown specs/NNNN-REGEN-NNN-slug.md` in Claude Code, or `$spec-breakdown` in Codex.
The argument is the parent spec; without one, ask which spec to split.

## Ground rules

- **The parent spec is the input.** Read it in full, together with `AGENTS.md` ("Specs" and the
  size guide referenced from "Git workflow"), `doc/rules/git-workflow.md` and the latest specs for
  numbering and format. Search the code before assuming anything is missing.
- **Ask before splitting.** Confirm with the author what makes the work too big and which
  strategy fits, then confirm the proposed slices before writing anything.
- **Specs only.** Write new files under `specs/` and update the parent spec's Work breakdown.
  Do not change code, tests or the changelog. A delivered spec is never edited; if the parent is
  delivered, the breakdown is a new spec that names it.
- **Every slice leaves main shippable.** A slice may add a changeset, an entity or an endpoint
  that nothing uses yet, but it never breaks a v1 test, never half-applies a schema change, and
  never leaves a failing build for the next slice to fix.

## Strategies

Choose one and say why:

1. **Schema first, then behaviour**: the changelog and entities as their own slice when entities
   change shape, then the endpoint and service slices (the pattern of specs 0003 and 0005).
2. **Vertical slicing** by behaviour: each slice is one endpoint or one rule, end to end, with
   its tests. The default for v2 features once the schema exists.
3. **Risk-based**: a spike spec first (time-boxed, with a written finding as its deliverable),
   then implementation slices with the unknowns resolved.
4. **Dependency-based**: separate what can run in parallel from what must be sequential, so two
   developers or two Ralph runs do not collide in the same files.
5. **Scope-based**: minimum first, then enhancements, then polish, when the deadline is fixed.
6. **By acceptance criterion**: one slice per major criterion, when the parent's criteria are
   already independent.

## Process

1. **Understand the parent.** Its type, priority, acceptance criteria, decisions, open questions
   and the areas it touches: changelog, entities, DTOs and validation, services, endpoints,
   OpenAPI and ReDoc, README, tests and snapshots.
2. **Analyse why it is too big.** Estimate the non-generated lines per area and compare with the
   size guide; name the mixed concerns; list the unknowns. A parent that fits one pull request
   does not need splitting: say so and stop.
3. **Choose a strategy** with the author.
4. **Propose the slices.** For each: title, what it includes and excludes, which parent
   acceptance criteria it takes (every parent criterion lands in exactly one slice), what it is
   verified by, its dependencies, whether it can run in parallel with another, and its estimated
   size. Show the dependency order. Confirm or adjust with the author.
5. **Write the child specs**, one file each, `specs/NNNN-REGEN-NNN-<slice-slug>.md`, numbered
   consecutively from the next free number, in the repo's spec structure (same headings as
   `spec-clarify` produces). In the metadata table, `Type` starts "Part N of M of REGEN-NNN" and
   `Parent` names the parent spec file. Acceptance Criteria are the parent's criteria for that
   slice, reworded only where the slice boundary requires. Out of Scope names the sibling slices.
   Decisions repeat nothing: they reference the parent's Decisions and add only what the split
   itself decided.
6. **Update the parent.** Add or replace its "### Work breakdown" table under Proposed Solution:
   one row per slice with the child spec file, the work and what verifies it, in order. This is
   allowed because the parent is in progress.
7. **Summarise.** Parent title, the slices with their dependency graph, the suggested order, what
   can run in parallel, and the first slice to start.

## Sizing

- A slice stays within the size guide in `doc/rules/git-workflow.md` (about 800 non-generated
  changed lines; snapshots and lock files do not count) and mixes no unrelated concerns.
- A slice has at least one testable acceptance criterion and its own verification (which test
  project, which Compose step). A slice with no observable result is too small; fold it in.
- A slice is a good size for one Ralph build iteration or one focused day of work.
- Prefer fewer, well-bounded slices to many tiny ones; the overhead is a pull request each.

## Do and do not

Do:

- Keep the parent's context in each child so it can be read alone.
- Make dependencies reflect real technical constraints, and say what can run in parallel.
- Give each slice a title that says what is true when it is merged.
- Explain the strategy chosen and the sizes estimated.

Do not:

- Split a spec that fits one pull request.
- Create circular dependencies, or a slice that needs a later slice to compile or pass tests.
- Lose a parent acceptance criterion, or let one land in two slices.
- Edit a delivered spec, implement anything, or create branches.

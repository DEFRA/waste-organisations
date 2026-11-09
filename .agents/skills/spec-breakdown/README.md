# spec-breakdown

Splits a spec that is too big for one pull request into child specs, one per slice, each within
the PR size guide (about 800 non-generated changed lines, see `doc/rules/git-workflow.md`) and
independently testable, with dependencies and an order. Each child spec becomes one pull request,
which is how specs 0003 and 0005 delivered REGEN-141 in two steps.

Adapted from the Equal Experts llm-toolkit `breakdown` command (commit a46825d). The agent-facing
procedure is in [SKILL.md](SKILL.md); this file is for developers.

## When to use it

- `spec-clarify` ended by saying the spec needs more than one pull request.
- A spec mixes concerns that review better apart: a schema change, an endpoint, documentation.
- A story has unknowns that deserve a spike before the implementation slices.
- A pull request review flagged the PR as over the size guide and the work has to be re-planned.

## How to start it

| Tool        | How                                                                                   |
| ----------- | ------------------------------------------------------------------------------------- |
| Claude Code | `/spec-breakdown specs/0007-REGEN-150-return-an-organisation-by-id.md`                 |
| Codex       | `$spec-breakdown` (or pick it from `/skills`), then name the spec file                   |
| Either      | Ask in plain words: "split spec 0007 into smaller PRs"; the skill loads from its description |

## What happens

1. It reads the parent spec, the size guide and the code, estimates the size per area and tells
   you why the work is too big, or that it is not and should stay one pull request.
2. It proposes a strategy (schema first, vertical slices, spike first, by dependency, by scope,
   by criterion) and the slices: title, scope, which parent acceptance criteria each takes,
   dependencies, what can run in parallel, estimated size. You confirm or adjust.
3. It writes one child spec per slice, `specs/NNNN-REGEN-NNN-<slice-slug>.md`, numbered from the
   next free number, in the standard spec format, each marked "Part N of M" with the parent named.
4. It updates the parent spec's "Work breakdown" table so the parent lists its children in order.
5. It summarises the dependency graph and the first slice to start.

It never writes code and never edits a delivered spec.

## What you get

See [examples/example-breakdown.md](examples/example-breakdown.md): a synthetic parent split into
three child specs with their order and sizes.

## Afterwards

- Review the child specs as a pull request if the split changes what the team agreed.
- Implement or run the Ralph loop one child spec at a time; the review skills check each pull
  request against its own spec's acceptance criteria and against the size guide.

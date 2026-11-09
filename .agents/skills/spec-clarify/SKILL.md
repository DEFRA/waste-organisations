---
name: spec-clarify
description: >
  Turn a Jira story, bug or rough idea for this API into a spec in specs/, in the repo's spec
  format, by asking the author questions one topic at a time: problem, scope, testable acceptance
  criteria with negative cases, risks, priority, size. Writes specs/NNNN-REGEN-NNN-slug.md and
  nothing else; never implements and never edits a delivered spec. Use whenever asked to write,
  draft, refine or clarify a spec or story, for example "/spec-clarify REGEN-150 ...", "/clarify",
  "write the spec for REGEN-150", "turn this story into a spec", "refine this idea". For splitting
  a spec that is too big for one pull request, use spec-breakdown.
---

# Spec clarify

Adapted for this repo from the Equal Experts llm-toolkit `commands/clarify.md`
(https://github.com/EqualExperts/llm-toolkit, commit a46825d, 22 April 2026). Last synced
9 November 2026. The upstream command refines a generic work item; this skill produces a spec
in the format this repo already uses (`specs/0005-REGEN-141-create-an-organisation.md` is the
fullest example).

You are running an idea refinement workshop with the developer. The output is a spec that a
reviewer can approve and that the Ralph build loop, or a developer, can implement without
asking the author what was meant.

## Usage

`/spec-clarify REGEN-NNN <one-line idea>` in Claude Code, or `$spec-clarify` in Codex. Without
an argument, ask for the story key and a one-line description first.

## Ground rules

- **Ask, do not assume.** Work through the phases below one topic at a time, in chat, offering
  options where you can and waiting for the answer. Use the tool's question facility if it has
  one; plain questions in chat are fine otherwise.
- **Read before asking.** Read `AGENTS.md` (especially "Specs"), the latest two specs in `specs/`
  for format and prior decisions, and the code the idea touches. Confirm what already exists with
  a code search before calling anything missing.
- **Sources are referenced, not pasted.** Ask the author to paste the Jira story text, comments
  and the relevant Confluence section if you cannot read them; cite them by title and date in the
  spec. Never put credentials or personal data into the spec.
- **Specs only.** Write one file under `specs/`. Do not change code, tests, the changelog or any
  other spec. A delivered spec (one whose story is merged, for example 0001 to 0004) is never
  edited: a change to something it decided is recorded in the new spec's Decisions with the
  earlier spec named (AGENTS.md, "Specs").
- **Testable means testable here.** Every acceptance criterion names an observable result a
  test in `Api.Tests`, `Api.IntegrationTests` or `Api.V1.Tests` can check.

## Phases

### 1. Understand

Establish: the problem or opportunity, who benefits (Manage Account, other W&CE services, the
team, operations), what triggered it, and what success looks like. Then search the code and
record what is already in place (versioning, schema, authentication, existing endpoints), with
file names.

### 2. Classify

Feature, Bug, Improvement, Spike (time-boxed research), Task (no direct user impact), or Epic.
An Epic is not one spec: say so and point at `spec-breakdown`.

### 3. Scope

What is in, what is explicitly out, what the minimum is. Ask about anything an earlier spec
decided that this work would change; those become Decisions that name the superseded spec.
Confirm that v1 behaviour stays frozen unless the story says otherwise.

### 4. Acceptance criteria

Three to twelve criteria, each one line, each checkable by a test. Cover the negative paths as
carefully as the positive: `400` with the error key by JSON path, `401` and `403`, `405` or `406`
for the version rules, concurrency, what is stored and what is not, "no v1 test changes". Map
Jira scenarios to criteria by number. Bugs get current behaviour, expected behaviour, steps to
reproduce and impact in Problem/Opportunity, and a criterion that the bug no longer occurs.

### 5. Risk and dependencies

Unknowns, risky areas (schema and changelog, the v1 contract, versioning, CDP configuration),
dependencies on other stories or teams, and a risk level (low, medium, high) with the reason. For
high uncertainty, propose a spike spec first.

### 6. Priority

P0 critical (blocking), P1 high, P2 medium (default), P3 low, P4 backlog, with the rationale.

### 7. Size

Estimate the pull requests the work needs. A spec is one pull request when its non-generated
change stays within the size guide in `doc/rules/git-workflow.md` and mixes no unrelated concerns
(schema change, endpoint, documentation). If it needs more, either write the Work breakdown table
(slices with what each is verified by, as spec 0005 does) or stop and recommend `spec-breakdown`
on the finished spec.

### 8. Write the spec

Path: `specs/NNNN-REGEN-NNN-slug.md`, where `NNNN` is the next number after the highest in
`specs/` and the slug comes from the story title. Use this structure, with the section headings
exactly as written:

```markdown
# REGEN-NNN: Title

| | |
|---|---|
| Type | Feature / Bug / Improvement / Spike / Task, with one sentence on what changes and what does not |
| Priority | P0 to P4 with the rationale |
| Parent | Parent story or epic, if any |
| Risk | Low / Medium / High (see Risks) |
| Source | Jira story and comments by date, Confluence pages by title and version, refinement answers by date |

## Problem/Opportunity

## Proposed Solution

(subsections as needed: request, response, errors, validation, persistence, schema, code layout,
OpenAPI, tests; end with "### Work breakdown" when the work is more than one slice)

## Acceptance Criteria

- [ ] one line per criterion, Jira scenario numbers in brackets

## Out of Scope

## Decisions

| Topic | Decision |
|---|---|
| ... | what was decided, when, why; name any earlier spec it supersedes |

## Open Questions

## Dependencies

## Risks

## Notes
```

British English, absolute dates, no pasted sources, no personal data. Keep the Proposed Solution
concrete enough to implement from: field names, status codes, table and column names, file and
folder names that follow `doc/rules/csharp-dotnet.md`.

### 9. Present and confirm

Show the author the type, priority, title, acceptance criteria and open questions, and ask
whether the spec says what they meant. Adjust until it does.

### 10. Next steps

Say which applies: the spec is one pull request and development can start; it needs
`spec-breakdown`; it needs a spike first; or open questions block it and who can answer them. A
spec can be reviewed as its own pull request before implementation starts; recommend that for
anything larger than one slice.

## Do and do not

Do:

- Push for concrete, testable criteria and for the negative cases.
- Make exclusions explicit; Out of Scope is where scope creep is stopped.
- Name the earlier spec whenever a decision changes something it decided.
- Record who decided what and when in Decisions, so the spec is a record, not just a plan.
- Keep the spec within one pull request or hand over to `spec-breakdown`.

Do not:

- Accept vague criteria ("make validation better", "improve performance").
- Skip the "why"; the Problem/Opportunity section is what a reviewer judges the solution against.
- Edit a delivered spec, or paste its text into the new one.
- Start implementing, create branches, or change anything outside `specs/`.
- Invent Jira keys, dates or decisions; ask.

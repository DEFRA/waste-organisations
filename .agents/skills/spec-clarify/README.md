# spec-clarify

Turns a Jira story, bug or idea into a spec in `specs/`, in the format the team already uses, by
asking you questions one topic at a time. It is the first step of every story in this repo: the
spec is what reviewers approve and what development is planned from.

The agent-facing procedure is in [SKILL.md](SKILL.md); this file is for developers.

## When to use it

- A new story is ready for refinement and needs a spec before development.
- A bug needs a spec with reproduction steps and acceptance criteria.
- An idea is still vague and you want it shaped into something actionable, or found to be an
  epic whose stories each need their own spec.

## How to start it

| Tool        | How                                                                                   |
| ----------- | ------------------------------------------------------------------------------------- |
| Claude Code | `/spec-clarify REGEN-150 Return an organisation by id through v2`                      |
| Codex       | `$spec-clarify` (or pick it from `/skills`), then give the story key and one line        |
| Either      | Ask in plain words: "write the spec for REGEN-150"; the skill loads from its description |

Have the Jira story text, its comments and the relevant Confluence section to hand. The agent
cannot read Jira or Confluence, so it will ask you to paste what matters and will cite them by
title and date.

## What happens

1. It reads `AGENTS.md`, the latest specs and the code the story touches, then asks about the
   problem, who benefits, scope, acceptance criteria, risks, priority and size. Answer one topic
   at a time; say "don't know" where you don't and it becomes an Open Question.
2. It writes `specs/NNNN-REGEN-NNN-slug.md` with the standard sections: metadata table,
   Problem/Opportunity, Proposed Solution, Acceptance Criteria, Out of Scope, Decisions, Open
   Questions, Dependencies, Risks, Notes.
3. It shows you the summary and adjusts until the spec says what you meant.
4. It tells you whether the work fits one pull request. If not, it agrees the slices with you and
   writes them in the spec's "Work breakdown" table, one pull request each; the spec stays one
   file.

It never writes code, never touches another spec, and never edits a delivered spec; a change to
something an earlier spec decided is recorded in the new spec's Decisions, naming that spec.

## What you get

A spec shaped like the ones already in `specs/`: `specs/0005-REGEN-141-create-an-organisation.md`
is the fullest example of a feature spec, `specs/0001-REGEN-145-test-scenarios-for-existing-v1-api.md`
of a task spec.

## Afterwards

- Open the spec as its own pull request if the work is more than one slice, so the team reviews
  the plan before the code.
- Start development, by hand or with an AI coding tool; the review skills check the pull request
  against the spec's acceptance criteria.
- When the spec has a Work breakdown, build one slice at a time ("build slice 1 of spec NNNN")
  and open a pull request per slice. Development is complete when every slice is merged.

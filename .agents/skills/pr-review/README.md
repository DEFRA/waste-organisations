# pr-review

Reviews a colleague's pull request against the linked Jira story, its spec and this repo's
standards, and gives you a report to act on. It reads only: it never approves, requests changes
or comments on GitHub. A human still reviews and approves every pull request; this skill makes
that review faster and better informed.

The agent-facing procedure is in [SKILL.md](SKILL.md); this file is for developers. To check
your own pull request before asking for review, use [pr-preflight](../pr-preflight/README.md).

## When to use it

- You have been asked to review a pull request and want a first pass before you read the diff.
- A pull request is large and you want to know which files were covered and where to look.
- You want the acceptance criteria of the story mapped to the tests and changes in the PR.

## Before you start

- `gh` must be signed in to the GitHub account that has access to `DEFRA/waste-organisations`.
  With two accounts, `gh auth switch --user <account>` first; a read-only account is enough.
- Have the Jira story (or its spec file under `specs/`) to hand. The agent cannot read Jira, so it
  will ask you to paste the acceptance criteria if the spec does not carry them.

## How to start it

| Tool        | How                                                                               |
| ----------- | --------------------------------------------------------------------------------- |
| Claude Code | `/pr-review 229` (a PR number, URL or branch name)                                 |
| Codex       | `$pr-review` (or pick it from `/skills`), then give the PR number                    |
| Either      | Ask in plain words: "review PR #229"; the skill loads from its description         |

## What happens

1. It fetches the PR metadata, diff and CI status with `gh`, counts generated files and compares
   the size with the guide in `doc/rules/git-workflow.md`.
2. It establishes the spec: the story's acceptance criteria, the `specs/` file and the rules in
   `doc/rules/` that apply to the lines the PR touches.
3. It works through the review lenses that apply (security, quality, redundancy, testing,
   api-contract, data, architecture, dependencies, PR hygiene, spec), reading the matching
   `doc/rules` file for each, and records the lenses it skipped and why.
4. It maps each acceptance criterion to its evidence in the PR and classifies every finding as
   blocking (in spec), non-blocking (in spec) or out of scope. Out-of-scope ideas become
   suggested Jira stories, never requests on the PR.
5. It states which files it read in full, skimmed or did not review, and ends with where your
   judgement is needed.

## What you get

A report in chat with the PR summary, the acceptance criteria table, blocking and non-blocking
findings with file and line references, suggested new stories, and a verdict that is never an
approval. Blocking findings carry `confirmed` (reproduced) or `from reading` so you know which to
trust first.

## Afterwards

- Read the files it skimmed and the places named under "Human review focus".
- Post findings to GitHub yourself, in your own words, following the review comments guidance.
- If you want the agent to post specific findings, say so explicitly; it posts only what you
  approve.

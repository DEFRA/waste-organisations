# pr-preflight

Checks your own pull request before you ask a colleague to review it, with the same lenses as
[pr-review](../pr-review/README.md), then fixes the mechanical, in-spec blocking findings it has
confirmed, verifies them with the full repo gate, commits them as a new commit and pushes to your
branch. It posts nothing to GitHub. The goal is that the reviewer finds nothing you could have
caught yourself.

The agent-facing procedure is in [SKILL.md](SKILL.md); this file is for developers.

## When to use it

- Your pull request is ready and you are about to request review.
- You pushed a fix after review and want to check nothing else slipped.
- You want the "Human review focus" text for the PR description.

## Before you start

- `gh` must be signed in as the account that opened the PR. The skill compares the PR author with
  `gh api user` and stops if they differ, so with two GitHub accounts run
  `gh auth switch --user <account>` first.
- Docker must be running and host port 5432 must be free. A local PostgreSQL on 5432 makes the
  integration tests authenticate against the wrong database and fail; stop it before running
  the skill rather than changing ports (see "Postgres And Liquibase" in `AGENTS.md`).
- The .NET SDK and the restored packages, as for any local build.
- No uncommitted work you want to keep unstashed: the skill checks out the PR's head branch.

## How to start it

| Tool        | How                                                                                 |
| ----------- | ----------------------------------------------------------------------------------- |
| Claude Code | `/pr-preflight 229` (a PR number, URL or branch name)                                |
| Codex       | `$pr-preflight` (or pick it from `/skills`), then give the PR number                   |
| Either      | Ask in plain words: "preflight PR #229" or "is my PR ready for review?"             |

## What happens

1. It confirms the PR is yours, fetches metadata, diff and CI status, and compares the size with
   the guide in `doc/rules/git-workflow.md`.
2. It reviews with the pr-review lenses, maps the acceptance criteria to evidence and classifies
   every finding.
3. It fixes a blocking finding only when the finding is confirmed, the fix is mechanical, in
   spec, and needs no judgement call you have not already made. Frozen things (v1 tests,
   verified snapshots, delivered specs) and anything needing a decision are reported instead.
4. It runs the full gate as `AGENTS.md` describes: CSharpier, the build, `Api.Tests`, then
   `Api.IntegrationTests` and `Api.V1.Tests` against Docker Compose, and checks coverage has not
   dropped. A fix that fails the gate is reverted and reported.
5. It commits the fixes with the `commit` skill, noting they were caught in self-review, and
   pushes to the PR's branch.

## What you get

A report in chat: the PR summary with size and coverage lines, the acceptance criteria table,
findings tagged `[FIXED]` or `[NEEDS MANUAL FIX]`, suggested new stories, a verdict and the
"Human review focus" to paste into the PR description.

## Afterwards

- Deal with the `[NEEDS MANUAL FIX]` items; they need your decision or a non-mechanical change.
- Update the PR description if the report says it no longer matches the diff.
- Paste the "Human review focus" into the description, then mark the PR ready for review.

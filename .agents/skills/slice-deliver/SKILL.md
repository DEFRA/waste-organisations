---
name: slice-deliver
description: >
  Deliver one slice of a spec's Work breakdown as a pull request that is ready for human review:
  build it with the ralph loop (ralph auto: archive, init, plan, build, review, fix), run the
  repo's full gate, open a draft pull request, run the pr-preflight skill and apply its mechanical
  fixes, wait for CI, mark the pull request ready, and report a short summary with a message for
  the team that carries the pull request link. Stops and reports in chat whenever a step fails or
  needs a decision, and continues from that step when it is run again. Use whenever asked to
  build, complete, deliver or finish a slice of a spec end to end, for example "/slice-deliver
  spec 5 slice 1", "complete slice 1 in spec 5", "build slice 2 of spec 0005 and open the PR",
  "continue slice 1 of spec 6". One slice per run; never merges, never requests reviewers.
---

# Slice deliver

Last reviewed 12 November 2026.

You are taking one slice of a spec from "not started" to "pull request ready for review" without
the developer running each step by hand. A slice is one row of the spec's "Work breakdown" table
and becomes one pull request (`AGENTS.md`, "Specs"). The developer reads only what you put in
chat: a short report at the end, or a clear stop when something needs them.

The work is a fixed sequence of stages. A script, `.agents/skills/slice-deliver/scripts/slice.sh`,
tells you which stage the slice is at from the real state of git, GitHub and ralph, and is the
only way you start ralph. That is what makes a run repeatable: running the skill again after a
stop continues at the right stage.

## Usage

`/slice-deliver spec 5 slice 1` in Claude Code, `$slice-deliver` in Codex, or plain words. The
spec is a number (`5`, `0005`) or a path; the slice is a number from the spec's Work breakdown.
If either is missing, ask for it before doing anything.

## Ground rules

- **Status first, every time.** Begin with `slice.sh status <spec> <slice>` and act on the
  `stage=` it prints, also when you believe you know where things stand. Run it again after each
  stage. Never skip a stage and never trust an earlier turn over the script.
- **One slice per run.** Deliver the slice you were given and stop. Never start the next slice.
- **Only `slice.sh run` starts ralph.** Never call `ralph auto`, `ralph archive`, `ralph init` or
  `ralph clean` yourself: a plain `ralph auto` archives the plan of a slice that is half built.
  The script chooses between a fresh run, a resume and a continued build.
- **One ralph run per invocation.** If ralph fails or leaves work open after the run you started,
  stop and report. The developer decides whether to run the skill again.
- **A blocker stops the run.** Any `blocker=` line means stop and report it, except where a stage
  below says how to resolve it. While `stage=build-running`, ignore blockers and wait.
- **Never ready without evidence.** A pull request is marked ready only when the full gate passed
  on the current head, `pr-preflight` found nothing left to fix on the current head, and CI is
  green. The script enforces this through the stage; never record a result you did not obtain.
- **Frozen things stay frozen.** Never edit a v1 test, a `*.verified.*` snapshot or a delivered
  spec, and never weaken or skip a test, to get past a stage (`AGENTS.md`, "V1 API Tests" and
  "Specs"). A failure that would need one is a decision for the developer.
- **Safe git only.** New commits made with the `commit` skill and a plain `git push` to the
  slice's own branch. Never amend, rebase, merge, reset, force-push or skip hooks. Never stage
  `IMPLEMENTATION_PLAN.md`, `PROGRESS.md`, `PROMPT_*.md` or `.ralph/`.
- **Three GitHub writes, no others.** Create the draft pull request, edit its title and
  description, mark it ready. No comments, reviews, reviewers, labels or merges, and no message
  sent to anyone: the team message is text the developer pastes.
- **No tool or AI attribution** in commits, the pull request or the team message, and no secrets
  or personal data anywhere.
- **Stop means stop.** Report in the stop format below and end the turn. Do not look for another
  route round a failed step.

If your tool sandboxes commands, `slice.sh run`, the gate and `gh` need the escalation that
`AGENTS.md` describes under "Local Build And Test Checks".

## The script

Run it from the repository root with `bash .agents/skills/slice-deliver/scripts/slice.sh`:

| Command | What it does |
|---|---|
| `status <spec> <slice>` | Prints facts as `key=value`, then `warning=` and `blocker=` lines, then `stage=`. Changes nothing |
| `probe` | Checks that the agent ralph will use can run headless, with a one-word request |
| `run <spec> <slice>` | Starts or continues ralph for the slice, detached, logging to `.ralph/slice-deliver/ralph.log`. Refuses when a blocker stands or there is nothing to run |
| `wait [seconds]` | Returns when ralph exits or after the given time (default 540), with `ralph=running` or `ralph=exited` and `exit=<code>`, and the last log lines |
| `record <key> <value>` | Records a stage result: `gate_sha`, `preflight_sha`, `preflight_verdict`, `done`, `note` |
| `reset` | Forgets the slice in flight. Only when the developer asks to abandon a slice |

## Stages

| `stage=` | What to do |
|---|---|
| `checks` | [Check and start](#1-check-and-start) |
| `build-running` | [Wait for ralph](#2-wait-for-ralph) |
| `build-failed`, `build-incomplete` | [Continue or stop](#3-a-build-that-failed-or-left-work-open) |
| `push` | `git push -u origin <branch>`, plain. A rejected push is a stop |
| `gate` | [Run the full gate](#4-the-full-gate) |
| `pr` | [Open the draft pull request](#5-the-draft-pull-request) |
| `preflight` | [Run pr-preflight](#6-pr-preflight) |
| `preflight-blocked` | [Stop for the developer](#6-pr-preflight) |
| `ci` | [Wait for CI](#7-ci) |
| `ci-failed` | Stop: list the failing checks from `gh pr checks <pr_url>` |
| `ready` | `gh pr ready <pr_url>`, then check that the stage became `report` |
| `report` | `record done yes`, then give the [delivered report](#delivered) |
| `done` | Say the slice is already delivered and give the delivered report again from the facts |

### 1. Check and start

Nothing has changed yet at this stage, so every problem here is cheap: fix what the list below
allows and stop on the rest.

1. `git fetch origin main`, then `status`.
2. **Branch.** If you are on `main` with a clean tree, create the slice's branch from
   `origin/main` and run `status` again: for slice 1 the branch named in the spec's Notes
   (`Branch: ...`), or `<story>-<slug>` from the spec's file name when it names none; for a later
   slice the same name with `-slice-<n>`. If a local branch of that name already exists, stop:
   it is left over from earlier work and the developer removes or renames it. On any other branch
   that draws a `blocker=branch`, stop.
3. **Earlier slices.** For slice 2 or later, confirm each earlier slice is merged
   (`gh pr list -R <repo> --state merged --search "<story> in:title"`) and that the branch is not
   behind `origin/main`. If you cannot tell, ask the developer once.
4. **Warnings** do not stop the run; keep them for the report. Never rebase or merge to clear one.
5. `probe`. A failure means ralph could not start an agent: stop with the detail it prints.
6. `run <spec> <slice>`. Tell the developer in one line that ralph has started, that it can take
   an hour or more, and where the log is.

### 2. Wait for ralph

Call `wait` until it reports `ralph=exited` or `ralph=not-running`. A `ralph=running` answer is
not a failure: call it again. If your tool can run a command in the background and notify you,
run `wait 14400` that way instead of looping. Stay quiet while waiting. Then run `status`.

### 3. A build that failed or left work open

- **After a run you started in this invocation:** stop. Report the phase (`ralph_failed_phase`),
  the exit code, the plan items still open (their titles from `IMPLEMENTATION_PLAN.md`) and the
  last lines of the log. If the log shows ralph asking for a manual repair, quote that.
- **When the first `status` of an invocation shows this stage:** the developer has seen the
  failure and asked to carry on. With no blocker, call `run` once: it resumes ralph at the failed
  phase, or continues the open items. Then wait as in stage 2.

Stop without running anything, and say why, when the log shows one of the cases `AGENTS.md`
reserves for a human: a failing v1 test, a delivered spec that would need editing, or an
acceptance criterion that cannot be met without a decision.

### 4. The full gate

1. Compare the slice with what was built: `git diff --stat origin/main...HEAD`. A change under
   `tests/Api.V1.Tests` or to a `*.verified.*` file that the slice's Work or Verified by cell does
   not name is a stop.
2. Run the gate exactly as `AGENTS.md` gives it, in order: CSharpier, the build, `Api.Tests`,
   `docker compose up --build -d`, `Api.IntegrationTests`, then `Api.V1.Tests` on its own. Always
   finish with `docker compose down -v --remove-orphans`, also after a failure.
3. A CSharpier failure is the one thing fixed here: run `dotnet csharpier format .`, commit the
   result with the `commit` skill, push, and start the gate again.
4. Any other failure is a stop: name the failing tests and quote the first error. Do not change
   code or tests to make it pass.
5. On a pass, `record gate_sha <head>` with the full commit hash from `status`.

### 5. The draft pull request

Write the title and the description from the spec's slice and from the real diff
(`git diff origin/main...HEAD`), not from the plan alone.

- **Title:** `<story>: <summary>`, imperative, capitalised, no full stop, short
  (`doc/rules/git-workflow.md`).
- **Description:** `.github/PULL_REQUEST_TEMPLATE.md` with every section filled and no placeholder
  comment left. "Why" carries `Jira: <story>`, `Spec: <spec_file>` and, when the spec has more
  than one slice, "Slice n of m". "How to test" must be followable without reading the code.
  Tick a checklist line only when a stage gave you the evidence for it.
- Write the description to `.ralph/slice-deliver/pr-body.md`, then:
  `gh pr create -R <repo> --draft --base main --head <branch> --title "<title>" --body-file .ralph/slice-deliver/pr-body.md --assignee @me`
- Pass `-R <repo>` or the pull request URL to every `gh` command: `gh` cannot find the repository
  from a remote that uses an SSH host alias.

Run `status` and check `pr_state=OPEN` and `pr_draft=true`.

### 6. pr-preflight

Follow the `pr-preflight` skill in full, giving it `pr_url`. Its rule against writing to the pull
request binds that skill; the description edits below are this skill's own. When it has reported:

1. If it pushed a fix commit, it ran the gate on that commit: `record gate_sha <new head>`.
2. `record preflight_sha <head>`, then `record preflight_verdict ready` when its verdict is
   "Ready to request review" or "Fixed N finding(s) ... ready to request review", and
   `record preflight_verdict blocked` for any other verdict.
3. Update the description with `gh pr edit <pr_url> --body-file ...`: apply the corrected text it
   reported and add its "Human review focus". Do this before waiting for CI, because an edit
   starts the checks again.
4. A `blocked` verdict is a stop: the pull request stays a draft. List every
   `[NEEDS MANUAL FIX]` finding as `pr-preflight` wrote it.

At `preflight-blocked` on a later run, nothing has been committed since that verdict: report the
same findings again. Mark the verdict `ready` over open findings only when the developer says so
in this conversation, record it with `record note "<what was accepted, by whom, date>"`, and
repeat it in the report. A new commit sends the slice back through the gate and `pr-preflight`.

### 7. CI

Wait with `gh pr checks <pr_url> --watch --interval 30`, calling it again if your tool's timeout
ends it, for at most 30 minutes in all. Then run `status`. Stop, without marking anything, when
checks are still running after that time, or when none has appeared five minutes after the pull
request was opened or edited.

## Stops

Every stop is one of three kinds. Say which, so the developer knows how much has happened:

| Kind | When | What is true |
|---|---|---|
| Blocked before start | A blocker at `checks`, or a failed `probe` | Nothing has changed |
| Failed step | ralph failed or left items open, a rejected push, a red gate, red or slow CI | Earlier stages are done and recorded; the pull request, if any, is a draft |
| Needs your decision | A frozen thing would have to change, an acceptance criterion cannot be met, a `[NEEDS MANUAL FIX]` finding, a pull request over the size guide, a conflict with `main` | The same; no fix was attempted |

## What to say in chat

While working, one line when ralph starts and one line as each later stage completes. No tables,
no log dumps, no plan contents.

### Delivered

```
**Slice <n> of <m>, spec <NNNN> (<story>): ready for review**
PR: <pr_url>
- Build: ralph <phases that ran>, <n> commits, <files> files (+<adds>/-<dels>, <n> generated)
- Checks: gate passed at <short sha> · CI passed · preflight fixed <n>, none left
- Notes: <warnings, accepted findings, anything not verified; omit the line when there are none>
- Next: <"slice <n+1> once this is merged: /slice-deliver spec <NNNN> slice <n+1>" | "last slice: the spec is complete when this merges">

Message for the team:
> <PR title> is ready for review: <pr_url>
> <One sentence on what it does.> About <n> changed lines without snapshots, <files> files<, slice n of m of <spec file name>>.
> Review focus: <the two or three places pr-preflight named>.
```

### Stopped

```
**Slice <n> of <m>, spec <NNNN> (<story>): stopped at <stage> (<blocked before start | failed step | needs your decision>)**
- What happened: <one or two sentences>
- Evidence: <the blocker lines, the failing tests or checks, or at most ten log lines>
- Where it stands: <what is done and recorded: commits pushed, plan items done and open, draft PR link>
- To continue: <the one thing the developer must do>, then run `/slice-deliver spec <NNNN> slice <n>` again; it continues at <stage>.
```

Report what is true. If a stage was not reached or a check was not run, say so; never describe a
draft pull request as ready or a stopped slice as delivered.

## Anti-patterns

- Starting ralph by hand, or running it twice in one invocation to get past a failure.
- Acting on what an earlier turn found instead of running `status` again.
- Marking the pull request ready because the build finished, with the gate, `pr-preflight` or CI
  still open, or recording a gate or verdict you did not see.
- Fixing a failing test, a red gate or a `[NEEDS MANUAL FIX]` finding yourself. Formatting is the
  only fix this skill makes; `pr-preflight` makes its own under its own rules.
- Rebasing, merging `main`, amending or force-pushing to tidy the branch.
- Requesting reviewers, commenting on the pull request or sending the team message yourself.
- Starting the next slice, or building more than the slice's Work cell names.
- A long report. The developer wants the link, the state and what, if anything, they must do.

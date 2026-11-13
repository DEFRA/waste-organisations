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
  "continue slice 1 of spec 6". Asks where the slice's branch starts (main, the previous slice's
  branch, or the current branch) when that is a real choice. One slice per run; never merges,
  never requests reviewers.
---

# Slice deliver

Last reviewed 13 November 2026.

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
If either is missing, ask for it before doing anything. The request may also say where the
slice's branch starts ("from main", "on top of slice 1", "on this branch"); when it does not,
stage 1 asks.

## Ground rules

- **Status first, every time.** Begin with `git fetch origin` and `slice.sh status <spec> <slice>`,
  and act on the `stage=` it prints, also when you believe you know where things stand. Run
  `status` again after each stage. Never skip a stage and never trust an earlier turn over the
  script.
- **One slice per run.** Deliver the slice you were given and stop. Never start the next slice.
- **Only `slice.sh run` starts ralph and makes the slice's branch.** Never call `ralph auto`,
  `ralph archive`, `ralph init` or `ralph clean` yourself: a plain `ralph auto` archives the plan
  of a slice that is half built. The script chooses between a fresh run, a resume and a continued
  build. Never create or switch branches yourself either: `run` makes the branch from the base
  the developer chose and records that base.
- **The developer chooses where the branch starts.** When more than one start is valid, ask
  (stage 1). Never pick for them.
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
| `run <spec> <slice> [base]` | Starts or continues ralph for the slice, detached, logging to `.ralph/slice-deliver/ralph.log`. A new slice needs `base`: `current` to build on the branch you are on, or `main` or a branch name, from which it first makes the slice's branch. Refuses when a blocker stands or there is nothing to run |
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
| `stacked-waiting` | [Stop: the slice below is not in `main` yet](#6-a-stacked-slice-waits) |
| `preflight` | [Run pr-preflight](#7-pr-preflight) |
| `preflight-blocked` | [Stop for the developer](#7-pr-preflight) |
| `ci` | [Wait for CI](#8-ci) |
| `ci-failed` | Stop: list the failing checks from `gh pr checks <pr_url>` |
| `ready` | `gh pr ready <pr_url>`, then check that the stage became `report` |
| `report` | `record done yes`, then give the [delivered report](#delivered) |
| `done` | Say the slice is already delivered and give the delivered report again from the facts |

### 1. Check and start

Nothing has changed yet at this stage, so every problem here is cheap. Stop on any blocker other
than `blocker=branch`, which the choice of branch below may resolve.

1. `git fetch origin`, then `status`.
2. **Where the branch starts.** Work out the valid starts from `status`:

   | Start | Valid when | `base` for `run` |
   |---|---|---|
   | A new branch from `main` | `slice_branch_exists=no` | `main` |
   | A new branch from the previous slice's branch | slice 2 or later, `slice_branch_exists=no`, and `previous_branch_exists` is not `no` | the `previous_branch` name |
   | The branch you are on | there is no `blocker=branch` line | `current` |

   - If the request already says which, take it. If exactly one start is valid, say in one line
     which you are taking, unless it is the branch you are on and `status` prints a
     `warning=github` line: a pull request is already open there, so ask before adding the slice
     to it. Otherwise ask the developer and wait, with your tool's question facility if it has
     one, in plain words if not.
   - Put the start you recommend first: from `main` for slice 1 and when
     `previous_pr_state=MERGED`; from the previous slice's branch while that pull request is not
     merged.
   - Say in a few words what each start means. A new branch is named `slice_branch`, which is
     `<ticket-id>-<slug>-slice-<n>` for every slice, slice 1 included. A slice
     built on the previous slice's branch waits as a draft until that slice is merged (stage 6).
     The branch you are on is used as it is.
   - If the previous slice lives on a branch `status` did not find, the developer names it, and
     that name is the `base`.
   - No valid start is a stop: `slice_branch` already exists and you are not on a branch for this
     story, so the developer checks that branch out or removes it.
3. **Warnings** do not stop the run; keep them for the report. Never rebase or merge to clear one.
4. `probe`. A failure means ralph could not start an agent: stop with the detail it prints.
5. `run <spec> <slice> <base>`. For `main` or a branch name it first makes `slice_branch` from that
   base and switches to it; it refuses when the tree is not clean or the base does not hold the
   spec. Tell the developer in one line which branch the slice is on, that ralph has started and
   can take an hour or more, and where the log is.

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

1. Compare the slice with what was built: `git diff --stat origin/main...HEAD`, or
   `git diff --stat <base>...HEAD` for a stacked slice (`base` is a branch name). A change under
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
(`git diff origin/main...HEAD`, or `git diff <base>...HEAD` for a stacked slice), not from the
plan alone.

- **Title:** `<story>: <summary>`, imperative, capitalised, no full stop, short
  (`doc/rules/git-workflow.md`).
- **Description:** `.github/PULL_REQUEST_TEMPLATE.md` with every section filled and no placeholder
  comment left. "Why" carries `Jira: <story>`, `Spec: <spec_file>`, `Size:` with the
  non-generated changed lines against the size guide, counted as `doc/rules/git-workflow.md`
  says, and, when the spec has more than one slice, "Slice n of m". "How to test" must be
  followable without reading the code.
  Tick a checklist line only when a stage gave you the evidence for it.
- Write the description to `.ralph/slice-deliver/pr-body.md`, then:
  `gh pr create -R <repo> --draft --base main --head <branch> --title "<title>" --body-file .ralph/slice-deliver/pr-body.md --assignee @me`
- A stacked slice still targets `main`, because the checks run only for pull requests into
  `main`. Say in "Why" which pull request it is stacked on (`base_pr_url`) and that the diff
  includes that slice until it is merged.
- Pass `-R <repo>` or the pull request URL to every `gh` command: `gh` cannot find the repository
  from a remote that uses an SSH host alias.

Run `status` and check `pr_state=OPEN` and `pr_draft=true`.

### 6. A stacked slice waits

`stage=stacked-waiting` means the slice was built on another slice's branch (`base`), and either
that slice is not in `main` yet or `main` has not been merged into this branch since. The pull
request's diff still holds both slices, so it stays a draft: `pr-preflight` and a reviewer would
judge two slices as one.

This is a stop of the kind "waiting on the previous slice". Say which step comes next:

- `base_pr_state=OPEN`: the previous slice's pull request (`base_pr_url`) has to be reviewed and
  merged first. `none` or `CLOSED` means no pull request for that branch can merge: say so.
- `base_pr_state=MERGED` and `behind_main` is not `0`: the developer brings `main` into this
  branch, with the "Update branch" button on the pull request and then `git pull`. Never do that
  merge yourself.

On the next run the head has changed, so the slice goes through the gate again and then on to
`pr-preflight`.

### 7. pr-preflight

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

### 8. CI

Wait with `gh pr checks <pr_url> --watch --interval 30`, calling it again if your tool's timeout
ends it, for at most 30 minutes in all. Then run `status`. Stop, without marking anything, when
checks are still running after that time, or when none has appeared five minutes after the pull
request was opened or edited.

## Stops

Every stop is one of four kinds. Say which, so the developer knows how much has happened:

| Kind | When | What is true |
|---|---|---|
| Blocked before start | A blocker at `checks`, or a failed `probe` | Nothing has changed |
| Failed step | ralph failed or left items open, a rejected push, a red gate, red or slow CI | Earlier stages are done and recorded; the pull request, if any, is a draft |
| Needs your decision | A frozen thing would have to change, an acceptance criterion cannot be met, a `[NEEDS MANUAL FIX]` finding, a pull request over the size guide, a conflict with `main` | The same; no fix was attempted |
| Waiting on the previous slice | `stacked-waiting` | The slice is built, gated and pushed, and its pull request is a draft. Nothing failed |

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
**Slice <n> of <m>, spec <NNNN> (<story>): stopped at <stage> (<blocked before start | failed step | needs your decision | waiting on the previous slice>)**
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
- Rebasing, merging `main`, amending or force-pushing to tidy the branch, including bringing
  `main` into a stacked branch.
- Choosing where the branch starts when the developer has a real choice, or creating and
  switching branches yourself instead of through `run`.
- Running `pr-preflight` on, or marking ready, a stacked slice that is still waiting.
- Requesting reviewers, commenting on the pull request or sending the team message yourself.
- Starting the next slice, or building more than the slice's Work cell names.
- A long report. The developer wants the link, the state and what, if anything, they must do.

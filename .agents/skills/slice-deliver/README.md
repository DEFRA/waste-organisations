# slice-deliver

Takes one slice of a spec's Work breakdown from "not started" to "pull request ready for review"
in one request: it builds the slice with the ralph loop, runs the full gate, opens a draft pull
request, runs [pr-preflight](../pr-preflight/README.md), waits for CI, marks the pull request
ready and gives you a short report with a message for the team. When a step fails or needs a
decision it stops and tells you in chat; running it again continues from that step.

The agent-facing procedure is in [SKILL.md](SKILL.md); this file is for developers.

## When to use it

- A spec is written and committed, and you want slice 1 built and put up for review.
- The previous slice is merged and you want the next one.
- The previous slice is still in review and you want the next one built on top of it meanwhile.
- An earlier run stopped, you have dealt with what it reported, and you want it to carry on.

It replaces running `ralph archive`, `ralph init`, `ralph plan`, `ralph build`, opening the pull
request, `pr-preflight` and marking it ready by hand. It does not replace the human review.

## Before you start

- **ralph runs on your machine with permission prompts off.** `ralph auto` refuses to run outside
  a container; the skill passes `--force`, so the agents ralph starts can run any command as you.
  Inside a devcontainer (`ralph sandbox`) the flag is not passed.
- **ralph is configured.** `ralph.yaml` in the repository, or `~/.config/ralph/config.yaml`,
  chooses the model for each phase. `ralph auto` also runs `review` and `fix`: a file with no
  `review` section runs that phase on the backend's default model.
- **The agent ralph uses is signed in** for headless runs, for example `claude auth status` in a
  terminal. The skill checks this before it starts.
- **`gh` is signed in as an account that can push** to this repository and open pull requests.
  With two GitHub accounts run `gh auth switch --user <account>` first.
- **Docker is running and host port 5432 is free.** A local PostgreSQL on 5432 makes the
  integration tests reach the wrong server; stop it rather than changing ports (see "Postgres And
  Liquibase" in `AGENTS.md`).
- **The spec is committed and the tree is clean.** ralph refuses to plan over uncommitted changes
  in `specs/`.
- **Know where the slice's branch should start.** The skill asks when there is a real choice: a
  new branch from `main`, a new branch on top of the previous slice's branch, or the branch you
  are on. A new branch gets the name in the spec's Notes for slice 1 and the same name with
  `-slice-<n>` after that, and its base must already hold the spec.

## How to start it

| Tool        | How                                                                         |
| ----------- | --------------------------------------------------------------------------- |
| Claude Code | `/slice-deliver spec 5 slice 1`                                             |
| Codex       | `$slice-deliver` (or pick it from `/skills`), then give the spec and slice   |
| Either      | Ask in plain words: "complete slice 1 in spec 5"                             |

To skip the branch question, say it in the request: "... from main", "... on top of slice 1" or
"... on this branch". To continue after a stop, give the same request again.

## What happens

1. **Checks and branch.** Nothing is changed until the spec, the slice, `gh`, Docker, port 5432
   and the agent login are all in order. It then asks where the branch starts, if that is a real
   choice, and makes the slice's branch.
2. **Build.** `ralph auto` plans the slice, builds it one plan item per commit, reviews the result
   and fixes what the review found. It runs detached and logs to `.ralph/slice-deliver/ralph.log`;
   expect an hour or more.
3. **Gate.** CSharpier, the build, `Api.Tests`, then `Api.IntegrationTests` and `Api.V1.Tests`
   against Docker Compose, as `AGENTS.md` describes. Formatting is the only thing fixed here.
4. **Draft pull request**, titled and described from the template, the spec's slice and the diff.
5. **pr-preflight**, which fixes the mechanical findings it has confirmed and reports the rest.
   The skill then updates the pull request description with the review focus.
6. **CI**, waited for up to 30 minutes.
7. **Ready for review**, and the report.

It marks the pull request ready only when the gate passed on the current commit, `pr-preflight`
left nothing to fix on it, and CI is green.

## Building on a slice that is not merged yet

When the next slice needs the previous one and that is still in review, choose "on top of the
previous slice's branch". The skill builds the slice, runs the gate and opens a draft pull
request, then stops and waits: until the previous slice is merged, the diff holds both slices and
cannot be reviewed as one. The pull request still targets `main`, because the checks run only for
pull requests into `main`.

When the previous slice is merged, press "Update branch" on the waiting pull request, `git pull`,
and ask for the slice again. It re-runs the gate on the updated branch and carries on with
`pr-preflight`, CI and ready. The skill never merges `main` into the branch itself.

## What you get

A few lines in chat: the pull request link, what was built and checked, anything you should know,
and what comes next. Below them is a message for the team with the link, the size and the review
focus, ready to paste. The skill does not send it and does not request reviewers.

## If it stops

The report says where it stopped, why, what is already done and what you need to do. There are
four kinds of stop:

- **Blocked before start:** a check failed and nothing has changed. Fix it and ask again.
- **Failed step:** ralph failed or left plan items open, the gate or CI is red, or a push was
  rejected. Earlier steps are kept. Fix the cause and ask again; the skill resumes ralph at the
  phase that failed and never starts the slice over.
- **Needs your decision:** a v1 test or verified snapshot would have to change, an acceptance
  criterion cannot be met, `pr-preflight` reported a `[NEEDS MANUAL FIX]` finding, the pull
  request is over the size guide or conflicts with `main`. The pull request stays a draft.
- **Waiting on the previous slice:** the slice was built on top of one that is not in `main` yet.
  Nothing failed; see "Building on a slice that is not merged yet".

To see where a slice is without changing anything:

```bash
bash .agents/skills/slice-deliver/scripts/slice.sh status 5 1
```

To abandon a slice in flight, so that another can start (the plan, the commits and the branch are
left as they are):

```bash
bash .agents/skills/slice-deliver/scripts/slice.sh reset
```

## Afterwards

- Paste the message for the team where the team asks for reviews.
- Ask for the next slice: from `main` once this one is merged, or on top of this one while it is
  in review. The spec's development is complete when its last slice is merged.
- If review asks for changes, commit them and ask for the slice again: the skill re-runs the gate
  and `pr-preflight` on the new commit.

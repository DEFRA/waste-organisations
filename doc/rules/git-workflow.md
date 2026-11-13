# Git workflow: version control, commits and pull requests

Adapted for this repo from the DEFRA [version control standards](https://github.com/DEFRA/software-development-standards/blob/main/docs/standards/version_control_standards.md), [pull requests process](https://github.com/DEFRA/software-development-standards/blob/main/docs/processes/pull_requests.md) and [developer workflows guide](https://github.com/DEFRA/software-development-standards/blob/main/docs/guides/developer_workflows.md). Last synced 9 November 2026.

## Version control standards

- All code is held in the Defra GitHub organisation (`DEFRA/waste-organisations`).
- GitHub flow: `main` plus short-lived story branches. CDP supports no other workflow.
- `main` is protected by repository rulesets: two approving reviews (GitHub states this when a PR is opened), code owner review (`.github/CODEOWNERS` gives the v1 tests their own approver), the `Run Pull Request Checks` and `CDP SonarCloud Scan` status checks, resolved review threads, signed commits, no force pushes or deletion.
- Releases are tagged and versioned by the CDP build action on every merge to `main` (`.github/workflows/publish.yml`), which also publishes the Liquibase changelog as a versioned migrations artefact. **Accepted deviation:** the version number lives in the CDP tag and the `SERVICE_VERSION` environment variable, not in the source code, because CDP owns release versioning for every service it hosts.

## Pull request process

Goals: focused PRs, simple reviews, a clean commit history, and respect for reviewers' time.

1. **Always on a branch.** However small the change: `git checkout -b <ticket-id>-<slug>`, where `<ticket-id>` is the story's Jira key, whatever its project. A branch that delivers one slice of a spec ends with the slice: `<ticket-id>-<slug>-slice-<n>`. The DEFRA process uses a plain descriptive name; this repo prefixes the Jira key so the PR title and story link can be derived from it. Older `feature/<ticket-id>` branches exist; new branches use the new form. Dependabot branches are exempt. Work that is not associated with a Jira story takes no key: the branch has a short descriptive name and the PR title is the summary alone. Never invent a placeholder key.
2. **Start with an empty commit** (`git commit --allow-empty`) using this template, so the PR is visible from the start:

   ```text
   50 character limited title

   Link to originating story/bug in Jira

   Description covering why we're making this change, and briefly what the change is.
   ```

   Cover **the actual change you intend to make**, not a repeat of the backlog story; the Jira story key provides that context.

3. **Push immediately and open the PR** (`git push -u origin <ticket-id>-<slug>`). Title it `<ticket-id>: summary`, or the summary alone when the work has no Jira story, and fill in `.github/PULL_REQUEST_TEMPLATE.md`, which GitHub offers in place of the commit message; the empty commit still carries the why in the history. Assign yourself. Open it as a draft until it is ready for review, and complete the "How to test" section before you request review.
4. **Code, committing frequently** with the `commit` skill: Conventional Commits subjects and short bulleted bodies (see "Commit messages" below). Keep `WIP` noise to a minimum; the messages are visible in the PR.
5. **Keep your branch up to date** with `git rebase origin/main` (preferred over merge, to keep the PR history clean). Never rebase or force-push a branch someone else is working on.
6. **Keep the PR small.** One spec slice or one plan item per PR. A PR that mixes a schema change, an endpoint and a refactor is three PRs. Size guide: at most about 800 non-generated changed lines (generated means `*.verified.*` snapshots and lock files); above that, split by spec slice before asking for review. The `pr-review` and `pr-preflight` skills flag a PR over the guide with a suggested split point. The figure was proposed in November 2026 and the team can revise it here.
7. **Get it reviewed.** At least one other developer must review and approve before merge. A v1 test or snapshot change also needs the v1 code owner's approval. AI review (the `pr-preflight` and `pr-review` skills) prepares for the human review and never replaces or approves it.
8. **Squash and merge**, then **delete the branch**. The squash commit is what `main` records: its subject is the PR title and its body is the PR description, never the list of every branch commit message, which would also carry any co-author trailers onto `main`. With one commit in the PR, GitHub uses that commit's subject, so give it the `<ticket-id>: ` prefix too (see "Commits And Pull Requests" in `AGENTS.md`).

## Pull request description

The description follows `.github/PULL_REQUEST_TEMPLATE.md`:

- **Why**: why the change is needed and briefly what it does, with the Jira story key (a repository autolink turns it into a link), or `Jira: none` with the reason when the work has no story, and the spec file. Not a copy of the story.
- **What changed**: one bullet per behaviour change, not per file.
- **How to test**: everything QA or another developer needs to verify the change without reading the code: environment, preconditions (changelog applied, data, configuration), which client credentials to use, numbered requests with the `Accept` header version and the expected status and body, the negative cases, how to confirm the stored data, the automated tests and spec scenarios that cover the change, and what still needs a human. Required for any behaviour, schema or configuration change; "No behaviour change: <reason>" is acceptable for a docs-only or tooling PR.
- **Checklist**: the gate, tests and coverage, no PII or secrets, docs updated, Dependabot groups updated.

This is how the repo meets the DEFRA quality assurance standard's expectation that each story's testing is documented alongside the story. The `pr-review` and `pr-preflight` skills check that "How to test" is present, matches the diff and could be followed without reading the code.

## Commit messages

**Branch commits** follow [Conventional Commits](https://www.conventionalcommits.org/): `<type>(<scope>): <subject>` with an imperative subject of at most 50 characters and an optional body of at most three short bullets. The `commit` skill in `.agents/skills/commit` produces this format. Branch commits never reach `main`, so they carry no Jira key, and the DEFRA process asks only that interim messages are not ignored. No tool signatures or co-author trailers.

**The PR title and the squash commit** are what `main` records, so the DEFRA commit message rules apply to them, and to the empty commit that opens the PR:

1. Separate subject from body with a blank line
2. Limit the subject line to 50 characters
3. Capitalise the subject line
4. Do not end the subject line with a period
5. Use the imperative mood in the subject line (_Add ability_, not _Added ability_)
6. Use the body to explain _what and why_, not _how_, and name the Jira story key

Source: [How to write a Git commit message](https://chris.beams.io/posts/git-commit/), as adopted by the DEFRA pull requests process.

## Ralph loop commits

- The build loop commits with the `commit` skill after the full gate passes, one plan item per commit or per small set of atomic commits.
- Loop artefacts (`IMPLEMENTATION_PLAN.md`, `PROGRESS.md`, `PROMPT_*.md`, `.ralph/`) are gitignored and never staged.
- Merging to `main` is always a human's squash merge after review; the loop pushes to the story branch only.

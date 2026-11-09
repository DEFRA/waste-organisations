---
name: commit
description: >
  Commit finished work in this repository as one or more atomic commits with messages in the
  DEFRA style (capitalised imperative subject of at most 50 characters, blank line, body saying
  what and why with the Jira link). Use whenever asked to commit, save, or check in changes, and
  at the end of a Ralph build iteration. Stages explicit paths only and never commits the loop
  artefacts.
allowed-tools: Bash
---

You are committing changes in this repository. Follow the DEFRA pull request process as
recorded in `doc/rules/git-workflow.md`. Use plain `git` commands.

## Commit message format

```
Capitalised imperative subject, 50 characters max

Jira: REGEN-NNN

Why this change is needed and, briefly, what it changes. Wrap at 72 characters.
```

Rules for the message:

1. Separate the subject from the body with a blank line.
2. Limit the subject to 50 characters.
3. Capitalise the subject.
4. Do not end the subject with a period.
5. Use the imperative mood (_Add ability_, not _Added ability_).
6. Use the body to explain _what and why_, not _how_. Name the Jira story key when one exists (a repository autolink makes it clickable); take the key from the branch name (`REGEN-141-create-an-organisation` or `feature/REGEN-141` gives `REGEN-141`). On a branch with no story key, omit it rather than invent one.
7. A single-commit PR is squash-merged under its commit subject, so when the branch will have only this commit, prefix the subject with the story key: `REGEN-141: Create an organisation`.

## Procedure

1. **Analyse the changes**: `git status` and `git diff` before composing anything.
2. **Atomic commits**: each commit captures one logical change. If the working tree holds separable concerns (a refactor _and_ the feature it enables, or two unrelated fixes), produce one commit per concern rather than a grab-bag commit.
3. **Stage selectively**: `git add -- <paths>` with explicit paths. Never `git add -A` or `git add .` when the diff spans more than one concern.
4. **Never stage loop or local artefacts**: `IMPLEMENTATION_PLAN.md`, `PROGRESS.md`, `PROMPT_*.md`, `.ralph/`, `*.received.*`. They are gitignored; if one shows as tracked, stop and ask.
5. **Check recent commits** (`git log --oneline -10`) to stay consistent with the branch.
6. **Verify first**: do not commit a change that has not passed the gate in `AGENTS.md` ("Local Build And Test Checks"). If a v1 test is red, stop; never commit a v1 test or snapshot change to get green.
7. **Commit** with a heredoc so the subject, blank line and body keep their newlines:

   ```bash
   git commit -m "$(cat <<'MSG'
   Add organisation type seed changeset

   Jira: REGEN-141

   The v2 POST needs the 40 organisation type codes present before the
   endpoint can map a request; this adds them as Liquibase 1.2 so every
   environment seeds them the same way.
   MSG
   )"
   ```

8. **Commit immediately** once the message is composed: the user invoked this skill because they want a commit. Respect hooks (never `--no-verify`), never amend or rewrite a pushed commit, never create an empty commit except the PR-opening one described in `doc/rules/git-workflow.md`.

## Quality standards

- **Always include** footer: `Co-Authored-By: Claude <noreply@anthropic.com>`
- **Never include** "Generated with Claude Code" signatures
- One logical change per commit; split larger diffs into several atomic commits in a single invocation.
- Body in sentences, two to five lines; no bullet dumps of file names, which the diff already shows.
- Match the branch's style, not its content: do not copy earlier messages.

If the intent or scope of the changes is unclear, ask specific questions before committing.

# waste-organisations

.NET 10 backend API (ASP.NET Core minimal APIs) for organisation data and their Extended Producer Responsibility (EPR) registrations, hosted on Defra's Core Delivery Platform (CDP). Data lives in Postgres (EF Core over a Liquibase-managed schema) and MongoDB. All work in this repo must follow the [DEFRA software development standards](https://github.com/DEFRA/software-development-standards).

## Detailed standards

The rules below are the always-applicable summary. The full standards, tailored to this repo, live in `doc/rules/`. **Read the relevant file before starting the matching kind of task**:

- [csharp-dotnet.md](doc/rules/csharp-dotnet.md): writing any C#; endpoints, services, DTOs, validation, data access, API versioning; adding NuGet packages
- [common-coding.md](doc/rules/common-coding.md): readability, naming, comments, simplicity; coverage rules
- [git-workflow.md](doc/rules/git-workflow.md): branches, commit messages, PR titles, the full PR process
- [security-logging.md](doc/rules/security-logging.md): secure coding, secrets, GitHub Advanced Security, Serilog/ECS logging and PII rules
- [quality-assurance.md](doc/rules/quality-assurance.md): acceptance criteria, the three test projects, snapshots, coverage
- [continuous-integration.md](doc/rules/continuous-integration.md): workflow changes, action SHA-pinning, Trivy, Sonar
- [containers.md](doc/rules/containers.md): Dockerfile, compose.yml and image changes

## Reusable skills

Agent-invocable procedures live in `.agents/skills/` in the open Agent Skills format (`SKILL.md`). Codex reads that directory natively; Claude Code reads the same skills through the `.claude/skills/` symlinks. When a task matches one of these skills, follow the skill rather than improvising:

- `spec-clarify`: turn a Jira story, bug or idea into a spec in `specs/`, in the repo's format, by asking the author questions; the first step of every story.
- `spec-breakdown`: split a spec that needs more than one pull request into child specs, one per slice, with dependencies and order.
- `commit`: stage and commit finished work as atomic commits with Conventional Commits messages.
- `pr-review`: review a colleague's PR against the linked Jira story and these standards. Findings are reported for a human to act on; the AI never approves, requests changes, or comments.
- `pr-preflight`: pre-flight check of your own PR before requesting human review; may fix mechanical, in-spec blocking findings (new commit, plain push, full gate first).

Both review skills enforce scope discipline: AI-suggested changes must be in spec (the linked story's acceptance criteria plus these standards on the lines the PR touches). Valuable out-of-scope findings are raised as suggested new Jira stories, never as changes or change requests on the PR.

## Language and framework

- C# on .NET 10, the current LTS. This is a backend API only: no Razor, Blazor, MVC views or any other .NET UI.
- ASP.NET Core minimal APIs. Each endpoint is a static class under `src/Api/Endpoints/<Resource>/` with a `Map...` extension method registered in `Endpoints.cs`. Handlers stay thin: validate, call a service in `src/Api/Services`, return a `TypedResults` value.
- Nullable reference types and implicit usings stay enabled. Records for DTOs. `async`/`await` with a `CancellationToken` throughout; never `.Result` or `.Wait()`.
- Postgres is accessed through EF Core LINQ on `PostgresDbContext`, MongoDB through the typed driver API. Never build SQL or Mongo filters from strings.
- The Postgres schema is the Liquibase changelog only (see "Postgres And Liquibase" below and the README): a new changeset file, never an EF Core migration, never an edit to a published changeset.
- API versions are selected by the `Accept` header (README, "API versioning"). v1 behaviour is frozen; new or changed behaviour goes in endpoints that declare `.WithApiVersions(...)`.

## Code style

- Formatting is CSharpier (`dotnet csharpier check .`, which also covers `changelog/*.xml`); do not hand-format. Conventions are in `.editorconfig`.
- Warnings are errors and SonarAnalyzer runs on every build. Do not add `#pragma warning disable`, `<NoWarn>` or `[SuppressMessage]` without a comment saying why, and never to get a build green.
- Intent-revealing names; no generic names (`temp`, `data2`, `x`); spell out a domain acronym (EPR, ACL, CDP) where it is first used. Prefer small, named methods over comments; comments explain _why_, never _how_. Never leave commented-out code. No `TODO` without a Jira reference.
- Simplicity first: no speculative abstraction or premature reuse. Apply the rule of three before extracting shared code.

## Dependencies

- NuGet only, with exact versions in the `.csproj`. Vet a new package before adding it (maintained, widely used, OGL-compatible licence, security history) and say why it was chosen in the PR.
- Adding or moving a package means checking the Dependabot groups (see "Updating Dependabot" below).
- A `NuGetAuditSuppress` entry needs the advisory link and the reason in a comment, as the existing one has.

## Testing and quality

- Every change ships with tests. Unit coverage reported to SonarCloud must stay at or above 90% and must never decrease. `[ExcludeFromCodeCoverage]` only on bootstrap wiring, never on business logic.
- Negative cases matter as much as positive ones: validation failures, `401`/`403`, `404`/`405`/`406`, concurrency conflicts, error paths.
- Run the full gate before proposing a change: CSharpier, the build, `Api.Tests`, then `Api.IntegrationTests` and `Api.V1.Tests` against Docker Compose, in that order (commands in "Local Build And Test Checks" and "V1 API Tests" below).
- Never weaken, delete or skip a test or an assertion to make a build pass. v1 tests and every `*.verified.*` snapshot are frozen (see "V1 API Tests"). Review a new `*.received.*` file before accepting it as `*.verified.*`.

## Security

- Follow the OWASP Secure Coding Practices quick reference guide (version 2). Validate all input at the boundary and return problem details, never stack traces or connection strings.
- Never log PII or secrets: no names, addresses, emails, phone numbers, National Insurance numbers, usernames, passwords, API keys or tokens in log output. Use the configured Serilog logger (ECS format) with message templates; never `Console.WriteLine`.
- Secrets come from CDP environment variables. Development-only credentials live in `appsettings.Development.json` and `compose.yml`; `appsettings.json` holds placeholders only. If a secret ever reaches git history, treat it as compromised and follow Defra's credential exposure process.
- In GitHub Actions, pin third-party actions to a full commit SHA with the version as a trailing comment, never a tag or branch. Pin container images by digest.

## Git workflow

- All work happens on a branch named `REGEN-NNN-short-slug` (Dependabot branches and work with no Jira story are exempt); never commit directly to `main`. Main is always shippable.
- Branch commits follow Conventional Commits (`type(scope): subject`, imperative, at most 50 characters, optional body of up to three bullets), made with the `commit` skill, with no tool signatures or co-author trailers. They never reach `main`.
- PRs are small and focused on one change, titled `REGEN-NNN: summary` (no prefix when the work has no Jira story), described with `.github/PULL_REQUEST_TEMPLATE.md` including its "How to test" section, squash-merged with the PR title and description as the commit message, and the branch deleted after merge (see "Commits And Pull Requests" below).
- A human developer reviews and approves every PR. AI review supplements that review and never approves.

## AI-assisted development

- Read this file and the matching `doc/rules/` file before changing anything. If a request conflicts with these rules, say so and stop; never proceed silently or work around a rule.
- Ralph loop: one plan item per iteration. Never stage or commit `IMPLEMENTATION_PLAN.md`, `PROGRESS.md`, `PROMPT_*.md` or `.ralph/`. Stop and report, instead of forcing a fix, when a v1 test fails, when a delivered spec would need editing, or when an acceptance criterion cannot be met without a decision.
- Never put secrets, credentials or personal data into prompts, specs, tickets or test fixtures. Test data is synthetic.
- Reference files, specs and standards by path or link; do not paste them into prompts or rules.

## Updating Dependabot

The Dependabot configuration lives in `.github/dependabot.yml`. Keep dependency PRs grouped enough to reduce noise, but not so broadly that one risky update is hidden inside an unrelated batch.

When changing the NuGet update configuration:

- Keep NuGet checks on a daily schedule unless the user explicitly asks to change cadence.
- Keep `open-pull-requests-limit` at `10` unless the user explicitly asks to change the limit.
- Do not add one broad group for all minor and patch updates. That can make it harder to see which package family broke the build.
- Keep major updates out of routine groups by using `update-types` with only `minor` and `patch`, unless the user explicitly asks for major updates to be grouped.
- Group NuGet packages by related dependency families, such as test support, ASP.NET runtime, API documentation, data access, and observability.
- Keep test-only and test-support packages separate from functional/runtime package updates.
- Remember that Dependabot NuGet groups are matched by dependency name patterns, not by the project file path where a package is referenced.
- Put more specific groups before broader groups because Dependabot uses the first matching group.
- If a package matches a broad pattern but belongs elsewhere, use `exclude-patterns` on the broad group and add the package to the more specific group.
- Let unmatched packages remain as individual PRs unless there is a clear dependency family for them.

When refreshing Dependabot NuGet groups from the current dependencies, check package and tool references in:

- Project files such as `src/**/*.csproj` and `tests/**/*.csproj`.
- Shared MSBuild props such as `Directory.Build.props`.
- .NET tool manifests such as `.config/dotnet-tools.json`.

When adding or moving a NuGet package reference, check whether `.github/dependabot.yml` should also be updated:

- Test framework, assertion, mocking, coverage, fixture, test SDK, and test host packages usually belong in `nuget-test-support`.
- ASP.NET runtime packages usually belong in `nuget-aspnetcore-runtime`, unless they are specifically test or documentation packages.
- Swagger/OpenAPI documentation packages usually belong in `nuget-api-documentation`.
- MongoDB, compression, and persistence-adjacent packages usually belong in `nuget-data-access`.
- PostgreSQL packages also belong in `nuget-data-access`: `Npgsql*` (which covers `Npgsql.EntityFrameworkCore.PostgreSQL`), `Microsoft.EntityFrameworkCore*`, and `AspNetCore.HealthChecks.NpgSql`.
- Logging, metrics, tracing, and telemetry packages usually belong in `nuget-observability`.
- Code analysis and formatting tools usually belong in `nuget-code-analysis`.

GitHub Actions updates can stay grouped together weekly unless the user asks for finer separation.

Dependabot does not see the Liquibase image: it is the `FROM` line of the `dockerfile_inline` in the `liquibase` service in `compose.yml`, not an `image:` entry. Bump its tag and digest by hand, resolving the digest (for example with `docker buildx imagetools inspect liquibase/liquibase:<tag>`) rather than writing it from memory. Do not add a `liquibase` entry to the `docker-compose` ignore list: it would match nothing.

Before finishing a Dependabot change:

- Parse `.github/dependabot.yml` to catch YAML errors.
- Review the diff and make sure unrelated scheduling or PR limit changes have not slipped in.
- If changing Dependabot syntax rather than package patterns, check the current GitHub Dependabot options documentation.

## Local Build And Test Checks

In the sandbox environment, avoid plain `dotnet build` because it can hang or take significantly longer due to workload notification/build-server delays.

NuGet restore will not run successfully in the sandbox without elevated network access. When a restore is required, request escalation instead of repeatedly retrying it in the sandbox.

After dependencies are restored, prefer this build command:

```bash
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build waste-organisations.slnx --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal
```

If a build is unexpectedly slow, stop it, run `dotnet build-server shutdown`, and retry the sandbox build command above.

Run Api.Tests with:

```bash
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build tests/Api.Tests/Api.Tests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test --test-modules tests/Api.Tests/bin/Debug/net10.0/Api.Tests.dll --no-build -v:minimal
```

In the sandbox environment, Api.Tests may need escalation because the test application can bind local sockets.

For integration tests, run the local environment first:

```bash
docker compose up --build -d
```

Then run Api.IntegrationTests with:

```bash
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build tests/Api.IntegrationTests/Api.IntegrationTests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test --test-modules tests/Api.IntegrationTests/bin/Debug/net10.0/Api.IntegrationTests.dll --no-build -v:minimal
```

Stop the local environment afterwards:

```bash
docker compose down -v --remove-orphans
```

In the sandbox environment, Api.IntegrationTests need escalation because the test application accesses Docker Compose services.

## Postgres And Liquibase

The Docker Compose stack includes Postgres and Liquibase as well as Mongo and the API. `liquibase` is a one-shot service: it applies the changelog in `changelog/` to Postgres and exits, and `api` waits for it to complete successfully. An exited `liquibase` container after `docker compose up --build -d` is therefore expected, not a failure.

Api.IntegrationTests connect from the host to the Compose Postgres on `127.0.0.1:5432`, so host port 5432 must be free before the stack starts. Do not change the host port in `compose.yml` or in the tests to get round a local Postgres that holds it: stop and ask.

The Postgres schema is managed by the Liquibase changelog, not by EF Core migrations. See the "Postgres schema changes" section of `README.md` for how to add a changelog file.

`dotnet csharpier check .` at the repository root also checks `changelog/*.xml`. CI does not: the check runs inside the Docker build, and the `Dockerfile` does not copy `changelog/`. Run the check locally after editing a changelog file.

## V1 API Tests

`tests/Api.V1.Tests` is a black-box test project that pins the behaviour of the existing (v1) API. It runs against the Docker Compose stack, like the integration tests. See `specs/0001-REGEN-145-test-scenarios-for-existing-v1-api.md`.

Never edit or delete a v1 test or snapshot (`*.verified.*`) to make a build pass. A failing v1 test means the API's behaviour changed: stop and ask.

With the local environment running (`docker compose up --build -d`), run Api.V1.Tests with:

```bash
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build tests/Api.V1.Tests/Api.V1.Tests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test --test-modules tests/Api.V1.Tests/bin/Debug/net10.0/Api.V1.Tests.dll --no-build -v:minimal
```

Run Api.V1.Tests after Api.IntegrationTests, never in the same `dotnet test` call or at the same time: the integration tests delete every document in the `Organisation` collection before each test, which would remove data a v1 test has just created.

In the sandbox environment, Api.V1.Tests need escalation because the tests call the API running in Docker Compose.

## Specs

Specs live in `specs/` and are named `NNNN-TICKET-ID-slug.md`. A spec whose development is done (its story is delivered and the changes are merged, for example specs 0001 to 0004) is a record of what was built and why. Never edit such a spec, not even its Open Questions or Decisions, while doing new development or writing a new spec. When new work changes something an earlier spec decided, such as a column or a rule it introduced, record the change and its reason in the new spec, name the earlier spec it supersedes and in what, and leave the earlier spec as it is. Edit a spec only while its own story is still in progress.

A new spec is written with the `spec-clarify` skill, so it has the sections, the sources and the positive and negative acceptance criteria the team expects. A spec whose work would exceed the pull request size guide in `doc/rules/git-workflow.md`, or that mixes a schema change with behaviour, is split with the `spec-breakdown` skill into child specs before development starts, one pull request each. Each skill has a README for developers in its folder under `.agents/skills/`.

## Commits And Pull Requests

Branches are named after the Jira story: the key followed by a short slug, for example `REGEN-141-create-an-organisation`. Older branches use `feature/REGEN-141`; the story number is the `REGEN-NNN` part either way. Dependabot branches are exempt. Work that is not associated with a Jira story takes no key: the branch has a short descriptive name and the PR title is the summary alone. Never invent a placeholder key.

Start every pull request title with the Jira story number, a colon and a short summary, for example `REGEN-141: Create an organisation through v2 POST /organisations`. Take the story number from the branch name; if the branch has none, ask rather than guess. Dependabot pull requests and work with no Jira story take no prefix. The description follows `.github/PULL_REQUEST_TEMPLATE.md`; its "How to test" section is required for any behaviour, schema or configuration change, so QA or another developer can verify the change without reading the code.

Commits on a feature branch keep their conventional-commit subjects (`feat(create): ...`), since they do not reach `main` on their own; the `commit` skill produces them. The squash commit is what `main` records: use the pull request title as its subject and the description as its body, never the list of branch commit messages. See `doc/rules/git-workflow.md` for the full process.

The repository only allows squash merges, so each pull request becomes one commit on `main`. GitHub names it after the pull request title when the pull request has several commits, but after the commit subject when it has only one: give a single commit the same `REGEN-NNN: ` prefix.

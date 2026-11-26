# Constraints of waste-organisations

Rules that exist only in this repo, and its accepted deviations from the shared and stack rules of the [Manage Account AI toolkit](https://github.com/DEFRA/waste-manage-account-ai-toolkit). [AGENTS.md](../AGENTS.md) sends an AI coding tool here before it changes code, tests, a workflow, the Dockerfile or the local stack. Where a line here disagrees with a toolkit rule, this file wins.

## What does not apply

- This repo is a backend API with no user interface. The DEFRA criteria about screens, the GOV.UK Design System, browsers and WCAG apply to the frontends that call it, not here.

## Code

- v1 is the permanent default API version and its behaviour is frozen by `tests/Api.V1.Tests`. New or changed behaviour is an endpoint that declares `.WithApiVersions(2)`, or the next version. The mechanism is in the README section "API versioning" and spec 0004.
- v2 requests use nullable members with `[Required]` (spec 0005, Decisions). v1 DTOs use the C# `required` modifier and are frozen, as are v1 response names.
- `OrganisationResponse` is the create's item, not yet the general one. Leave it and `OrganisationDetailsResponse` as they are until the next v2 endpoint that returns an organisation shows which shape is the general one; that endpoint's spec decides any rename.
- Each endpoint is a static class under `src/Api/Endpoints/<Resource>/` with a `Map...` extension method registered in `Endpoints.cs`; services live in `src/Api/Services`.
- MongoDB indexes are declared in `MongoIndexService`.
- A comment that records a decision points at the spec that made it, as the existing code does (`spec 0005, Decisions`).

## Tests

- `Api.V1.Tests` has no project reference to `src/Api` or `tests/Testing` and must not gain one: production types would let a v2 change alter what the tests send and expect.
- v1 tests are named `Action_WhenCondition_ReturnsOutcome` and carry one `[Trait("Scenario", "ID")]` per spec 0001 scenario. Integration tests carry `[Trait("Category", "IntegrationTests")]`.
- The Mongo integration tests clear the `Organisation` collection before each test (`MongoTestBase`).
- The Postgres integration tests empty `organisation_address`, `organisation` and `address` once at the start of each run; within a run each test still creates its own distinct data.
- Snapshots live in `Snapshots/<TestClass>/` beside the test class and are committed as `*.verified.*`; `*.received.*` files are gitignored. Review a new `*.received.*` file before accepting it as `*.verified.*`.
- Coverage comes from `Api.Tests` only (currently about 98%). Integration and v1 tests do not count towards the Sonar gate, so business logic needs unit tests even when an integration test also covers it.

## Security, configuration and logging

- Authentication is the Basic API-key and JWT handlers in `src/Api/Authentication`; authorisation is the `read` and `write` scope policies declared on each endpoint. The only endpoints without `RequireAuthorization` are health, OpenAPI and ReDoc.
- HSTS is applied unconditionally (`UseHstsUnconditionally`). Keep it; CDP terminates TLS in front of the service.
- `DetailedErrors` is on only in `appsettings.Development.json`.
- Per-environment configuration and credentials are environment variables set by CDP (`Mongo__*`, `Postgres__*`, `Acl__*`, `Serilog__*`). Postgres in deployed environments uses IAM authentication (`UseIamAuthentication: true`), not a password.
- `appsettings.json` holds `set-automatically-when-deployed` placeholders only. Development-only values (`developer-pwd`, `postgres`/`postgres`, the LocalStack `test` keys in `compose/aws.env`) live in `appsettings.Development.json`, `compose.yml` and `compose/`, and must never be reused anywhere else.
- Logging is configured in `appsettings*.json` and `src/Api/Utils/Logging`; health-check request noise is already filtered out in `ConfigureLogging`. Request metrics are set up in `src/Api/Utils/Metrics`.
- `appsettings.json` sets the log level to `Information`, which CDP environments inherit. Aligning deployed environments with the DEFRA baseline is a configuration change on CDP, tracked as its own story, not something a code change does in passing.

## Continuous integration

- The pull request check is `.github/workflows/check-pull-request.yml`: `Api.Tests` on the runner; `docker compose up -d`, then `Api.IntegrationTests` and `Api.V1.Tests` against the stack, in that order; the Docker image build, which runs `dotnet csharpier check .`, `dotnet build --warnaserror` and `Api.Tests` again inside the build stage; Trivy against the built image, ignoring unfixed findings; and the reusable `sonarcloud.yml` workflow, which reports `Api.Tests` coverage to SonarQube Cloud.
- `publish.yml` and `publish-hotfix.yml` run the unit and integration tests; the v1 tests run on pull requests only. A test step changed in one workflow is considered for the other two, and a deliberate difference is recorded here. `publish.yml` also publishes the Liquibase changelog as a versioned migrations artefact.
- `main` is protected by repository rulesets: the `Run Pull Request Checks` and `CDP SonarCloud Scan / CDP SonarCloud coverage scan` status checks, two approving reviews, code owner review (`.github/CODEOWNERS` gives the v1 tests their own approver), resolved review threads, signed commits, no force pushes or deletion. The repository allows only squash merges.
- Workflows declare the least `permissions` at the top level and widen per job: `contents: write` and `id-token: write` only on the publish job. Secrets are passed with `secrets: inherit` to the reusable Sonar workflow only.
- SonarQube Cloud project `DEFRA_waste-organisations` runs on every pull request and on `main` through `sonarcloud.yml`, analysed with `dotnet-sonarscanner`. The Defra quality gate must pass: new-code coverage of at least 90%, no new blocker, critical or major issues, no new duplication above 3%, every new security hotspot reviewed. Sonar does not run for Dependabot pull requests.
- Dependabot runs daily for NuGet and weekly for GitHub Actions, Docker and Docker Compose (see "Updating Dependabot"). The Trivy image in the workflow is pinned by tag and digest, like every `FROM` line.

## Containers and the local stack

- Accepted deviation, recorded 9 November 2026: base images are Microsoft's `mcr.microsoft.com/dotnet/sdk` and `mcr.microsoft.com/dotnet/aspnet`, pinned by digest, not the Defra parent images. This follows the [CDP .NET backend template](https://github.com/DEFRA/cdp-dotnet-backend-template), and the platform health check expects the resulting layout. Moving to the Defra parent images is a decision to take with CDP, not a change to make in passing.
- Accepted deviation, recorded 9 November 2026: the final stage has no `USER` instruction, so the process runs as root inside the container; CDP runs it on ECS with the platform's task-level controls. Adding `USER $APP_UID` is a candidate change to raise with CDP; do not make it in passing, because the platform health check and log shipping would need verifying.
- Record any further deviation here with the date and the reason.
- `compose.yml` is the source of truth for the services of the local stack; read it rather than rely on a list here. Its images are pinned by tag and digest.
- Host ports in `compose.yml` are fixed because the tests depend on them. Do not change them to work round a local conflict; stop the conflicting service instead.

## Git and pull requests

- This repo is public, `DEFRA/waste-organisations`. Older branches use `feature/<ticket-id>`; new branches use the toolkit's form.
- A repository autolink turns a Jira story key in a pull request into a link.
- "How to test" for this API also says which client credentials to use, the `Accept` header version of each request with the expected status and body, whether the changelog must be applied, and how to confirm the stored data.
- The pull request checklist also asks that the Dependabot groups are updated.
- A v1 test or snapshot change needs the v1 code owner's approval.

## Local procedures

### Updating Dependabot

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

### Local Build And Test Checks

The gate is the Repo profile's `Gate commands`, run in that order. These notes apply to it:

- In the sandbox environment, avoid plain `dotnet build`: it can hang or take significantly longer due to workload notification and build-server delays. Use the build command in the profile.
- NuGet restore will not run successfully in the sandbox without elevated network access. When a restore is required, request escalation instead of repeatedly retrying it in the sandbox.
- If a build is unexpectedly slow, stop it, run `dotnet build-server shutdown`, and retry.
- In the sandbox environment, `Api.Tests` may need escalation because the test application can bind local sockets. `Api.IntegrationTests` and `Api.V1.Tests` need escalation because they reach the Docker Compose services.
- `ReDocPageTests`, in `Api.IntegrationTests`, also needs Google Chrome installed (see the README, "Tests").
- Always stop the local environment afterwards with the gate's last command, also when a test step fails.

### Postgres And Liquibase

The Docker Compose stack includes Postgres and Liquibase as well as Mongo and the API. `liquibase` is a one-shot service: it applies the changelog in `changelog/` to Postgres and exits, and `api` waits for it to complete successfully. An exited `liquibase` container after `docker compose up --build -d` is therefore expected, not a failure.

Api.IntegrationTests connect from the host to the Compose Postgres on `127.0.0.1:5432`, so host port 5432 must be free before the stack starts. Do not change the host port in `compose.yml` or in the tests to get round a local Postgres that holds it: stop and ask.

The Postgres schema is managed by the Liquibase changelog, not by EF Core migrations. A schema change is a new `db.changelog-1.N.xml` included at the end of `db.changelog.xml`, never an EF Core migration and never an edit to a published changeset. Entities follow the changelog, never the other way round. See the "Postgres schema changes" section of `README.md` for how to add a changelog file.

`dotnet csharpier check .` at the repository root also checks `changelog/*.xml`. CI does not: the check runs inside the Docker build, and the `Dockerfile` does not copy `changelog/`. Run the check locally after editing a changelog file.

The Liquibase image sits inside a `dockerfile_inline` in `compose.yml`, so Dependabot cannot see it (see "Updating Dependabot").

### V1 API Tests

`tests/Api.V1.Tests` is a black-box test project that pins the behaviour of the existing (v1) API. It runs against the Docker Compose stack, like the integration tests. See `specs/0001-REGEN-145-test-scenarios-for-existing-v1-api.md`.

Never edit or delete a v1 test or snapshot (`*.verified.*`) to make a build pass. A failing v1 test means the API's behaviour changed: stop and ask.

Run Api.V1.Tests after Api.IntegrationTests, never in the same `dotnet test` call or at the same time: the integration tests delete every document in the `Organisation` collection before each test, which would remove data a v1 test has just created.

### Specs

Specs live in `specs/` and are named `NNNN-<ticket-id>-slug.md`. A spec whose development is done (its story is delivered and the changes are merged, for example specs 0001 to 0004) is a record of what was built and why. Never edit such a spec, not even its Open Questions or Decisions, while doing new development or writing a new spec. When new work changes something an earlier spec decided, such as a column or a rule it introduced, record the change and its reason in the new spec, name the earlier spec it supersedes and in what, and leave the earlier spec as it is. Edit a spec only while its own story is still in progress.

A new spec is written with the `spec-clarify` skill, so it has the sections, the sources and the positive and negative acceptance criteria the team expects. A spec whose work would exceed the pull request size guide in the toolkit's `rules/shared/git-workflow.md`, or that mixes a schema change with behaviour, stays one spec: `spec-clarify` slices it in the spec's "Work breakdown" table before development starts, one pull request per slice, built in order. Every spec has that table, with a single slice when the work is one pull request. Each skill has a README for developers in its folder under `skills/` in the toolkit.

# Agent Guidance

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

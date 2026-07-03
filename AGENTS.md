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

When adding or moving a NuGet package reference, check whether `.github/dependabot.yml` should also be updated:

- Test framework, assertion, mocking, coverage, fixture, test SDK, and test host packages usually belong in `nuget-test-support`.
- ASP.NET runtime packages usually belong in `nuget-aspnetcore-runtime`, unless they are specifically test or documentation packages.
- Swagger/OpenAPI documentation packages usually belong in `nuget-api-documentation`.
- MongoDB, compression, and persistence-adjacent packages usually belong in `nuget-data-access`.
- Logging, metrics, tracing, and telemetry packages usually belong in `nuget-observability`.

GitHub Actions updates can stay grouped together weekly unless the user asks for finer separation.

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
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal
```

In the sandbox environment, Api.Tests may need escalation because VSTest binds a local socket for test host communication.

For integration tests, run the local environment first:

```bash
docker compose up --build -d
```

Then run Api.IntegrationTests with:

```bash
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test tests/Api.IntegrationTests/Api.IntegrationTests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal
```

Stop the local environment afterwards:

```bash
docker compose down -v --remove-orphans
```

In the sandbox environment, Api.IntegrationTests need escalation because VSTest binds a local socket and the tests access Docker Compose services.

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
- Logging, metrics, tracing, and telemetry packages usually belong in `nuget-observability`.
- Code analysis and formatting tools usually belong in `nuget-code-analysis`.

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

## Waste Obligations journey tests

The shared suite lives in
[DEFRA/waste-obligations-journey-tests](https://github.com/DEFRA/waste-obligations-journey-tests).
Read its [run instructions](https://github.com/DEFRA/waste-obligations-journey-tests/blob/main/README.md)
and [agent guidance](https://github.com/DEFRA/waste-obligations-journey-tests/blob/main/AGENTS.md)
when changing behavior used by the journey.

The backend, Waste Obligations frontend and packaging proxy PR workflows use
the [shared action](https://github.com/DEFRA/waste-obligations-journey-tests/blob/main/run-journey-tests/action.yml)
to run E2E, accessibility and passive security profiles against a CDP-only
Docker stack. Browser traffic enters through the packaging proxy; Azure
application navigation is omitted, but the remaining scenario must run.
Azure AD B2C login is still required. After CDP service deployment to dev,
the deployed suite exercises the full Azure-to-CDP journey. Passing the Docker
checks does not prove that deployed Azure navigation or configuration works.

### Environment and contract changes

For every added, renamed, removed or changed environment variable, feature
flag, default, credential, endpoint or dependency used by this journey:

1. Trace where the service reads the setting and which journey behavior it
   controls. Check the service examples/defaults and deployment configuration.
2. Check the journey repository's
   [CI Compose stack](https://github.com/DEFRA/waste-obligations-journey-tests/blob/main/ci/compose.yml),
   action inputs/environment and caller workflow. Add or amend the value where
   the target service actually receives it; a variable set only on the test
   runner does not configure another container. Update the journey `.env.example`
   only for settings consumed by the runner or required local setup.
3. Review service-owned Compose fragments, WireMock contracts, infrastructure
   initialisers and scenario seed data. Keep service dependency setup with its
   owning service, and shared orchestration/scenario data in the journey repo.
4. Check the deployed CDP and Azure configuration separately. Document required
   flag/secret changes and their owner; Docker values do not propagate there.
   Keep real credentials out of source control and logs.
5. Coordinate repository revisions when contracts change. The three CI callers
   select a matching journey branch or fall back to main; the action resolves
   explicit backend/frontend/proxy revisions, then matching branches, then
   published images with main setup assets. Verify the selected revisions
   contain all required changes before relying on a run.
6. Run the affected Docker journey profiles and the deployed journey where its
   behavior changes and the environment is available. Record mode, revision,
   pass/fail/skip counts and blockers. Explain in the change description which
   journey setup was updated, or why no journey configuration change is needed.
   Do not hide a configuration mismatch by skipping a whole scenario.

This service is a runtime dependency of the shared Docker stack. The current
action uses its published image; it does not resolve a matching source branch
for Waste Organisations. To validate an unpublished change, build/select that
image explicitly using `WASTE_ORGANISATIONS_IMAGE`. For an unpublished local
image, start Compose with `--pull never`, because the service otherwise uses
`pull_policy: always`. Do not assume a same-named branch is automatically
included. Organisation fixtures are seeded through the API by the journey repository's `ci/seed-waste-organisations.mjs`.
Review API contracts, organisation registration data, ACL credentials and
container environment there whenever this service changes.

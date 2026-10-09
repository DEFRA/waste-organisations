# Api.V1Tests

## Purpose

`Api.V1Tests` is the regression baseline for the existing (v1) Waste Organisations
API. The suite pins the observable v1 contract — status codes, response shapes,
headers, authentication and authorisation behaviour, server-error handling, and
the OpenAPI document — exactly as the code behaves today. It exists to catch any
unintended change to v1 before it ships: a failing v1 test means the v1 contract
moved.

The suite has two tiers:

- **`Blackbox/`** drives the running API over HTTP against the Docker Compose
  stack. These tests carry `[Trait("Category", "V1Baseline")]` and
  `[Trait("Category", "IntegrationTests")]`.
- **`InProcess/`** hosts the API in a `WebApplicationFactory`, substituting
  services where needed (scope authorisation, throwing dependencies) and reading
  the OpenAPI document directly. These tests carry
  `[Trait("Category", "V1Baseline")]` only.

## How to run

Bring up the local environment first, then build and run the suite:

```bash
docker compose up --build -d

DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build tests/Api.V1Tests/Api.V1Tests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test --test-modules tests/Api.V1Tests/bin/Debug/net10.0/Api.V1Tests.dll --no-build -v:minimal

docker compose down -v --remove-orphans
```

The suite is deterministic and independent: each test provisions its own data and
asserts only on its own organisation ids, so it passes in any order, under
parallelism, and twice in a row without `docker compose down -v` between runs.

## Pointing at another environment

The black-box tier reads its base URL from the `V1_TESTS_BASE_URL` environment
variable and defaults to `http://localhost:8080` (the Compose stack). Point the
suite at another running environment by setting it:

```bash
V1_TESTS_BASE_URL=https://my-other-environment.example dotnet test --test-modules tests/Api.V1Tests/bin/Debug/net10.0/Api.V1Tests.dll --no-build
```

The in-process tier hosts the API itself and ignores `V1_TESTS_BASE_URL`.

## Snapshots

Body and document assertions use [Verify](https://github.com/VerifyTests/Verify).
Each snapshot is a committed `*.verified.json` file next to its test. On a
mismatch, Verify writes a `*.received.json` file; review the difference, and if
the new output is correct, update the `*.verified.json` file to match.

**A v1 snapshot changes only for a deliberate breaking change to the v1
contract.** Any other snapshot difference is a regression: fix the code, not the
snapshot. Never auto-accept received snapshots.

## Known ticket discrepancies

The REGEN-145 ticket and the code disagree in the places below. These tests pin
the **code's** behaviour, not the ticket's.

1. The ticket route for registrations is
   `/organisations/{id}/{type}--{registrationYear}`. The code route is
   `/organisations/{id}/registrations/{type}-{registrationYear}`.
2. The ticket omits the 404 on registration PUT when the organisation is not
   found. The code returns it.
3. The ticket omits the 400 on registration DELETE for an invalid type or year.
   The code returns it.
4. The ticket omits 401 and 403 on every endpoint. The code returns them, and the
   OpenAPI document does not document them.
5. The ticket lists 500 on every endpoint. The code never returns 500
   deliberately; it appears only when a dependency throws (see the S-500 tests).

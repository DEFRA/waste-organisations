# 001 REGEN-145: Baseline test scenarios for the existing (v1) Waste Organisations API

Jira: https://eaflood.atlassian.net/browse/REGEN-145 (parent REGEN-125, blocks REGEN-140).

## 1. Goal

Create an automated regression baseline that pins the current behaviour of the Waste Organisations API ("v1"). The API will evolve and gain new endpoints. These tests must fail if any change breaks an existing v1 consumer.

The tests describe what v1 does today. They do not fix bugs or redesign anything.

## 2. Decisions (already agreed, do not revisit)

1. **New project.** Create `tests/Api.V1Tests/` (xunit v3, net10.0, Microsoft Testing Platform, same conventions as `tests/Api.IntegrationTests/Api.IntegrationTests.csproj`). Reference `tests/Testing/Testing.csproj`. Add it to `waste-organisations.slnx` under `/tests/`.
2. **Source of truth is the current code**, not the ticket table. The ticket table has errors (see section 4). Where the ticket and code differ, test the code and record the difference in section 4.
3. **Black-box by default.** Scenarios call the API over HTTP with `HttpClient`. They do not use mocks or internal services. Base URL defaults to `http://localhost:8080` and may be overridden by env var `V1_TESTS_BASE_URL`. This lets the same suite run against a deployed environment later.
4. **Two tiers inside the one project**, because the docker-compose API cannot produce every v1 behaviour:
   - `Blackbox/` runs against the compose stack (`docker compose up --build -d`, `ASPNETCORE_ENVIRONMENT=Development`). Available auth clients there: `Developer` (ApiKey, secret `developer-pwd`, read+write) and `IntegrationTest` (OAuth, read+write; build the JWT with `tests/Testing/Jwt.cs`, as `IntegrationTestBase` does).
   - `InProcess/` uses `WebApplicationFactory` against `src/Api` with `appsettings.IntegrationTests.json` clients. It covers only what compose cannot: read-only and write-only clients (403) and forced 500 responses via a throwing `IOrganisationService`. Copy the pattern from `tests/Api.Tests/Endpoints/EndpointTestBase.cs` and `ApiWebApplicationFactory.cs`. Do not take a dependency on `Api.Tests`.
5. **Traits.** Tag black-box tests `[Trait("Category", "V1Baseline")]` and `[Trait("Category", "IntegrationTests")]`. Tag in-process tests `[Trait("Category", "V1Baseline")]` only.
6. **Isolation.** Every black-box test uses its own `Guid.NewGuid()` organisation id. Tests never assume an empty database and never delete other tests' data. Search tests assert on their own organisations by filtering the result by id.
7. **Snapshots.** Use Verify (`Verify.XunitV3`, as the other test projects do) for response bodies and the OpenAPI document. Scrub Guids and timestamps with the same settings as `tests/Api.Tests/VerifySettings.cs`. Snapshot files live next to the test class. Never auto-accept snapshots.
8. **Existing tests stay untouched.** Do not edit or delete anything under `tests/Api.Tests` or `tests/Api.IntegrationTests`. Duplicating a scenario already covered there is acceptable; this suite is the standalone v1 contract.

## 3. v1 contract reference

Read these files before writing tests: `src/Api/Endpoints/**`, `src/Api/Dtos/**`, `src/Api/RegistrationYear.cs`, `src/Api/Authentication/**`, `tests/Testing/**`.

| Operation | Method and route | Auth scope | Success | Other codes in code |
|---|---|---|---|---|
| Search | `GET /organisations?registrations=&registrationYears=&statuses=` | read | 200 `{ "organisations": [...] }` | 400, 401, 403, 500 |
| Get | `GET /organisations/{id:guid}` | read | 200 organisation | 404, 401, 403, 500 |
| Put organisation | `PUT /organisations/{id:guid}` body `OrganisationRegistration` | write | 201 (new, `Location: /organisations/{id}`) or 200 (existing) | 400, 401, 403, 500 |
| Put registration | `PUT /organisations/{id:guid}/registrations/{type}-{registrationYear:int}` body `{ "status": ... }` | write | 201 (new, `Location` header) or 200 (existing) with registration | 400, 404 (organisation missing), 401, 403, 500 |
| Delete registration | `DELETE /organisations/{id:guid}/registrations/{type}-{registrationYear:int}` | write | 204 | 400, 404 (organisation or registration missing), 401, 403, 500 |

Wire values:
- `type` / `registrations`: `SMALL_PRODUCER`, `LARGE_PRODUCER`, `COMPLIANCE_SCHEME`, `REPROCESSOR`, `EXPORTER`
- `status` / `statuses`: `REGISTERED`, `CANCELLED`
- `businessCountry`: `GB-ENG`, `GB-NIR`, `GB-SCT`, `GB-WLS` (optional)
- `registrationYear`: integer 2023 to 2050 inclusive
- Query lists are comma separated. Absent or empty list means no filter.
- Organisation fields: `id`, `name`, `tradingName?`, `businessCountry?`, `companiesHouseNumber?`, `address` (`addressLine1`, `addressLine2`, `town`, `county`, `postcode`, `country`, all optional), `registrations[]` (`status`, `type`, `registrationYear`, `created`, `updated`).
- Put organisation body: `name`, `tradingName?`, `businessCountry?`, `companiesHouseNumber?`, `address`, `registration` (`status`, `type`, `registrationYear`).
- Auth: ApiKey via `Authorization: Basic base64(clientId:secret)`. OAuth via `Authorization: Bearer <jwt>` with a `client_id` claim (confirm the exact scheme names in `BasicAuthenticationHandler.SchemeName` and `JwtAuthenticationHandler.SchemeName`).

## 4. Discrepancies between the ticket and the code

Record these in the test project `README.md` under "Known ticket discrepancies". Do not test the ticket's version.

1. Ticket route for registrations is `/organisations/{id}/{type}--{registrationYear}`. Code route is `/organisations/{id}/registrations/{type}-{registrationYear}`.
2. Ticket omits 404 on registration PUT (organisation not found).
3. Ticket omits 400 on registration DELETE (invalid type or year).
4. Ticket omits 401 and 403 on every endpoint.
5. Ticket lists 500 on every endpoint. The code never returns it deliberately; it appears only when a dependency throws (see S-500).

## 5. Scenarios

Each scenario is one or more xunit tests. Use `[Theory]` for value ranges. Name tests `When<Condition>_Should<Outcome>` like the existing tests. IDs are for traceability in the PR and in snapshot names.

### 5.1 Search organisations (`Blackbox/SearchOrganisationsTests.cs`)

- **SEARCH-01** No filters returns 200 and a body with an `organisations` array that contains the organisations created by the test.
- **SEARCH-02** Filter `registrations=<each type>` (Theory over all 5 types) returns only organisations having a registration of that type.
- **SEARCH-03** `registrations=LARGE_PRODUCER,EXPORTER` returns organisations matching either type.
- **SEARCH-04** `registrationYears=<year>` returns only matching organisations. Cover 2023, 2050 and a middle year.
- **SEARCH-05** `registrationYears=2024,2025` matches either year.
- **SEARCH-06** `statuses=REGISTERED` and `statuses=CANCELLED` each return only matching organisations.
- **SEARCH-07** Combined `registrations`, `registrationYears` and `statuses` apply together. Create organisations that match on only two of three criteria and assert they are excluded.
- **SEARCH-08** Filters matching nothing return 200 with the test's organisations absent.
- **SEARCH-09** Invalid `registrations` value (for example `UNKNOWN`, lower case `small_producer`, one valid and one invalid) returns 400 problem details. Snapshot the body.
- **SEARCH-10** Invalid `registrationYears` (2022, 2051, `abc`, `2024,abc`) returns 400. Snapshot the body.
- **SEARCH-11** Invalid `statuses` (for example `PENDING`) returns 400. Snapshot the body.

### 5.2 Get organisation (`Blackbox/GetOrganisationTests.cs`)

- **GET-01** Existing organisation returns 200. Snapshot the full body, including registration `created` and `updated` as ISO 8601 with offset.
- **GET-02** Unknown id returns 404.
- **GET-03** Non-guid id (for example `not-a-guid`) returns 404. This is the observed behaviour because of the `{id:guid}` route constraint. Confirm by running, then pin what actually happens.
- **GET-04** Optional fields (`tradingName`, `businessCountry`, `companiesHouseNumber`) are returned as `null` or omitted exactly as today. Snapshot to pin it.

### 5.3 Put organisation (`Blackbox/PutOrganisationTests.cs`)

- **ORG-01** New id returns 201 with `Location: /organisations/{id}` and a body matching the stored organisation. Snapshot the body.
- **ORG-02** Subsequent GET returns the same organisation with exactly one registration.
- **ORG-03** Put on an existing id with a different registration (new type or year) returns 200 and the organisation now has both registrations.
- **ORG-04** Put on an existing id with the same type and year but a different status returns 200 and updates the status. The registration count is unchanged.
- **ORG-05** Put on an existing id updates organisation fields (`name`, `tradingName`, `address`, `businessCountry`, `companiesHouseNumber`). Pin exactly which fields change and which are kept when omitted, by observing current behaviour, and snapshot the result.
- **ORG-06** Registration year boundaries 2023 and 2050 return 201.
- **ORG-07** Registration year 2022 and 2051 return 400. Snapshot.
- **ORG-08** Every valid type (Theory over 5) and status (Theory over 2) returns 201.
- **ORG-09** Invalid `businessCountry` returns 400. Snapshot.
- **ORG-10** Invalid registration `type` returns 400. Snapshot.
- **ORG-11** Invalid registration `status` returns 400. Snapshot.
- **ORG-12** Missing required fields (`name`, `address`, `registration`) each return 400. Snapshot.
- **ORG-13** Empty or malformed JSON body returns 400 (or whatever it is today; pin it).
- **ORG-14** Non-guid id in route returns 404 (pin observed behaviour).
- **ORG-15** Every `businessCountry` value (Theory over 4) is accepted and echoed back using its wire value.

### 5.4 Put registration (`Blackbox/PutRegistrationTests.cs`)

Setup for every test: create the organisation with `PUT /organisations/{id}`.

- **REG-01** New type and year returns 201, `Location` header `/organisations/{id}/registrations/{key}` (pin the exact key format), and body with `status`, `type`, `registrationYear`, `created`, `updated`. Snapshot.
- **REG-02** Same type and year again with a different status returns 200 and the new status. `created` is unchanged and `updated` is not earlier than before.
- **REG-03** Same type and year again with the same status returns 200.
- **REG-04** Subsequent GET shows the registration list including the new one.
- **REG-05** Unknown organisation id returns 404.
- **REG-06** Invalid type in route (`UNKNOWN`, lower case, empty) returns 400. Snapshot.
- **REG-07** Registration year 2022, 2051 and non-numeric return 400 or the observed status. Pin it. Snapshot.
- **REG-08** Year boundaries 2023 and 2050 return 201.
- **REG-09** Invalid or missing body `status` returns 400. Snapshot.
- **REG-10** Each type (Theory over 5) with each status (Theory over 2) returns 201 for a fresh organisation.

### 5.5 Delete registration (`Blackbox/DeleteRegistrationTests.cs`)

- **DEL-01** Existing registration returns 204 with an empty body. A follow-up GET no longer lists it. Other registrations on the organisation remain.
- **DEL-02** Deleting the same registration twice returns 204 then 404.
- **DEL-03** Unknown organisation returns 404.
- **DEL-04** Organisation exists but the registration (type and year) does not return 404.
- **DEL-05** Invalid type in route returns 400. Snapshot.
- **DEL-06** Year 2022 and 2051 return 400. Snapshot.
- **DEL-07** Deleting the last registration returns 204 and the organisation still exists with an empty `registrations` array.

### 5.6 Authentication and authorisation

Black-box tier (`Blackbox/AuthTests.cs`), Theory over all five operations:
- **AUTH-01** No `Authorization` header returns 401.
- **AUTH-02** ApiKey client `Developer` with correct secret succeeds on read and write operations.
- **AUTH-03** ApiKey with wrong secret returns 401.
- **AUTH-04** OAuth client `IntegrationTest` with a valid JWT succeeds on read and write operations.
- **AUTH-05** Malformed credentials (bad base64, unknown scheme, unknown client id) return 401.

In-process tier (`InProcess/ScopeTests.cs`), Theory over all five operations:
- **AUTH-06** Read-only client (ApiKey and OAuth) can call Search and Get, and receives 403 on Put organisation, Put registration and Delete.
- **AUTH-07** Write-only client (ApiKey and OAuth) receives 403 on Search and Get, and succeeds on the write operations.
- **AUTH-08** Read-write client succeeds on everything.

### 5.7 Server errors (`InProcess/ServerErrorTests.cs`)

- **S-500** For each of the five operations, replace `IOrganisationService` with a substitute that throws. Assert status 500 and that the body is a problem details response (`application/problem+json` with `status` 500). Pin the observed content type and shape with a snapshot. Do not assert on exception text.

### 5.8 Contract shape and OpenAPI

- **SHAPE-01** (`Blackbox/ContractShapeTests.cs`) Parse the raw JSON of a GET response and assert exact property names (`id`, `name`, `tradingName`, `businessCountry`, `companiesHouseNumber`, `address`, `registrations`, and inside registrations `status`, `type`, `registrationYear`, `created`, `updated`). Assert enum wire values as strings, not numbers.
- **SHAPE-02** (`Blackbox/ContractShapeTests.cs`) Search response root has exactly one property, `organisations`.
- **SHAPE-03** (`Blackbox/ContractShapeTests.cs`) Error responses for 400 and 404 on the endpoints above have the problem details content type. Pin it by observation.
- **OPENAPI-01** (`InProcess/OpenApiTests.cs` or `Blackbox/OpenApiTests.cs`) Fetch `openapi/v1.json` and snapshot it. The snapshot is the machine-readable v1 contract.
- **OPENAPI-02** Add a separate test that parses the document and asserts that every v1 path, method and response status code listed in section 3 is present, and that required request properties and enum values are present. This test must still pass when new endpoints or new optional fields are added. Only removals or changes to existing items fail it.

## 6. Non-functional requirements

- Tests must be deterministic and independent. Any order, any parallelism within a collection.
- Use `TestContext.Current.CancellationToken` for every async call, as the existing tests do.
- Use AutoFixture builders from `tests/Testing/Fixtures` where they fit. Extend the Testing project only by adding files; do not change existing fixtures.
- Format with CSharpier per `.csharpierrc`. Honour `.editorconfig` and the Sonar rules the repository already enforces.
- Do not change anything under `src/`. If a scenario cannot be written without a source change, stop and note it in `PROGRESS.md`.
- Add `tests/Api.V1Tests/README.md` explaining: purpose, how to run, how to point at another environment with `V1_TESTS_BASE_URL`, how snapshots are reviewed, the rule that v1 snapshots change only for a deliberate breaking change, and the discrepancy list from section 4.
- Add the V1 tests to `.github/workflows/check-pull-request.yml` after the existing integration test step, using the same build and `dotnet test --test-modules` pattern. The black-box tests run while the compose stack is up.

## 7. Acceptance criteria

1. `tests/Api.V1Tests` builds with the repo's build command (see `AGENTS.md`) and is listed in `waste-organisations.slnx`.
2. With the compose stack up, `dotnet test --test-modules tests/Api.V1Tests/bin/Debug/net10.0/Api.V1Tests.dll --no-build` passes with zero failures and zero skipped tests. Run it twice in a row without `docker compose down -v` and it passes both times.
3. Every scenario ID in section 5 has at least one test, and the test name or a comment contains the ID.
4. Each of the five operations has at least one success test, every documented error code (400, 401, 403, 404, 500 as per section 3) is covered at least once, and unsupported combinations are noted in `PROGRESS.md` rather than silently dropped.
5. Mutation check, done once and then reverted: temporarily rename the JSON property `registrationYear` in `src/Api/Dtos/Registration.cs`, then change the registration route to `/orgs/...`; the suite must fail in both cases. Restore the source afterwards and confirm `git diff -- src` is empty.
6. Existing `Api.Tests` and `Api.IntegrationTests` still pass unchanged.
7. No files under `src/` or existing test projects are modified, except `waste-organisations.slnx` and `.github/workflows/check-pull-request.yml`.
8. Snapshot files are committed and reviewed. No `.received.*` files are left in the repo.

## 8. Suggested build order for the plan

1. Project scaffold, slnx entry, base classes (black-box client factory for ApiKey and OAuth, unique-data helpers), Verify settings.
2. Get and Put organisation (they bootstrap data for everything else).
3. Search.
4. Put registration and Delete registration.
5. Auth black-box tests, then the in-process scaffold with scope tests and the 500 tests.
6. Contract shape and OpenAPI tests.
7. README, CI workflow step, mutation check.
8. Full verification: build and run all three test projects per `AGENTS.md`.

## 9. Out of scope

- New v2 or future endpoints, load tests, performance tests.
- Health, metrics, security-header and logging behaviour.
- Mongo index or persistence-layer tests (already covered by `OrganisationServiceTests`).
- Changing or fixing any v1 behaviour, including behaviour that looks like a bug. Record such observations in `PROGRESS.md` instead.

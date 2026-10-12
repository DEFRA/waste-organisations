# REGEN-145: Add a protected test project for the existing (v1) Waste Organisations API

| | |
|---|---|
| Type | Task: a new test project and repository configuration. No change to what the API does |
| Priority | P1 (High): it blocks REGEN-140 |
| Risk | Low to medium (see Risks) |
| Source | Jira story REGEN-145, and the 3-amigos summary and discussion notes added to it on 12 October 2026 |

## Problem

Manage Account will change this API for v2. Current clients must not be broken by that work.

Normally tests evolve with the code. That does not fit here, because the team inherited this code and is not really evolving it. In effect two services will run inside one, and the v1 one has to keep behaving as it does now.

The existing unit and integration tests cannot guarantee that. They use the API's internal types, they are expected to change as the code changes, and nothing makes them hard to change.

We need a set of v1 tests that is set in stone: either not changed, or changed only by a conscious, approved decision.

## Who benefits

- **Current clients of the API:** their behaviour is protected while v2 is built.
- **Engineers working on v2:** a failing v1 test tells them early, on their own machine, that they have changed something clients rely on.

## Proposed solution

Add a separate, black-box test project for v1, make it difficult to change, and run it locally and on every pull request, in `check-pull-request.yml`.

This story delivers the project, its protection, its pipeline step, and tests for all of the API's current behaviour that can be observed from outside. The scenarios in the existing integration tests are the starting point; the full list is under "Scenarios".

### The project

| | |
|---|---|
| Path | `tests/Api.V1.Tests/` |
| Framework | xUnit v3 on the Microsoft Testing Platform, AwesomeAssertions and Verify, as the other test projects use |
| Target | `http://localhost:8080`, the address the Docker Compose stack exposes |

### Rules for the project

1. **No reference to production code.** No project reference to `src/Api` or `tests/Testing`. If the tests used the API's own types, a v2 change to those types would change what the tests send and expect without anyone editing a v1 test.
2. **Requests are raw JSON.** Response bodies are checked with Verify snapshots; status codes and headers with explicit assertions.
3. **HTTP, with one exception.** Tests set up data through the API. Only the legacy-data scenarios (`LEG-*`) also write to MongoDB, through one small helper that inserts and removes documents built as raw `BsonDocument`s. The helper never uses the API's entity types and touches only documents with ids the test created.
4. **Each test owns its data.** A test creates organisations with new ids and asserts only on those. It never relies on an empty database or on another test.
5. **Characterisation is marked.** A test that pins behaviour which looks unintended carries the comment `// Characterisation: documents current behaviour, not a requirement`.
6. **Scenario ids.** Each test carries `[Trait("Scenario", "<ID>")]` using the ids under "Scenarios".

### Making the tests difficult to change

- Add `.github/CODEOWNERS` with a rule for `/tests/Api.V1.Tests/` and one for the `CODEOWNERS` file itself, so that the rule cannot be removed quietly. Both rules name Darren (`@FluentSynergyDW`) as the code owner.
- Turn on "Require review from Code Owners" for `main`. A change to a v1 test or its snapshots then cannot merge without an owner's approval.

A git submodule was also considered. It is harder still to change, but it adds work to every clone, every workflow and the Docker build, and it breaks "runs locally with no extra effort". Rule 1 keeps that option open: the project could be moved to its own repository later without being rewritten.

### Pipeline

Add a "Run v1 tests" step straight after "Run integration tests" in `check-pull-request.yml`. The Compose stack is already up at that point.

```bash
dotnet build tests/Api.V1.Tests/Api.V1.Tests.csproj
dotnet test --test-modules tests/Api.V1.Tests/bin/Debug/net10.0/Api.V1.Tests.dll --no-build
```

### Supporting changes

| File | Change |
|---|---|
| `waste-organisations.slnx` | Add the project |
| `Dockerfile` | Add the project's two `COPY` lines next to the other test projects. Without them the image build cannot restore the solution |
| `compose.yml` | Add test clients with limited scopes as `Acl__Clients__…` environment variables on the `api` service: one read-only and one write-only, for API key and for OAuth. Today the stack only has clients with both scopes, so 403 cannot be tested |
| `AGENTS.md` | Add the build and test commands, and a rule: never edit or delete a v1 test or snapshot to make a build pass; stop and ask |
| `README.md` | Add the project to the Tests section |

## Scenarios

**Expected today** is marked *(observed)* where the result was seen by calling the API in-process on 8 October 2026 at commit `826750c`. Every expected result is confirmed against the Compose stack when its test is written. If the API behaves differently, correct this spec; do not bend the test.

The ids follow the scenario list already attached to the story. Rows marked **Added** are not in that list.

The existing integration scenarios are covered by ORG-PUT-01 and 03, ORG-SEARCH-02 and 03, REG-PUT-01 and 02, and REG-DEL-01. `BackwardsCompatibleWithProductionData` is covered by the legacy-data scenarios (`LEG-*`), which write their documents directly to MongoDB.

### `GET /organisations` (search)

"Returned" always means "among the organisations this test created".

| ID | Scenario | Expected today |
|---|---|---|
| ORG-SEARCH-02 | Several organisations, no filters | 200; all are returned, each with all its registrations |
| ORG-SEARCH-03 | `registrations` = each of the five types in turn | Only organisations with a registration of that type |
| ORG-SEARCH-04 | Several values in one parameter | An organisation matching any listed value is returned |
| ORG-SEARCH-05 | `registrationYears`, including 2023 and 2050 | An organisation with a registration in any listed year is returned |
| ORG-SEARCH-06 | `statuses` = `REGISTERED`, `CANCELLED`, both | An organisation with a registration in any listed status is returned |
| ORG-SEARCH-07 | Filters are each satisfied, but by different registrations of one organisation | Not returned |
| ORG-SEARCH-08 | Organisation matched through one registration | It is returned with all its registrations, not only the matching one |
| ORG-SEARCH-09 | Filters that none of the test's organisations match | 200; none of them is returned |
| ORG-SEARCH-10 | Organisation whose last registration was deleted | Never returned by search; still returned by get |
| ORG-SEARCH-11 | Invalid `registrations` | 400, `Invalid registration type(s) - <invalid values>` |
| ORG-SEARCH-12 | Invalid `registrationYears`: not a number, 2022, 2051 | 400, `Invalid registration year(s) - <invalid values>` |
| ORG-SEARCH-13 | Invalid `statuses` | 400, `Invalid registration status(s) - <invalid values>` |
| ORG-SEARCH-14 | Several parameters invalid | 400, one error per parameter *(observed)* |
| ORG-SEARCH-15 | Input edge cases | *(observed)* Lower-case value: 400. Empty value or trailing comma: 400. Space after a comma, duplicate values, repeated parameter, unknown parameter: 200. Numeric value such as `registrations=1`: 200. Characterisation |
| ORG-SEARCH-16 | Response shape and wire values | Snapshot of one organisation in the result |
| ORG-SEARCH-17 | No credentials, write-only client, read-only client | 401, 403, 200 |

### `GET /organisations/{id}`

| ID | Scenario | Expected today |
|---|---|---|
| ORG-GET-01 | Existing organisation | 200, full payload (snapshot) |
| ORG-GET-02 | Unknown id; empty GUID | 404, empty body |
| ORG-GET-03 | Id is not a GUID | 404, empty body, even without credentials *(observed)* |
| ORG-GET-05 | Optional fields were never set | Returned as explicit `null`, not left out *(observed)* |
| ORG-GET-06 | No credentials, write-only client, read-only client | 401, 403, 200 *(observed)* |
| ORG-GET-08 | **Added.** Id is an upper-case GUID, or has no hyphens | 200, the same organisation *(observed)* |

### `PUT /organisations/{id}`

| ID | Scenario | Expected today |
|---|---|---|
| ORG-PUT-01 | New id, valid body | 201, `Location: /organisations/{id}`, one registration, `created` = `updated` |
| ORG-PUT-02 | Existing id, changed fields | 200; fields replaced; optional fields left out become `null` *(observed)* |
| ORG-PUT-03 | Existing id, registration with a new type or year | 200; registration added, the others kept |
| ORG-PUT-04 | Same request twice | 201 then 200; registration timestamps unchanged |
| ORG-PUT-05 | Same registration, new status | 200; `updated` advances, `created` kept |
| ORG-PUT-06 | Year 2023, 2050 | 201 |
| ORG-PUT-07 | Year 2022, 2051 | 400; the year must be between 2023 and 2050 |
| ORG-PUT-08 | Invalid `businessCountry`, type or status | 400 |
| ORG-PUT-09 | Each `businessCountry` (`GB-ENG`, `GB-NIR`, `GB-SCT`, `GB-WLS`) and `null` | Accepted and returned unchanged |
| ORG-PUT-10 | Each required field missing in turn: `name`, `address`, `registration`, and `status`, `type`, `registrationYear` inside it | 400 naming the field *(observed)* |
| ORG-PUT-11 | Body edge cases | *(observed)* Malformed JSON: 400. Empty body: 400. Content type not JSON: 415, empty body. Unknown properties, and property names in another case: accepted. A `registrations` array in place of `registration`: 400. Characterisation |
| ORG-PUT-12 | Id is not a GUID | 404 *(observed)* |
| ORG-PUT-14 | Several updates to one organisation sent at the same time | Each returns 200 or 500; every registration whose request returned 200 is stored, and no other |
| ORG-PUT-15 | No credentials, read-only client, write-only client | 401, 403, 201 *(observed)* |
| ORG-PUT-17 | Unicode and special characters in text fields | Stored and returned unchanged |
| ORG-PUT-18 | **Added.** `"address": null` | 500 *(observed)*. Characterisation, likely bug |
| ORG-PUT-19 | **Added.** `"name": null` or `""` | 201, stored as sent *(observed)*. Characterisation, likely bug |
| ORG-PUT-20 | **Added.** Enum sent as a number; year sent as a string | `"type": 1` is stored as `LARGE_PRODUCER`; `"registrationYear": "2025"` is accepted *(observed)*. Characterisation, likely bug |
| ORG-PUT-21 | **Added.** Free-text fields with no validation: a very long `name`, whitespace-only values, a `companiesHouseNumber` or `postcode` in any format | Not yet observed. The code validates only the registration fields and `businessCountry`, so these should be accepted and returned as sent. Observe each case against the stack, then pin it. Characterisation |

### `PUT /organisations/{id}/registrations/{type}-{registrationYear}`

Body: `{ "status": "REGISTERED" }` or `{ "status": "CANCELLED" }`.

| ID | Scenario | Expected today |
|---|---|---|
| REG-PUT-01 | Organisation exists, registration does not | 201, `Location: /organisations/{id}/registrations/{type}-{registrationYear}`, `created` = `updated` |
| REG-PUT-02 | Registration exists, status changed | 200; `updated` advances, `created` kept |
| REG-PUT-03 | Registration exists, same status | 200; timestamps unchanged *(observed)* |
| REG-PUT-04 | Unknown organisation | 404, empty body |
| REG-PUT-05 | Each of the five types; years 2023 and 2050 | 201 *(observed)* |
| REG-PUT-06 | Invalid type; year 2022, 2051 | 400 |
| REG-PUT-07 | Input edge cases | *(observed)* Year not a number: 404. Lower-case type: 400. Body `{}`, invalid status, lower-case status, empty body: 400. `type` or `registrationYear` in the body: ignored. Characterisation |
| REG-PUT-08 | Other registrations and the organisation's own fields | Untouched |
| REG-PUT-09 | Id is not a GUID | 404 *(observed)* |
| REG-PUT-10 | No credentials, read-only client, write-only client | 401, 403, 201 *(observed)* |
| REG-PUT-11 | Several registrations added to one organisation at the same time | Each returns 201 or 500; exactly the registrations whose request returned 201 are stored |
| REG-PUT-13 | **Added.** Type or status sent as a number | `…/registrations/1-2025` creates `LARGE_PRODUCER-2025`; `"status": 1` is stored as `CANCELLED` *(observed)*. Characterisation, likely bug |

### `DELETE /organisations/{id}/registrations/{type}-{registrationYear}`

| ID | Scenario | Expected today |
|---|---|---|
| REG-DEL-01 | Organisation and registration exist | 204, empty body; only that registration is removed |
| REG-DEL-02 | Unknown organisation | 404 |
| REG-DEL-03 | Registration not found | 404 |
| REG-DEL-04 | Delete twice | 204 then 404 |
| REG-DEL-05 | Delete the last registration | 204; get returns the organisation with `registrations: []`; search never returns it |
| REG-DEL-06 | Invalid type; year 2022, 2051 | 400 |
| REG-DEL-07 | Year not a number; id not a GUID | 404 *(observed)* |
| REG-DEL-08 | Re-create the deleted registration | 201, with a new `created` |
| REG-DEL-10 | No credentials, read-only client, write-only client | 401, 403, 204 *(observed)* |
| REG-DEL-12 | **Added.** Each of the five types | 204 |

### Legacy data (written directly to MongoDB)

Production holds registrations stored without `created` and `updated`. The API cannot create that state, so each test inserts its own document into MongoDB with the helper (rule 3), then checks the behaviour through the API. Insert the document as `BsonDocument`, using the stored shape: database `waste-organisations`, collection `Organisation`, camelCase field names, enums as strings, and the id as a standard-representation GUID.

| ID | Scenario | Expected today |
|---|---|---|
| LEG-01 | Get an organisation whose registrations have no `created` or `updated` | 200; each such registration's `created` and `updated` both equal the organisation's `created` |
| LEG-02 | Search returns such an organisation | The same fallback values |
| LEG-03 | Organisation `PUT` with the same registration and status | 200; the registration's timestamps still equal the organisation's `created`; the organisation's `updated` advances |
| LEG-04 | Registration `PUT` with the same status | 200; the registration's timestamps unchanged, so still the fallback |
| LEG-05 | Registration `PUT` with a changed status | 200; `created` equals the organisation's `created`, `updated` is the time of the request |
| LEG-06 | Document with optional fields missing altogether: `tradingName`, `businessCountry`, `companiesHouseNumber`, address parts | Get returns each as explicit `null` |
| LEG-07 | Document with `registrations` missing, or empty | 200 with `registrations: []`; never returned by search |
| LEG-08 | Document with a value the API would not accept: an unknown registration type or status, or an unknown `businessCountry` | Not yet observed. Observe each case against the stack, then pin it. Characterisation |

### Cross-cutting

| ID | Scenario | Expected today |
|---|---|---|
| X-AUTH-01 | All five endpoints, API key and OAuth: no credentials, wrong secret, unknown client, wrong scope, right scope | *(observed)* No credentials, wrong secret, unknown client: 401. Wrong scope: 403. Right scope: success. Bearer token with an unknown or missing `client_id`: 403. Bearer value that is not a token, or an expired token: 401 |
| X-AUTH-02 | **Added.** No credentials and an invalid body | 401, not 400 *(observed)* |
| X-AUTH-03 | **Added.** Basic value that is not valid base64 | 500 *(observed)*. Characterisation, likely bug |
| X-BODY-01 | **Added.** Error body format | 401, 403 and 404 have an empty body; 400 returns `application/problem+json` with `type`, `title`, `status` and `traceId` *(observed)* |
| X-ROUTE-01 | **Added.** Unsupported method on an existing path: `POST /organisations`, `DELETE /organisations/{id}`, `GET` on a registration | 405 with an `Allow` header *(observed)* |
| X-ROUTE-02 | **Added.** `GET /organisations/{id}/registrations` | 404; no endpoint lists registrations *(observed)* |
| X-CONTRACT-01 | `GET /openapi/v1.json` | 200 without credentials; the document matches a snapshot |
| X-HDR-01 | HSTS header | `Strict-Transport-Security: max-age=31536000; includeSubDomains` on responses |
| X-OPS-01 | `GET /health` | 200, body `Healthy`, no credentials needed |
| X-OPS-02 | `GET /redoc/index.html` | 200, HTML, no credentials needed *(observed)* |
| X-OPS-03 | **Added.** `GET /health/all`, `GET /health/authorized` | `/health/all`: 200, JSON, no credentials needed. `/health/authorized`: 401 without credentials, 200 for any valid client *(observed)* |
| X-DATA-01 | Lifecycle: create an organisation, add all five types, search by each filter, change a status, delete a registration, search again | Results are consistent at every step |

### Not possible in this project

These cannot be observed by a caller of the API, so they stay where they are or stay untested.

| Scenario | Why | Covered elsewhere today |
|---|---|---|
| 500 when the data layer fails, on each endpoint | A caller cannot make the data layer fail | Only `GET /organisations/{id}`, by a unit test |
| Search with an empty database | Tests do not control the whole database | Unit test |
| Stored `version` and `updated` of an organisation | Not exposed by the API. The helper could read them, but they are not client-visible behaviour | Integration test of the service |
| MongoDB index created at start-up | Not client-visible behaviour | Not covered |
| Trace-header propagation | The API makes no outgoing HTTP calls, so nothing is observable | Not covered |

## Work breakdown

Slice 1 comes first. Slices 2 to 6 can then be taken by different engineers, because each adds its own test classes.

| Slice | Work | Scenarios |
|---|---|---|
| 1 | Project skeleton with one test, the supporting changes, the pipeline step and `CODEOWNERS` | X-OPS-01 |
| 2 | Get and organisation PUT | ORG-GET-*, ORG-PUT-* |
| 3 | Registration PUT and DELETE | REG-PUT-*, REG-DEL-* |
| 4 | Search | ORG-SEARCH-* |
| 5 | Cross-cutting | X-* |
| 6 | MongoDB helper, `MongoDB.Driver` package reference and legacy data | LEG-* |

## Acceptance criteria

- [ ] `tests/Api.V1.Tests` is in the solution and builds in the Docker image build. It has no project reference to `src/Api` or `tests/Testing`, and connects to MongoDB only in the `LEG-*` tests.
- [ ] Every scenario in the tables above has a passing test carrying its `Scenario` trait, or is listed in a pull request as not testable, with the reason.
- [ ] Every characterisation test carries the standard comment, and each one marked "likely bug" is listed in its pull request description.
- [ ] The whole suite runs locally with nothing more than `docker compose up --build -d` and the two commands in Notes, and passes twice in a row without the stack being reset in between.
- [ ] The v1 tests run after the integration tests in `check-pull-request.yml`, and a failing v1 test fails the pipeline.
- [ ] `.github/CODEOWNERS` covers `/tests/Api.V1.Tests/` and itself, "Require review from Code Owners" is on for `main`, and a trial pull request that touches a v1 test cannot merge without an owner's approval.
- [ ] No file under `src/` is changed, and nothing in `tests/Api.Tests`, `tests/Api.IntegrationTests` or `tests/Testing` is changed.
- [ ] `AGENTS.md` and `README.md` describe how to run the v1 tests, and `AGENTS.md` carries the rule against editing them.

## Decisions

| Topic | Decision |
|---|---|
| How to organise the v1 tests | A separate test project in this repository |
| How tied to the code | Fully black-box: no production types, no direct database access |
| How to make them difficult to change | `CODEOWNERS` with required owner review |
| Where MongoDB is touched | Only the `LEG-*` tests, through one helper using raw `BsonDocument`s, because legacy production data cannot be created through the API |
| Where they run | Against Docker Compose, locally and in the pull request check (`check-pull-request.yml`) only, not in the publish workflows. No deployed environment |
| What this story covers | All v1 behaviour that can be observed from outside the API: the 83 scenarios in this spec, delivered in slices |
| 401 and 403 | In scope, using limited-scope test clients added to `compose.yml` |
| Behaviour that looks like a bug | Pinned as marked characterisation tests, so that a later fix is a deliberate, owner-approved change |

## Dependencies

- A repository admin, to turn on "Require review from Code Owners" for `main`.
- Darren (`@FluentSynergyDW`) nominated as code owner. Darren needs write access to the repository, or GitHub ignores the CODEOWNERS entries that name them.

## Risks

| Risk | Level | Mitigation |
|---|---|---|
| A test depends on timing and fails now and then | Low | The API truncates timestamps to the millisecond, so a test that expects `updated` to advance waits 100 ms before the second request, as `Api.IntegrationTests` does with 50 ms. Assert the order of timestamps, not their values. The two concurrency scenarios assert an outcome that holds whether or not a conflict occurs |
| Tests interfere through the shared database | Low | Rule 4, checked by running the suite twice without resetting the stack |
| A legacy document written by hand differs from what production holds | Medium | Build it from the stored shape in the `LEG-*` section, and check one against a real production document if one can be shared |
| The helper couples the tests to the storage shape, so a v2 storage change breaks them | Low | That is intended: the stored shape is what production data looks like, and a break forces a deliberate decision |
| The branch setting is not turned on, so the protection only notifies | Medium | It is an acceptance criterion with a trial pull request |
| A pinned bug is mistaken for a requirement | Medium | The standard comment on each characterisation test, and the list in each pull request |
| An expected result in this spec is wrong | Low | Each is confirmed against the Compose stack when its test is written; correct the spec, do not bend the test |
| The pipeline gets slower | Low | The stack is already running for the integration tests; only the test run is added |
| SonarQube reports the duplicated test code | Low | Check the first pull request's analysis; exclude the project from duplication checks if needed |

## Out of scope

- Any production code change.
- Changing or removing existing tests.
- Running the v1 tests against a CDP environment.
- The five items under "Not possible in this project", including 500 responses caused by a data-layer failure.
- Tests for v2.
- Creating or updating Jira tickets and Confluence pages.

## Notes

- Start the stack with `docker compose up --build -d` and stop it with `docker compose down -v --remove-orphans`, as `AGENTS.md` describes for the integration tests.
- Local commands, in the form `AGENTS.md` uses:

```bash
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build tests/Api.V1.Tests/Api.V1.Tests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test --test-modules tests/Api.V1.Tests/bin/Debug/net10.0/Api.V1.Tests.dll --no-build -v:minimal
```

- The Compose stack runs the API in the `Development` environment. Its clients are in `src/Api/appsettings.Development.json`: `Developer` (API key) and `IntegrationTest` (OAuth), both with read and write scope. The limited-scope clients for the 401 and 403 scenarios are the ones this story adds to `compose.yml`.
- The API accepts a Bearer token signed with any key and reads the client from its `client_id` claim. The project needs its own small helper to build one. `tests/Testing/Jwt.cs` shows how, but must not be referenced. That file only makes a valid token that expires in 30 minutes; the helper must also build an expired token and tokens with an unknown or missing `client_id`, for X-AUTH-01.
- Tests cannot use `FakeTimeProvider`, because the API runs in Docker with the system clock. Where a scenario compares timestamps before and after a change (ORG-PUT-04 and 05, REG-PUT-02 and 03, LEG-03 to 05), call one shared `Wait()` helper of 100 ms between the two requests.
- Use fixed request payloads, not generated data, so that snapshots are stable. Scrub ids and timestamps in the Verify settings as well as `traceId`; Verify scrubs GUIDs and dates by default, so check that the snapshots show placeholders.
- In the project's Verify settings, call `UseStrictJson()` and `DontIgnoreEmptyCollections()`, and scrub `traceId`. Without the second, a snapshot drops `"registrations": []`.
- For new snapshots, run with `DiffEngine_Disabled=true`, read each `.received.json` against this spec, then accept it as `.verified.json`.
- The project needs `MongoDB.Driver` for the helper. Use the version `src/Api` uses, and note that the `nuget-data-access` group in `.github/dependabot.yml` already matches `MongoDB.Driver*`. The other packages are already used in this repository. Check `.github/dependabot.yml` against the rules in `AGENTS.md`.
- The helper connects to `mongodb://127.0.0.1:27017`, the port `compose.yml` publishes, and makes the URI configurable. `Api.IntegrationTests` does the same in `MongoTestBase`, which shows the connection but must not be referenced.
- `dotnet csharpier check .` and a warnings-as-errors build run in the Docker image build, so the new code must pass both.

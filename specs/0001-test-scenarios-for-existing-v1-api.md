# REGEN-145: Test scenarios for the existing (v1) Waste Organisations API

| | |
|---|---|
| Type | Task: tests only, no change to what the API does |
| Priority | P1 (High): it blocks REGEN-140 |
| Risk | Low |
| Source | Jira story REGEN-145 as updated on 9 October 2026 |

## Problem

Manage Account plans to change the current Waste Organisations API. The story earmarks the scenarios the API delivers today, so that those changes can be shown not to affect current functionality.

## Scope

Exactly what the story's acceptance criteria list, and nothing more: the five endpoints, their parameters, the listed parameter values and the listed response codes.

| Endpoint | Parameters and listed values | Codes |
|---|---|---|
| `GET /organisations` | `registrations`: `SMALL_PRODUCER`, `LARGE_PRODUCER`, `COMPLIANCE_SCHEME`, `REPROCESSOR`, `EXPORTER`; `registrationYears`: list of years; `statuses`: `REGISTERED`, `CANCELLED` | 200, 400, 500 |
| `GET /organisations/{id}` | `id`: UUID | 200, 404, 500 |
| `PUT /organisations/{id}` | `id`: UUID | 200, 201, 400, 500 |
| `PUT /organisations/{id}/registrations/{type}-{registrationYear}` | `id`: UUID; `type`: the five registration types; `registrationYear` | 200, 201, 400, 500 |
| `DELETE /organisations/{id}/registrations/{type}-{registrationYear}` | `id`: UUID; `type`: the five registration types; `registrationYear` | 204, 404, 500 |

The story writes the two registration paths as `/organisations/{id}/{type}--{registrationYear}`. This spec uses the paths the API actually serves (see open question 2).

## Proposed solution

Most of the table is already asserted by existing tests. The work is to trace every row to a named test and add tests only where a row has none.

A row counts as covered when a test asserts that response code, or sends that parameter value, on that endpoint. New tests go in the existing classes in `tests/Api.Tests`, which run the API in-process with a mocked `IOrganisationService`. No Docker is needed.

The story's table was taken from the API documentation, and that documentation is itself already pinned: `UT OpenApiTests.OpenApi_VerifyAsExpected` snapshots the whole OpenAPI document, so any change to a documented endpoint, parameter, allowed value or response code already fails a test. The tests below pin the behaviour behind the documentation.

### Traceability: story row to test

`UT` is `tests/Api.Tests`; `IT` is `tests/Api.IntegrationTests`.

**`GET /organisations`**

| Row | Test | State |
|---|---|---|
| 200, no filters | `UT SearchTests.WhenNoOrganisations_ShouldBeOk`, `IT OrganisationTests.OrganisationSearch` | Exists |
| 200, `registrations` = `SMALL_PRODUCER`, `LARGE_PRODUCER` | `UT SearchTests.WhenOrganisations_ShouldBeOk` | Exists |
| 200, `registrations` = `COMPLIANCE_SCHEME`, `REPROCESSOR`, `EXPORTER` | `UT SearchTests.WhenRegistrationType_ShouldBeOk` (theory over all five types) | **New** |
| 200, `registrationYears` = list of years | `UT SearchTests.WhenOrganisations_ShouldBeOk` (2024, 2025) | Exists |
| 200, `statuses` = `REGISTERED` | `UT SearchTests.WhenOrganisations_ShouldBeOk` | Exists |
| 200, `statuses` = `CANCELLED` | `UT SearchTests.WhenStatus_ShouldBeOk` (theory over both statuses) | **New** |
| 400, invalid `registrations` | `UT SearchTests.WhenInvalidRegistrations_ShouldBeBadRequest` | Exists |
| 400, invalid `registrationYears` | `UT SearchTests.WhenInvalidRegistrationYears_ShouldBeBadRequest` | Exists |
| 400, invalid `statuses` | `UT SearchTests.WhenInvalidStatuses_ShouldBeBadRequest` | Exists |
| 500 | `UT SearchTests.WhenOrganisationServiceThrows_ShouldBeInternalServerError` | **New** |

**`GET /organisations/{id}`**

| Row | Test | State |
|---|---|---|
| 200 | `UT GetTests.WhenOrganisationFound_ShouldBeOk` | Exists |
| 404 | `UT GetTests.WhenOrganisationNotFound_ShouldBeNotFound` | Exists |
| 500 | `UT RequestMetricsTests.WhenOrganisationServiceThrows_ShouldNotBeSwallowed_AndReportFault` | Exists |

**`PUT /organisations/{id}`**

| Row | Test | State |
|---|---|---|
| 200 | `UT PutTests.WhenOrganisation_ShouldUpdate` | Exists |
| 201 | `UT PutTests.WhenNoOrganisation_ShouldCreate` | Exists |
| 400 | `UT PutTests.WhenInvalidRequest_*` (five tests) | Exists |
| 500 | `UT PutTests.WhenOrganisationServiceThrows_ShouldBeInternalServerError` | **New** |

**`PUT /organisations/{id}/registrations/{type}-{registrationYear}`**

| Row | Test | State |
|---|---|---|
| 200 | `UT Registrations.PutTests.WhenOrganisationFound_AndRegistrationExists_ShouldBeUpdated` | Exists |
| 201 | `UT Registrations.PutTests.WhenOrganisationFound_AndRegistrationDoesNotExist_ShouldBeCreated` | Exists |
| 400 | `UT Registrations.PutTests.WhenInvalidRoute_ShouldBeBadRequest` | Exists |
| 500 | `UT Registrations.PutTests.WhenOrganisationServiceThrows_ShouldBeInternalServerError` | **New** |
| `type` = `SMALL_PRODUCER` | the 201 test above | Exists |
| `type` = each of the five types | `UT Registrations.PutTests.WhenRegistrationType_ShouldBeCreated` (theory) | **New** |

**`DELETE /organisations/{id}/registrations/{type}-{registrationYear}`**

| Row | Test | State |
|---|---|---|
| 204 | `UT Registrations.DeleteTests.WhenOrganisationFound_AndRegistrationFound_ShouldBeDeleted` | Exists |
| 404 | `UT Registrations.DeleteTests.WhenOrganisationNotFound_ShouldBeNotFound`, `…AndRegistrationNotFound_ShouldBeNotFound` | Exists |
| 500 | `UT Registrations.DeleteTests.WhenOrganisationServiceThrows_ShouldBeInternalServerError` | **New** |
| `type` = `SMALL_PRODUCER` | the 204 test above | Exists |
| `type` = each of the five types | `UT Registrations.DeleteTests.WhenRegistrationType_ShouldBeDeleted` (theory) | **New** |

### New tests

Eight tests in four existing files. Each asserts the response status code.

| File | Tests |
|---|---|
| `tests/Api.Tests/Endpoints/Organisations/SearchTests.cs` | `WhenRegistrationType_ShouldBeOk` (five cases), `WhenStatus_ShouldBeOk` (two cases), `WhenOrganisationServiceThrows_ShouldBeInternalServerError` |
| `tests/Api.Tests/Endpoints/Organisations/PutTests.cs` | `WhenOrganisationServiceThrows_ShouldBeInternalServerError` |
| `tests/Api.Tests/Endpoints/Organisations/Registrations/PutTests.cs` | `WhenRegistrationType_ShouldBeCreated` (five cases), `WhenOrganisationServiceThrows_ShouldBeInternalServerError` |
| `tests/Api.Tests/Endpoints/Organisations/Registrations/DeleteTests.cs` | `WhenRegistrationType_ShouldBeDeleted` (five cases), `WhenOrganisationServiceThrows_ShouldBeInternalServerError` |

How they work:

- **500 tests** make the mocked `IOrganisationService` throw, as `RequestMetricsTests` already does, and assert 500.
- **Value theories** send one listed value per case. The search theories also assert the value reaches `IOrganisationService.Search`; the registration theories assert the registration written or removed has that type.
- Existing fixtures in `tests/Testing/Fixtures` and URL builders in `tests/Testing/Endpoints.cs` are reused. No new classes or helpers are needed.

## Acceptance criteria

- [ ] Every row in the traceability tables names a test that exists and passes.
- [ ] The eight new tests are added to the four files listed, and no existing test's assertions are changed.
- [ ] The unit suite passes using the commands in `AGENTS.md`, and `dotnet csharpier check .` passes.
- [ ] No file under `src/` is changed.
- [ ] The team has confirmed that the tests belong in this repository (the one item under "To confirm with the team") before work starts.

## Decisions

| Topic | Decision | Basis |
|---|---|---|
| Form of the deliverable | Automated tests | Answered by the team |
| Who does the work | All engineers are working on the story | Answered by the team |
| Where the tests live | This repository's existing unit-test project, run by the pull request pipeline | Our proposal; see "To confirm with the team" |
| 500 rows | Covered by simulating a data-layer failure in-process, because a caller cannot cause a 500 on demand | Our proposal |
| Number of scenarios | One per endpoint and listed code, plus one per listed parameter value | Our proposal |
| Rows that existing tests already assert | Traced to the existing test; no duplicate test is written | Our proposal |
| Registration paths | The paths the API serves are tested, not the ones written in the story | Fact; see suggestion 1 |
| Likely bugs found during analysis | Not tested and not fixed in this story; see suggestion 3 | Our proposal |

## To confirm with the team

One item needs an answer before work starts, because a different answer changes the whole approach.

- **Where the tests live.** We propose this repository's existing unit tests, for three reasons:
  - They run on every pull request, so a change that breaks v1 fails before it merges.
  - They can cover the 500 rows. A suite run against the deployed dev API cannot.
  - 20 of the 28 rows are already asserted here, so only 8 tests are new.

  The story links to the deployed dev API, so someone may have expected a separate suite run against it. If so, this spec needs redoing.

Because several engineers are on the story, agree who takes which rows before starting. All eight new tests go into four files.

## Suggestions for the story owner

None of these blocks the work.

1. **Correct the registration paths in the story.** It shows `/organisations/{id}/{type}--{registrationYear}`. The API serves `/organisations/{id}/registrations/{type}-{registrationYear}`.
2. **Raise a follow-up story for authorisation.** The story's table omits 401 and 403 because the API documentation does. Today only search has tests for them; the other four endpoints have none, so a change that removed the scope check from a write endpoint would not fail any test. The other responses the table omits (400 on `DELETE` registration, 404 on `PUT` registration) are already tested.
3. **Raise one follow-up ticket for the likely bugs.** The analysis found the behaviours below, which look unintended. One ticket to triage them keeps them out of this story without losing them.
   - `PUT /organisations/{id}` with `"address": null` returns 500 instead of 400.
   - `PUT /organisations/{id}` accepts a null or empty `name`.
   - Enum fields accept numbers: `"type": 1` is stored as `LARGE_PRODUCER`, and `PUT …/registrations/1-2025` creates `LARGE_PRODUCER-2025`.
   - Basic credentials that are not valid base64 return 500 instead of 401.
   - A concurrent-update conflict returns 500, so a client cannot tell it from a fault.
   - `/health` runs no dependency checks, so it reports healthy when MongoDB is unreachable.

## Out of scope

- Authentication and authorisation responses (401, 403).
- Routing behaviour: non-GUID ids, unsupported methods (405), unsupported content types (415).
- Health and documentation endpoints.
- Behaviour under concurrent updates, and how search filters combine.
- Response codes and validation rules not listed in the story.
- Fixing or pinning any of the bugs found during analysis.
- Any production code change, and the REGEN-140 versioning decision.
- Creating or updating Jira tickets and Confluence pages.

## Notes

- Work on the branch `REGEN-145-test-scenarios-for-existing-v1-api`, off `main`.

### Verification commands

```bash
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build tests/Api.Tests/Api.Tests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test --test-modules tests/Api.Tests/bin/Debug/net10.0/Api.Tests.dll --no-build -v:minimal
```

```bash
dotnet csharpier check .
```

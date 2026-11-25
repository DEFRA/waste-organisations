# MAV-17: Duplication rules when trying to create an existing organisation

| | |
|---|---|
| Type | Feature: the v2 `POST /organisations` refuses, with `409`, an organisation whose Companies House number is already stored, and Postgres enforces one organisation per Companies House number. Organisations without a Companies House number, the v2 `GET /organisations/{id}` and every v1 endpoint are unchanged |
| Priority | P1 (High): the "one record per organisation" assumption must hold before any service integrates with v2 and stores organisations, and the unique index is cheapest to add while no environment holds v2 data (23 November 2026). Jira shows "Lowest" |
| Parent | MAV-10 Access to org data via Org API |
| Risk | Low (see Risks) |
| Source | Jira story MAV-17 as read on 23 November 2026 (context, business rules, tech considerations, Scenario 1; no comments), and Scenario 1 as updated on 24 November 2026 to match these decisions (Open Question 1); the refinement answers of 23 and 24 November 2026 recorded in Decisions; specs 0003, 0005, 0006 and 0007 |

## Problem/Opportunity

The v2 create stores every valid request as a new organisation. Sending the same company twice gives two organisations with the same Companies House number and two ids, so a consumer cannot tell which one is the company. MAV-17 assumes the API stores one record per organisation and that a Companies House number identifies a company uniquely. This story makes the create keep to that assumption for companies, and points the caller at the record that already exists.

Organisations without a Companies House number (sole traders, charities, partnerships and the other types that spec 0005 lets through without one) are about 10% of users. They are created through the same endpoint and are left to later stories.

What is already in place:

- **The v2 create (spec 0005, as changed by spec 0007):** `src/Api/Endpoints/Organisations/Post.cs`, with the description "Duplicate organisations are not detected." and the responses `201`, `400` and `500`; `CreateOrganisationValidationFilter` runs before the handler.
- **The Companies House number:** `OrganisationRequest.CompaniesHouseNumber` is required for the five types registered at Companies House and must be absent for the other five (`OrganisationTypeCodes.RegisteredAtCompaniesHouse`). It is trimmed and upper-cased by `CreateOrganisationExtensions.ToEntity` and must be two letters or digits followed by six digits. It is stored in `organisation.companies_house_number`.
- **Persistence:** `PostgresOrganisationService.CreateAsync` saves once and retries once on a violation of `ux_address_values`, the only unique index the create can hit today.
- **Schema:** `organisation.companies_house_number` is `text`, nullable, with no index. Spec 0003 left out a unique constraint on purpose ("how duplicates are detected is still being decided"), and spec 0005 decided "Duplicate organisations: Out of scope; no `409`". Changelogs 1.0 to 1.4 are published; the last tag is `v1.4.0`.
- **The read:** `GET /organisations/{id}` (spec 0006) returns an organisation by id, so a caller given the existing id can fetch its details.

Who benefits:

- **RPD and Manage Account:** a company is created once, and a repeat create tells them which organisation already holds the number.
- **The team and operations:** no duplicate companies to reconcile later.

## Proposed Solution

### What counts as a duplicate

A create request is a duplicate when its `companiesHouseNumber`, after the existing normalisation (trimmed, upper-cased), equals the `companies_house_number` of an organisation already stored. Nothing else is compared: the same number with a different name, type, regulator nation or addresses is still a duplicate. A request without a Companies House number is never a duplicate.

Every stored organisation counts, whatever its `valid_to`. No endpoint sets `valid_to` on an organisation today.

### The `409` response

For a duplicate, the v2 create stores nothing and returns:

- Status `409 Conflict`.
- Header `Location: /organisations/{existingId}`, the same form as the `Location` of a `201`.
- A problem details body (`application/problem+json`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Organisation already exists",
  "status": 409,
  "detail": "An organisation with this companiesHouseNumber already exists.",
  "organisationId": "3f2b8c1e-6d4a-4b7e-9a51-2c8e0f7d1a93"
}
```

`organisationId` is the id of the stored organisation. The body does not repeat the organisation's details: the caller reads them with `GET /organisations/{organisationId}` (Decisions, "`409` body"). The Companies House number is not echoed back.

The order of checks is unchanged, with the duplicate check last: `401`, `403`, `406`, `415` and the unreadable-body `400` come first, then validation (`400`), then the duplicate check (`409`). So a caller without the `write` scope learns nothing about which numbers are stored, and an invalid request that also carries a stored number gets `400`.

### Enforcement

- **Look-up before the save.** `CreateAsync` reads the id of the organisation holding the request's number (untracked, served by the new index) before it looks up the addresses. When there is one, nothing is saved.
- **Unique index.** A partial unique index on `organisation.companies_house_number`, `ux_organisation_companies_house_number`, where the number is not null. It stops two concurrent requests that both missed the look-up from storing the same number.
- **A violation is a duplicate.** Two requests with the same new number can both miss the look-up; the first to save stores its organisation, and the second's save fails on `ux_organisation_companies_house_number`. The second request then gets the same `409` it would have got had it arrived a moment later:
  1. The change tracker is cleared, so the organisation, links and addresses of the failed save are not inserted by a later save.
  2. The organisation holding the number, now committed, is looked up again.
  3. `CreateAsync` returns `OrganisationAlreadyExists` with that organisation's id. It does not retry: unlike a clashing address, which a retry can reuse, a clashing number can never be stored.

  `CreateAsync` already saves a second time after a violation of `ux_address_values`, and the number can clash on either save, so both saves are handled this way. If the second look-up finds no organisation (it was deleted between the failed save and the look-up, which no endpoint can do today), the exception is thrown and the caller gets `500`.

`CreateAsync` returns the outcome as a closed set of records, so the handler still calls one service method and matches the result:

```csharp
public abstract record CreateOrganisationResult;
public sealed record OrganisationCreated(Organisation Organisation) : CreateOrganisationResult;
public sealed record OrganisationAlreadyExists(Guid ExistingId) : CreateOrganisationResult;
```

The handler returns `TypedResults.Created(...)` for `OrganisationCreated`. For `OrganisationAlreadyExists` it returns the `409` above, with the `Location` header set and `organisationId` as a problem details extension.

### Logging

One `Information` entry for a refused duplicate: "Create refused: organisation '{OrganisationId}' already holds the Companies House number", with the existing id only. The number and the request body are not logged (`rules/shared/security-logging.md`).

### Schema: Liquibase changelog

A new file, `changelog/db.changelog-1.5.xml`, is included at the end of `changelog/db.changelog.xml`. No published changeset is edited.

| Changeset | Change | Rollback |
|---|---|---|
| `1.5-ux-organisation-companies-house-number` | `sql`: `CREATE UNIQUE INDEX ux_organisation_companies_house_number ON organisation (companies_house_number) WHERE companies_house_number IS NOT NULL;` | `DROP INDEX ux_organisation_companies_house_number;` |
| `1.5-tag-v1.5.0` | `tagDatabase` `v1.5.0` | Automatic |

The changelog does not touch stored rows. If an environment already holds two organisations with the same number, Liquibase fails the deployment and the team resolves it by hand (Decisions, "Existing data"). Run `dotnet csharpier check .` locally after editing the changelog.

The entity follows it: `OrganisationConfiguration` gains `HasIndex(o => o.CompaniesHouseNumber).IsUnique().HasFilter("companies_house_number IS NOT NULL").HasDatabaseName("ux_organisation_companies_house_number")`.

### OpenAPI

`/openapi/v2.json`: the `CreateOrganisation` operation gains a `409` problem details response. Its description replaces "Duplicate organisations are not detected." with a sentence saying that a Companies House number already stored gives `409` with the existing organisation's id in `organisationId` and in `Location`, and that organisations without a number are not checked. `OpenApiTests.OpenApiV2_VerifyAsExpected` is re-verified in slice 2 after its diff is read. `/openapi/v1.json` does not change.

### Code layout

| Path | Slice | Change |
|---|---|---|
| `changelog/db.changelog-1.5.xml`, `db.changelog.xml` | 1 | As above |
| `src/Api/Data/Postgres/Entities/Organisation.cs` | 1 | The unique filtered index |
| `tests/Testing` (new `CompaniesHouseNumbers.cs`) | 1, 2 | Named constants, one per integration test or theory case that stores a v2 organisation with a number, each distinct from every other. They use the accepted format, two letters or digits followed by six digits, and cover both forms (for example `12345678`, `SC123456`, `OC123456`). Slice 2 adds the constants its tests need |
| `src/Api/Services/CreateOrganisationResult.cs` | 2 | New: the three records |
| `src/Api/Services/IPostgresOrganisationService.cs`, `PostgresOrganisationService.cs` | 2 | `CreateAsync` returns `CreateOrganisationResult`; the look-up, the violation handling and the log entry of "Enforcement" and "Logging" |
| `src/Api/Endpoints/Organisations/Post.cs` | 2 | Matches the result; `.ProducesProblem(StatusCodes.Status409Conflict)`; the new description |
| `README.md` | 2 | The "API v2" paragraph names this spec; "Postgres schema changes" names changelog 1.5 (slice 1) |

No v1 endpoint, DTO, service or Mongo index changes. No NuGet package is added. A comment that records one of these decisions points at "spec 0008, Decisions".

### Tests

`*.verified.*` snapshots are frozen artefacts. The only snapshot that changes on purpose is `OpenApiTests.OpenApiV2_VerifyAsExpected`, in slice 2, after its diff shows the `409` response and the new description and nothing else. A difference anywhere else, including in `tests/Api.V1.Tests`, means stop and ask. Test data is synthetic.

Test numbers are constants, so the tests stay deterministic (Decisions, "Test data"). The test environments are cleared and rebuilt between runs, so a number only has to be distinct within one run. Today several integration tests store organisations with the same fixed number (`12345678`, `SC123456`, `OC123456`), which the index would reject. From slice 1 on, every integration test, and every case of a theory, that stores a v2 organisation with a number takes its own constant from `CompaniesHouseNumbers`. The constraints already ask Postgres tests to create their own distinct data. `CreateOrganisationTests.FullPayload_ShouldCreateOrganisationAndRows` keeps `12345678`, which no other test then uses, so its snapshot does not change. A test that sends a duplicate on purpose reuses its own constant. A test or theory case that stores two organisations takes one constant for each. A test whose row a constraint rejects also takes its own constant, so the unique index cannot reject that row before the constraint under test does (assumption of the slice 1 plan, 25 November 2026). `CreateOrganisationErrorTests.Created_ShouldNotBeFoundByV1Get` stores an organisation with `12345678` too, so it moves onto its own constant in slice 1.

| Project | Slice | Test |
|---|---|---|
| `Api.Tests` (`Data/PostgresDbContextModelTests.cs`) | 1 | `organisation` has the unique index `ux_organisation_companies_house_number` on `companies_house_number` with its filter |
| `Api.IntegrationTests` (`Scenarios/DatabaseChangelogTests.cs`) | 1 | The `v1.5.0` tag |
| `Api.IntegrationTests` (`Scenarios/OrganisationSchemaTests.cs`) | 1 | A second `organisation` row with a stored number is rejected by `ux_organisation_companies_house_number`; two rows with a null number are accepted |
| `Api.IntegrationTests` (`Scenarios/CreateOrganisationTests.cs`, `CreateOrganisationErrorTests.cs`, `GetOrganisationDetailsTests.cs`, `Services/PostgresOrganisationServiceTests.cs`, `Scenarios/OrganisationSchemaTests.cs`) | 1 | Shared fixed numbers replaced by distinct `CompaniesHouseNumbers` constants, the snapshot test keeping `12345678`; every test passes on a freshly built stack |
| `Api.Tests` (`Services/PostgresOrganisationServiceTests.cs`) | 2 | A stored number gives `OrganisationAlreadyExists` with its id and saves nothing; a violation of `ux_organisation_companies_house_number` gives `OrganisationAlreadyExists` after a fresh look-up; the same violation on the save after the `ux_address_values` retry does too; a violation whose holder is gone is thrown; no number gives `OrganisationCreated` without a look-up |
| `Api.Tests` (`Endpoints/Organisations/PostTests.cs`) | 2 | `OrganisationAlreadyExists` gives `409`, the problem details body, `organisationId` and the `Location` header; an invalid request gives `400` without calling the service; a read-only caller gets `403` |
| `Api.Tests` (`Endpoints/OpenApi/OpenApiTests.cs`) | 2 | v2 snapshot re-verified as above; v1 snapshot unchanged |
| `Api.IntegrationTests` (new `Scenarios/DuplicateOrganisationTests.cs`) | 2 | The Jira scenario end to end: create, then the same request: `409`, body, `organisationId` and `Location` equal the first `201`'s id, and the `organisation`, `organisation_address` and `address` row counts are unchanged. A number sent in lower case with spaces round it (for example ` sc654321 `) after its upper-case form: `409`. Every number in this class is its own `CompaniesHouseNumbers` constant. The same number with a different name, type and address: `409`, and a `GET` of the stored organisation returns what the first `201` returned. Two identical requests for a type without a number: two `201`s with different ids. Ten concurrent requests with one new number: one `201`, nine `409`s naming its id, one `organisation` row |
| `Api.V1.Tests` | all | No test or snapshot changes; every test passes as it is |

### Work breakdown

Two pull requests: the constraints keep a schema change apart from behaviour, and the author chose the index first (23 November 2026). Estimates in non-generated changed lines: about 300 and 500.

| Slice | Work | Verified by |
|---|---|---|
| 1 | Index. `db.changelog-1.5.xml` and its include; the unique filtered index on the `Organisation` entity; `CompaniesHouseNumbers` and the integration tests moved onto its distinct constants; the schema, changelog and model tests; the README names changelog 1.5. Stops there: no service, endpoint or OpenAPI change. A duplicate create now fails on the index and gets the global handler's `500` until slice 2 | `Api.Tests`; `Api.IntegrationTests` against Compose, where Liquibase applies `v1.5.0` and the existing create and read tests pass; `Api.V1.Tests`. Criteria 8 and 11 |
| 2 | `409`. "What counts as a duplicate", "The `409` response", "Enforcement", "Logging" and "OpenAPI": `CreateOrganisationResult`, the service and handler changes, the v2 OpenAPI snapshot, the README paragraph, and every slice 2 test in Tests | `Api.Tests`, `Api.IntegrationTests` and `Api.V1.Tests`; the v1 OpenAPI snapshot passes with no diff. Criteria 1 to 7, 9, 10 and 11 |

## Acceptance Criteria

- [ ] 1. A v2 `POST /organisations` whose Companies House number is not stored returns `201` and stores the organisation, as before.
- [ ] 2. A v2 `POST /organisations` whose Companies House number is already stored returns `409` with problem details titled "Organisation already exists", `organisationId` set to the stored organisation's id and `Location: /organisations/{that id}`; no `organisation`, `organisation_address` or `address` row is added or changed (Jira Scenario 1).
- [ ] 3. The number is compared after trimming and upper-casing: ` sc123456 ` is a duplicate of a stored `SC123456`.
- [ ] 4. The number alone decides: a request with a stored number and a different name, type, regulator nation or addresses returns `409`, and the stored organisation is unchanged.
- [ ] 5. Organisations without a Companies House number are not checked: two identical requests for a type not registered at Companies House both return `201` with different ids.
- [ ] 6. Validation comes first: an invalid request that carries a stored number returns `400` with its validation errors, not `409`.
- [ ] 7. Concurrent requests with the same new number store one organisation: one returns `201`, and every other returns `409` naming that organisation's id.
- [ ] 8. Changelog `db.changelog-1.5.xml` adds `ux_organisation_companies_house_number`, unique on `organisation.companies_house_number` where it is not null, and tags `v1.5.0`. The database rejects a second row with a stored number and accepts any number of rows without one. Changelogs 1.0 to 1.4 are unchanged and no EF Core migration exists.
- [ ] 9. A caller without valid credentials still gets `401` and one without the `write` scope `403`, also when the number is stored; a version above 2 still gets `406`.
- [ ] 10. `/openapi/v2.json` documents the `409` response of `POST /organisations` and no longer says duplicates are not detected; the refused create is logged with the existing id and without the Companies House number.
- [ ] 11. A request that does not ask for version 2 behaves as before: `Api.V1.Tests` passes with no test or snapshot changed and `/openapi/v1.json` is unchanged. After every slice the full gate passes and no NuGet package is added.

## Out of Scope

- Duplicate detection for organisations without a Companies House number (sole traders, partnerships, charities and the other types without one): later stories, as the Jira story says.
- Checking a Companies House number against Companies House (that it exists, is active or matches the name).
- Updating the stored organisation with the details of a duplicate request, or reporting how they differ.
- Returning the existing organisation's details in the `409` body (Decisions, "`409` body").
- Matching on name, trading name or address, and fuzzy matching.
- Dissolved or ended companies, and a number reused after one ends.
- v1 endpoints and their data in MongoDB.
- Duplicates within registrations, and the v1 `PUT /organisations/{id}`.
- Resolving duplicates already stored in an environment: none exists (Decisions, "Existing data").

## Decisions

All taken in refinement on 23 November 2026 unless a date is given.

| Topic | Decision |
|---|---|
| Duplicate key | The normalised Companies House number alone, as the story assumes the number is unique. The scenario's "same details" is read as "same company". This supersedes spec 0005 in its Decision "Duplicate organisations" and in its criterion that no `409` or duplicate check exists, and spec 0003 in "No unique constraint on name, Companies House number or any combination", for the Companies House number only. Neither spec is edited |
| Status | `409`, as the story asks. The stand-up idea that spec 0003 recorded (a duplicate returns the matching organisation with `200`) is not taken |
| `409` body | Problem details with the existing organisation's id in `organisationId` and in the `Location` header, not its details. The story asks for "a payload containing details of the existing organisation"; the author chose the id only, and the caller fetches the details with `GET /organisations/{id}`. The story's Scenario 1 was updated to match on 24 November 2026 (Open Question 1) |
| Enforcement | A look-up before the save, and the partial unique index `ux_organisation_companies_house_number` for requests that race. A violation of the index is a duplicate, not a retry |
| Index scope | Every organisation row with a number, whatever its `valid_to`, because nothing ends an organisation yet. A later story that ends organisations decides whether an ended one still blocks its number |
| Existing data | The changelog does not change rows. Production holds no v2 data (author, 23 November 2026) and the other environments have been cleared (author, 24 November 2026). An environment that somehow held a duplicate number would fail the deployment loudly rather than lose data silently |
| Check order | Authentication, authorisation, version and body checks, then validation, then the duplicate check. A caller without the `write` scope cannot probe which numbers are stored |
| No number echoed | The `409` names the existing id, not the number, and the log entry likewise; the caller already knows the number it sent |
| Service result | `CreateAsync` returns `CreateOrganisationResult` (`OrganisationCreated` or `OrganisationAlreadyExists`) rather than throwing, so the handler still calls one service method and a duplicate is not an exception |
| Version | v2 changes in place: no service consumes v2 yet and only v1 is frozen. The story's "all solutions built will need to be versioned" is met by v1 staying as it is, as for spec 0007 |
| Slices | Index first, then behaviour (author's choice). Between the two deployments a duplicate create gets `500` instead of a second organisation |
| Test data | Constant numbers, one distinct constant per integration test or theory case, declared together in `CompaniesHouseNumbers` so that any clash is visible in one file. Tests stay deterministic, and the test environments are cleared and rebuilt between runs, so nothing random or run-specific is needed (author, 24 November 2026). No snapshot changes for test data |
| Priority | P1 in this spec. The Jira priority stays "Lowest": the author chose not to change it (24 November 2026) |
| Confluence | No change to the page "Organisations API - waste-organisations": it does not document responses at the level of a `409` (author, 24 November 2026) |

## Open Questions

1. ~~**Jira wording.**~~ Done on 24 November 2026: Scenario 1 now reads "with the same Companies House number" and asks for "the id of the existing organisation, with a Location header pointing to it". The Jira priority was left as it is (Decisions, "Priority").
2. ~~**Confluence.**~~ Not needed (Decisions, "Confluence").
3. ~~**Non-production environments.**~~ Done: production holds no v2 data (author, 23 November 2026), and the other environments have been cleared (author, 24 November 2026), so no environment holds two organisations with the same Companies House number when changelog 1.5 is applied.

No question is open.

## Dependencies

- MAV-11 (spec 0005), MAV-12 (spec 0006) and MAV-42 (spec 0007), all merged: the v2 create and read this story changes, and changelog 1.4, which 1.5 follows.
- Nothing else has to land first. Later stories add duplicate rules for organisations without a Companies House number.
- The slices deploy in order, and slice 2 should follow slice 1 closely (Risks).

## Risks

| Risk | Level | Mitigation |
|---|---|---|
| An environment already holds two organisations with the same number, and the 1.5 deployment fails | Low | No v2 data in production (author, 23 November 2026) and the other environments cleared (author, 24 November 2026); the failure would be loud and nothing would be lost |
| Between slices 1 and 2 a duplicate create gets `500` instead of `409` | Low | No service consumes v2 yet; slice 2 follows directly; the pull request of slice 1 says so in "How to test" |
| Integration tests collide on shared numbers once the index exists | Medium | Slice 1 gives each test its own constant in `CompaniesHouseNumbers` and runs the integration tests against a freshly built stack; a clash fails loudly on the index |
| A company is legitimately created twice, for example by two services before either knows of the other | Low | That is the intended outcome: the second gets `409` and the existing id |
| The caller wanted the details in the `409`, as the story said | Low | Decided by the author; one `GET` gives them; Open Question 1 updates the story |
| The `ux_address_values` retry and the new violation interact wrongly | Low | Both paths have unit tests (Tests, slice 2), and the concurrency test runs against real Postgres |

## Notes

- Branches: `MAV-17-duplicate-organisations-slice-1`, `-slice-2`.
- The Jira title spells "exisiting"; this spec uses "existing".
- Synthetic numbers in examples and fixtures: `12345678`, `SC123456`, `OC123456` and the other constants in `CompaniesHouseNumbers`. A Companies House number is two letters or digits followed by six digits, as `OrganisationRequest` already checks.
- The pull request of slice 1 says in "How to test" that changelog 1.5 must be applied and that it fails if two organisations already share a number.

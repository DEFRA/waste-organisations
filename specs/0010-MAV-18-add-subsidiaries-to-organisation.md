# MAV-18: Add Subsidiaries to Organisation

| | |
|---|---|
| Type | Feature: a v2 `POST /organisations/{id}/subsidiaries/{service}` that links organisations already stored to a parent organisation as its subsidiaries in a service, and a schema change (`db.changelog-1.6.xml`) that adds the tables `subsidiary` and `organisation_lifecycle`. The existing v2 endpoints and every v1 endpoint are unchanged |
| Priority | P2 (Medium), as Jira shows: the story is In Progress in MAV-10, and the stories that read subsidiaries (MAV-19, MAV-20) build on its tables |
| Parent | MAV-10 Access to org data via Org API |
| Risk | Medium (see Risks): five contract questions are open and are to be answered at the review of this spec |
| Source | Jira story MAV-18 as read on 26 November 2026 (context, business rules, Scenarios 1 to 4, 6 and 7; one comment, unanswered); the "Add Subsidiaries to Organisation" section of the Confluence page "Organisations API - waste-organisations" and the "Data payloads" table of "Access to org data via Org API", both as read on 26 November 2026; the domain model on the Miro board (`uXjVHUfA4ZE=`, frame "Waste Organisation service", tables SUBSIDIARY and ORGANISATION_LIFECYCLE) as read on 27 November 2026; the Manage Account architecture site as read on 27 November 2026 (ADR-003, ADR-011, ADR-016, ADR-159, "Data Structure View", "Waste Organisations API"); the Packaging EPR prototype flow "Manage subsidiaries (producer)" as read on 27 November 2026; the "Subsidiaries" section of the user-centred design Mural board "Manage Account - UCD board" (the UCD board: the session of 25 November 2026 on today's RPD journeys, their problems, helpdesk requests and user needs) as read on 27 November 2026; Slack messages from Darren Walker, undated, relayed by the author on 27 November 2026; the author's answers of 27 November 2026 recorded in Decisions; specs 0003, 0005, 0006, 0008 and 0009 |

## Problem/Opportunity

Packaging producers register as groups: a holding company reports for its subsidiaries. Those relationships live in the Azure account service today. The v2 Organisations API can store an organisation (spec 0005) and read it back (spec 0006), but it holds no relationship between two organisations, so it cannot yet be the source of truth for a group. MAV-18 adds the first relationship: which organisations are subsidiaries of which, per service.

What is already in place:

- **The v2 create and read:** `POST /organisations` and `GET /organisations/{id}` (specs 0005, 0006, 0007 and 0008), with `IPostgresOrganisationService` and the tables `organisation`, `address`, `organisation_address`, `organisation_type` and `address_usagetype`.
- **Versioning (spec 0004):** `.WithApiVersions(2)`, `ApiVersionMatcherPolicy` and `ApiVersioningMiddleware`.
- **Validation:** an endpoint filter and validator pair that keys every error by JSON path (`CreateOrganisationValidationFilter`, `CreateOrganisationRequestValidator`).
- **Schema:** changelogs 1.0 to 1.5 are published; the last tag is `v1.5.0`. No table holds a relationship between organisations. Spec 0003 left `subsidiary` and `organisation_lifecycle` out of scope.
- **Authentication:** Basic or JWT bearer, scopes from the ACL, `PolicyNames.Write` for writes.

Who benefits:

- **RPD Submit Data:** records the subsidiaries a registration submission reports, with the date each joined.
- **Meet Obligations and the calculators:** later read the group structure and its dates from one place (MAV-19, MAV-20).
- **Manage Account:** the group structure it will display comes from the same store.

Three things in the story are read differently here (Decisions):

- "Create and link a new subsidiary": the subsidiary is an organisation, created with `POST /organisations`. This endpoint links it.
- "As this endpoint already exists": no v1 endpoint has this path, so it is a new v2-only endpoint.
- Message bodies on `401` and `404`: they stay empty, as on the other v2 endpoints.

## Proposed Solution

Add a v2-only `POST /organisations/{id}/subsidiaries/{service}`. It validates the body, checks that the parent and every subsidiary organisation exist, stores one `subsidiary` row and one `organisation_lifecycle` row for each new link in one transaction, and returns `201` with the links. Open Questions 1 to 5 are not answered yet; the spec is written on the answer proposed for each, so that the criteria are testable, and each question names what changes if the answer differs.

### The request

`POST /organisations/{id}/subsidiaries/{service}` with `Accept: application/vnd.defra.waste-organisations+json; version=2`, `Content-Type: application/json` and credentials with the `write` scope.

| Part | Rule |
|---|---|
| `id` | The id of the parent organisation, on the route constraint `{id:guid}` |
| `service` | A code from `WasteServiceCodes.All`, which holds `rpd` only. Matched without regard to case, so `RPD` is accepted; stored as `rpd` |

```json
{
  "subsidiaries": [
    { "organisationId": "0199c0f6-1f7a-7c8e-9f3a-5b2d8c4e7a10", "joinerDate": "2026-09-01" },
    { "organisationId": "0199c0f6-2a3b-7d4e-8f5a-6c7d8e9f0a1b", "joinerDate": "2026-10-15" }
  ]
}
```

| Field | Required | Rule |
|---|---|---|
| `subsidiaries` | Yes | One to 100 items. An `organisationId` appears in one item only |
| `organisationId` | Yes | A GUID: the id of a stored organisation, which becomes the subsidiary. Not the `id` of the path |
| `joinerDate` | Yes | A calendar date in the form `YYYY-MM-DD`: the date the subsidiary joined the parent. No range is checked: no source gives one |

`organisationId` and `joinerDate` are bound as strings and checked by the validator, so a value in the wrong format is a validation error under its JSON path and not an unreadable body, as spec 0005 does for `organisationType`. Unknown members are ignored, as on the v2 create.

### The response

`201 Created`, `Content-Type: application/json`, no `Location` header:

```json
{
  "subsidiaries": [
    { "organisationId": "0199c0f6-1f7a-7c8e-9f3a-5b2d8c4e7a10", "joinerDate": "2026-09-01" },
    { "organisationId": "0199c0f6-2a3b-7d4e-8f5a-6c7d8e9f0a1b", "joinerDate": "2026-10-15" }
  ]
}
```

- One item for each item of the request, in request order, with the joiner date now stored for the link.
- `rpdExternalSubsidiaryId` is added to an item when its link has a `public_subsidiary_id`, and omitted when it has none. This story stores none (Open Question 1), so the member is absent for every link the endpoint creates.
- Every success is `201`, also when the request changed nothing (Jira Scenario 4).

### Outcomes and errors

Checks run in this order. Nothing is stored unless every item succeeds.

| Case | Status | Body |
|---|---|---|
| `id` is not a GUID, or `service` is not in `WasteServiceCodes.All` | `404` | Empty, from routing: the route constraints match no endpoint, with or without credentials (Jira Scenario 6) |
| `version=2` is not requested: no `Accept` version, or `version=1` | `404` | Empty, from routing: v1 has no endpoint on this path |
| No or invalid credentials | `401` | Empty (Jira Scenario 7) |
| Credentials without the `write` scope | `403` | Empty |
| A version above 2, or a malformed one | `406` | `detail: "Unsupported API version"` (spec 0004) |
| A `Content-Type` other than JSON | `415` | Framework default |
| Malformed JSON, a value of the wrong JSON type, or an empty body | `400` | Problem details with a `detail`, as the v2 create |
| `subsidiaries` missing, empty or over 100 items; `organisationId` missing, not a GUID, repeated, or equal to the path `id`; `joinerDate` missing or not a `YYYY-MM-DD` date | `400` | Validation problem details keyed by JSON path, for example `subsidiaries[1].joinerDate`. Every failing field is reported (Jira Scenarios 2 and 3). A repeated `organisationId` is reported under the later item |
| No `organisation` row has the path `id` | `404` | Empty (the missing Jira Scenario 5) |
| No `organisation` row has an item's `organisationId` | `400` | Validation problem details with `subsidiaries[i].organisationId` for each such item: "No organisation has this id." |
| The subsidiary has no current link in the service | `201` | A new link is stored (Jira Scenario 1) |
| The subsidiary has a current link to this parent with the same `joinerDate` | `201` | Nothing changes (Jira Scenario 4; Open Question 4) |
| The subsidiary has a current link to this parent with another `joinerDate` | `201` | The stored joiner date becomes the request's (Open Question 4) |
| The subsidiary has a current link to another parent in the service, or the link would close a circle | `409` | Problem details, below |
| An unexpected failure | `500` | Problem details, as every other error |

A current link is a `subsidiary` row with no `valid_to`. No endpoint ends a link yet; a link ended in the database does not count, so its subsidiary can be added again, to any parent.

A link would close a circle when the subsidiary is an ancestor of the parent in that service: walking up from the parent through current links reaches the subsidiary (ADR-003: no circular references, enforced at write time).

The `409` body lists every item that cannot be added:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Subsidiaries cannot be added",
  "status": 409,
  "detail": "One or more subsidiaries cannot be added to this organisation.",
  "subsidiaries": [
    {
      "organisationId": "0199c0f6-1f7a-7c8e-9f3a-5b2d8c4e7a10",
      "reason": "HAS_ANOTHER_PARENT",
      "parentOrganisationId": "0199c0f5-9d8c-7b6a-8e4d-3c2b1a0f9e8d"
    },
    { "organisationId": "0199c0f6-2a3b-7d4e-8f5a-6c7d8e9f0a1b", "reason": "CIRCULAR_RELATIONSHIP" }
  ]
}
```

`parentOrganisationId` is the subsidiary's current parent, which the caller needs in order to end that link first. An item with another parent is reported as `HAS_ANOTHER_PARENT` and is not also checked for a circle.

### Persistence

A new `ISubsidiaryService` with `SubsidiaryService` (`src/Api/Services`) holds the rules; `PostgresOrganisationService` is not extended. Its one method, `AddAsync(Guid organisationId, string service, IReadOnlyList<SubsidiaryToAdd> subsidiaries, CancellationToken cancellationToken)`, returns the outcome as a closed set of records, as `CreateOrganisationResult` does:

```csharp
public abstract record AddSubsidiariesResult;
public sealed record SubsidiariesAdded(IReadOnlyList<Subsidiary> Subsidiaries) : AddSubsidiariesResult;
public sealed record ParentOrganisationNotFound : AddSubsidiariesResult;
public sealed record SubsidiaryOrganisationsNotFound(IReadOnlyList<Guid> OrganisationIds) : AddSubsidiariesResult;
public sealed record SubsidiaryConflicts(IReadOnlyList<SubsidiaryConflict> Conflicts) : AddSubsidiariesResult;
```

`SubsidiaryConflicts` arrives with slice 3. The handler calls `AddAsync` once and matches the result.

- **Reads**, untracked, through EF Core LINQ: whether the parent exists; which of the requested ids exist (one query); the current links of the requested subsidiaries in the service, each with its current lifecycle row (one query); and, for the circle check, the current parent of one organisation at a time, walking up from the parent.
- **Writes**, in one `SaveChangesAsync`: for each new link a `subsidiary` row (`organisation_id` the parent, `subsidiary_organisation_id` the subsidiary, `service`, no `public_subsidiary_id`) and an `organisation_lifecycle` row (`organisation_id` the parent, `subsidiary_id` the new row, `joiner_date`; the change columns null). For a changed date, `joiner_date` of the link's current lifecycle row is updated in place. Ids are `Guid.CreateVersion7()`; `valid_from`, `created_at` and `updated_at` come from `TimestampInterceptor`.
- **Concurrency.** Two requests that add the same new link both miss the look-up, and the later save fails on `ux_subsidiary_current`. The service clears the change tracker and evaluates the whole request once more against the committed rows, so it ends as "nothing changes", a changed date or a `409`; a second failure is thrown. Two requests that change the date of one link at the same time: the later one wins.

Nothing is written to MongoDB. One `Information` entry is logged for a success and one for a `409`, with the parent's id, the service and the number of items only.

### Schema: Liquibase changelog

A new file, `changelog/db.changelog-1.6.xml`, is included at the end of `changelog/db.changelog.xml`. No published changeset is edited. The tables follow the Miro board, with the conventions of spec 0003 (snake_case, `text`, `timestamptz`, ids set by the application, `ON DELETE RESTRICT`, no database defaults) and the additions in Decisions.

`subsidiary`: one row for each link between a parent and a subsidiary in a service.

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `uuid` | No | Primary key. The board's `SubsidiaryId` |
| `organisation_id` | `uuid` | No | The parent. `fk_subsidiary_organisation` |
| `subsidiary_organisation_id` | `uuid` | No | The organisation that is the subsidiary. `fk_subsidiary_subsidiary_organisation`. Not on the board |
| `service` | `text` | No | `ck_subsidiary_service`: `service IN ('rpd')`. Not on the board |
| `public_subsidiary_id` | `text` | Yes | The board's `PublicSubsidiaryId`. Not written by this story |
| `valid_from` | `timestamptz` | No | |
| `valid_to` | `timestamptz` | Yes | `ck_subsidiary_valid_to`: null or after `valid_from` |
| `created_at`, `updated_at` | `timestamptz` | No | |

`organisation_lifecycle`: the dated history of an organisation. This story writes the joiner date of a subsidiary link.

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `uuid` | No | Primary key. The board's `LifecycleId` |
| `organisation_id` | `uuid` | No | The parent of the link (Open Question 6). `fk_organisation_lifecycle_organisation` |
| `subsidiary_id` | `uuid` | Yes | The link the row is about. `fk_organisation_lifecycle_subsidiary`. Always set by this story |
| `joiner_date` | `date` | No | |
| `change_date` | `date` | Yes | Not written by this story |
| `change_code` | `text` | Yes | Not written by this story |
| `organisational_change_reason` | `text` | Yes | Not written by this story |
| `valid_from` | `timestamptz` | No | |
| `valid_to` | `timestamptz` | Yes | `ck_organisation_lifecycle_valid_to`: null or after `valid_from` |
| `created_at`, `updated_at` | `timestamptz` | No | |

| Changeset | Change | Rollback |
|---|---|---|
| `1.6-create-subsidiary` | `createTable` | Automatic |
| `1.6-create-organisation-lifecycle` | `createTable` | Automatic |
| `1.6-fk-...` (four) | The four foreign keys above, `onDelete="RESTRICT"` | Automatic |
| `1.6-ix-...` (four) | `ix_subsidiary_organisation_id`, `ix_subsidiary_subsidiary_organisation_id`, `ix_organisation_lifecycle_organisation_id`, `ix_organisation_lifecycle_subsidiary_id` | Automatic |
| `1.6-ck-subsidiary-valid-to`, `1.6-ck-organisation-lifecycle-valid-to` | `sql`: the two `valid_to` checks | Drop the constraint |
| `1.6-ck-subsidiary-service` | `sql`: `CHECK (service IN ('rpd'))` | Drop the constraint |
| `1.6-ck-subsidiary-not-self` | `sql`: `CHECK (organisation_id <> subsidiary_organisation_id)` | Drop the constraint |
| `1.6-ux-subsidiary-current` | `sql`: `CREATE UNIQUE INDEX ux_subsidiary_current ON subsidiary (subsidiary_organisation_id, service) WHERE valid_to IS NULL;` One current parent for a subsidiary in a service | `DROP INDEX` |
| `1.6-ux-organisation-lifecycle-subsidiary-current` | `sql`: `CREATE UNIQUE INDEX ux_organisation_lifecycle_subsidiary_current ON organisation_lifecycle (subsidiary_id) WHERE subsidiary_id IS NOT NULL AND valid_to IS NULL;` One current lifecycle row for a link | `DROP INDEX` |
| `1.6-tag-v1.6.0` | `tagDatabase` `v1.6.0` | Automatic |

Run `dotnet csharpier check .` locally after editing the changelog. If another story publishes changelog 1.6 first, this one takes the next number.

### Code layout

| Path | Slice | Change |
|---|---|---|
| `changelog/db.changelog-1.6.xml`, `db.changelog.xml` | 1 | As above |
| `src/Api/Data/Postgres/Entities/Subsidiary.cs`, `OrganisationLifecycle.cs` | 1 | New entities with their configurations, both `IValidFrom` and `ITimestamped`; `joiner_date` and `change_date` are `DateOnly`; the two unique filtered indexes are declared so the model matches the schema |
| `src/Api/Data/PostgresDbContext.cs` | 1 | `Subsidiaries` and `OrganisationLifecycles`, the two configurations, and the tables named in the summary |
| `src/Api/Data/Postgres/WasteServiceCodes.cs` | 1 | `Rpd = "rpd"` and `All`, beside `OrganisationTypeCodes`. A service added later is one entry and one changeset |
| `src/Api/Dtos/` | 2 | `AddSubsidiariesRequest` (`subsidiaries`), `SubsidiaryRequest`, `AddSubsidiariesResponse` (`subsidiaries`), `SubsidiaryResponse` |
| `src/Api/Dtos/Validation/AddSubsidiariesRequestValidator.cs` | 2 | The body rules of "Outcomes and errors", keyed by JSON path |
| `src/Api/Endpoints/Organisations/Subsidiaries/Post.cs` | 2 | `MapSubsidiariesPost`: `MapPost("/organisations/{id:guid}/subsidiaries/{service:wasteService}", Handle)` with `.WithName("AddSubsidiaries")`, `.WithTags("Subsidiaries")`, summary and description, `.Produces<AddSubsidiariesResponse>(201)`, `.ProducesValidationProblem()`, `.Produces(404)`, `.ProducesProblem(500)`, `.RequireAuthorization(PolicyNames.Write)`, `.WithApiVersions(2)`, `.DisableValidation()` and `.AddEndpointFilter<AddSubsidiariesValidationFilter>()`. Slice 3 adds `.ProducesProblem(409)` |
| `src/Api/Endpoints/Organisations/Subsidiaries/AddSubsidiariesValidationFilter.cs` | 2 | Runs the validator with the path `id`, so the handler sees a valid body only |
| `src/Api/Endpoints/Organisations/Subsidiaries/WasteServiceRouteConstraint.cs` | 2 | The inline constraint `wasteService`: matches a value in `WasteServiceCodes.All` without regard to case. Registered in `RouteOptions.ConstraintMap` |
| `src/Api/Endpoints/Organisations/Subsidiaries/AddSubsidiariesSchemaTransformer.cs` | 2 | v2 document: `format: uuid` and `format: date` on the two members, `minItems` 1 and `maxItems` 100 on `subsidiaries`, and the `service` values |
| `src/Api/Mapping/SubsidiaryExtensions.cs` | 2 | Request items to `SubsidiaryToAdd`; stored links to the response |
| `src/Api/Services/ISubsidiaryService.cs`, `SubsidiaryService.cs`, `AddSubsidiariesResult.cs` | 2, 3 | Slice 2: the parent and subsidiary look-ups and new links. Slice 3: the rules for links that exist, the circle check, `SubsidiaryConflicts` and the retry |
| `src/Api/Endpoints/Endpoints.cs`, `src/Api/Program.cs` | 2 | `MapSubsidiariesPost()`; the service and the route constraint registered |
| `README.md` | 1, 2, 3 | "Postgres schema changes" names changelog 1.6 (slice 1); the "API v2" paragraph names the endpoint and this spec (slice 2) and the `409` (slice 3) |

No v1 endpoint, DTO, service or Mongo index changes. No NuGet package is added. A comment that records one of these decisions points at "spec 0010, Decisions".

### OpenAPI

`/openapi/v2.json` gains `POST /organisations/{id}/subsidiaries/{service}`: the two path parameters, the request and `201` schemas, the `400` and `404` responses, and the `Accept` header and `406` that `AcceptHeaderOperationTransformer` adds. Slice 3 adds the `409`. `OpenApiTests.OpenApiV2_VerifyAsExpected` is re-verified in slices 2 and 3 after its diff is read. `/openapi/v1.json` does not change.

### Tests

`*.verified.*` snapshots are frozen artefacts. The only existing snapshot that changes on purpose is `OpenApiTests.OpenApiV2_VerifyAsExpected`, in slices 2 and 3. Test data is synthetic. Integration tests create their own organisations through the v2 `POST /organisations`, with Companies House numbers from `CompaniesHouseNumbers.Generate` or a type that has none.

| Project | Slice | Test |
|---|---|---|
| `Api.Tests` (`Data/PostgresDbContextModelTests.cs`) | 1 | The two tables, their columns and the two unique filtered indexes |
| `Api.IntegrationTests` (`Scenarios/DatabaseChangelogTests.cs`) | 1 | The `v1.6.0` tag |
| `Api.IntegrationTests` (new `Scenarios/SubsidiarySchemaTests.cs`) | 1 | A link and its lifecycle row are stored and read back through `PostgresDbContext` with their timestamps set. A second current link for the same subsidiary and service is rejected by `ux_subsidiary_current`, and accepted once the first has a `valid_to`. A row whose parent is its subsidiary is rejected. `ck_subsidiary_service` accepts every code in `WasteServiceCodes.All` and rejects `dwt`. A second current lifecycle row for one link is rejected. An organisation with a link cannot be deleted |
| `Api.Tests` (new `Dtos/AddSubsidiariesRequestValidatorTests.cs`) | 2 | Each body rule with its JSON path key; several failures report every key; a GUID in upper case and without hyphens is accepted |
| `Api.Tests` (new `Endpoints/Organisations/Subsidiaries/PostTests.cs`, with a substitute `ISubsidiaryService`) | 2 | `SubsidiariesAdded`: `201`, no `Location`, a Verify snapshot of the body in request order. `ParentOrganisationNotFound`: `404`, empty. `SubsidiaryOrganisationsNotFound`: `400` with the item keys. An invalid body: `400` and the service is not called. No credentials: `401`. A read-only client: `403`. No version and `version=1`: `404`. `version=3`: `406`. `dwt` and an id that is not a GUID: `404`, with and without credentials. `RPD`: the service is called with `rpd` |
| `Api.Tests` (new `Services/SubsidiaryServiceTests.cs`, `Mapping/SubsidiaryExtensionsTests.cs`) | 2 | A missing parent and a missing subsidiary save nothing. Each new link gives one `subsidiary` and one `organisation_lifecycle` row with the values of "Persistence". The response mapping keeps request order and omits a null `rpdExternalSubsidiaryId` |
| `Api.Tests` (`Endpoints/OpenApi/OpenApiTests.cs`) | 2, 3 | v2 snapshot re-verified; v1 snapshot unchanged |
| `Api.IntegrationTests` (new `Scenarios/AddSubsidiariesTests.cs`) | 2 | Jira Scenario 1 end to end: a parent and two subsidiaries are created, then added: `201`, the body, two `subsidiary` rows and two `organisation_lifecycle` rows with the sent values, `service` `rpd`, no `valid_to`, the change columns and `public_subsidiary_id` null. `RPD` in the path is stored as `rpd`. An unknown parent: `404`, no row. One unknown subsidiary among valid ones: `400` under its key and no row for any item. The validation cases, `401`, `403`, `404` without `version=2`, `406`, and `404` for `dwt`, each with no row added |
| `Api.Tests` (`Services/SubsidiaryServiceTests.cs`, `PostTests.cs`) | 3 | Same parent and date: nothing saved. Same parent, other date: the lifecycle row's date changes and nothing else. Another parent: `HAS_ANOTHER_PARENT` with that parent. A circle of two and of three organisations: `CIRCULAR_RELATIONSHIP`. Conflicts and valid items together: every conflict reported, nothing saved. A violation of `ux_subsidiary_current` is evaluated again once, and a second one is thrown. `SubsidiaryConflicts` gives the `409` body |
| `Api.IntegrationTests` (new `Scenarios/AddSubsidiariesExistingLinkTests.cs`) | 3 | Jira Scenario 4: the same request twice gives two `201`s and the row counts do not change. Another date: `201`, the stored date and `updated_at` change, and one current row of each kind remains. Another parent: `409` naming it, and no row for any item. A link ended through `PostgresDbContext`: its subsidiary is added to another parent with `201`. A added under B, then B under A: `409`. Ten identical concurrent requests: ten `201`s and one link. Two concurrent requests that add one subsidiary to two parents: one `201`, one `409`. A link given a `public_subsidiary_id` through `PostgresDbContext` returns it as `rpdExternalSubsidiaryId` |
| `Api.V1.Tests` | all | No test or snapshot changes; every test passes as it is |

### Work breakdown

Three pull requests: the constraints keep a schema change apart from behaviour, and the endpoint with its rules would exceed the size guide as one (author, 27 November 2026). Estimates in non-generated changed lines: about 450, 800 and 550.

| Slice | Work | Verified by |
|---|---|---|
| 1 | Schema. "Schema: Liquibase changelog": `db.changelog-1.6.xml` and its include, the two entities, `PostgresDbContext`, `WasteServiceCodes`, the model, changelog and schema tests, and the README paragraph for 1.6. If the fixture of spec 0009 is in place, its `TRUNCATE` also names `organisation_lifecycle` and `subsidiary` (Decisions, "Test data clearing"). Stops there: no DTO, service, endpoint or OpenAPI change | `Api.Tests`; `Api.IntegrationTests` against Compose, where Liquibase applies `v1.6.0`; `Api.V1.Tests`. Criterion 11, and criterion 12 for this slice |
| 2 | New links. "The request", "The response", and "Outcomes and errors" down to the row for a subsidiary with no current link; "Persistence" without the rules for existing links: the DTOs, validator and filter, the route constraint, `Post.cs`, the mapping, `ISubsidiaryService` with `SubsidiariesAdded`, `ParentOrganisationNotFound` and `SubsidiaryOrganisationsNotFound`, the OpenAPI snapshot and the README paragraph. Stops there: a subsidiary that already has a current link fails on `ux_subsidiary_current` and gets `500`, and circles are not checked, until slice 3 | `Api.Tests`, `Api.IntegrationTests` and `Api.V1.Tests`; the v1 OpenAPI snapshot passes with no diff. Criteria 1 to 6, and criterion 12 for this slice |
| 3 | Existing links. The last four outcome rows, the `409` body, the circle check and "Concurrency": `SubsidiaryConflicts`, the service and handler changes, the `409` in the OpenAPI snapshot and the README | `Api.Tests`, `Api.IntegrationTests` and `Api.V1.Tests`. Criteria 7 to 10, and criterion 12 for this slice |

## Acceptance Criteria

- [ ] 1. A v2 `POST /organisations/{id}/subsidiaries/rpd` with the `write` scope, a stored parent and stored subsidiary organisations that have no current link returns `201`, and stores one `subsidiary` row (parent, subsidiary, `rpd`, no `valid_to`) and one `organisation_lifecycle` row (parent, link, `joiner_date`) for each item (Jira Scenario 1).
- [ ] 2. The `201` body holds one item for each request item, in request order, with `organisationId` and the stored `joinerDate`; `rpdExternalSubsidiaryId` is absent when the link has no `public_subsidiary_id`; there is no `Location` header; `RPD` in the path is accepted and stored as `rpd`.
- [ ] 3. A body with `subsidiaries` missing, empty or over 100 items, an `organisationId` that is missing, not a GUID, repeated or equal to the path `id`, or a `joinerDate` that is missing or not a `YYYY-MM-DD` date returns `400` with every failing field keyed by its JSON path, and stores nothing (Jira Scenarios 2 and 3).
- [ ] 4. An `organisationId` that no organisation has returns `400` keyed by that item's JSON path, and no item of the request is stored.
- [ ] 5. A parent `id` that no organisation has returns `404` with an empty body; an `id` that is not a GUID and a `service` other than `rpd` return `404` with an empty body, with or without credentials (Jira Scenario 6 and the missing Scenario 5).
- [ ] 6. No or invalid credentials return `401` and credentials without the `write` scope `403`, both with an empty body (Jira Scenario 7); a version above 2 returns `406`; a request without `version=2` returns `404`.
- [ ] 7. Adding a subsidiary that already has a current link to the same parent returns `201`: with the same `joinerDate` no row changes, and with another `joinerDate` the link's current lifecycle row holds the new date and no row is added (Jira Scenario 4).
- [ ] 8. Adding a subsidiary that has a current link to another parent in the service returns `409` with `reason` `HAS_ANOTHER_PARENT` and that parent's id, and stores no item of the request; a link that has a `valid_to` does not count.
- [ ] 9. Adding a subsidiary that is an ancestor of the parent in the service returns `409` with `reason` `CIRCULAR_RELATIONSHIP`, for a circle of two and of more organisations, and stores nothing.
- [ ] 10. Concurrent identical requests all return `201` and store one link; two concurrent requests that add one subsidiary to two parents store one link, and one returns `201` and the other `409`.
- [ ] 11. Changelog `db.changelog-1.6.xml` adds `subsidiary` and `organisation_lifecycle` with the columns, foreign keys, checks and unique indexes of "Schema: Liquibase changelog", and tags `v1.6.0`; the database rejects a second current link for a subsidiary in a service, a link from an organisation to itself and a service other than `rpd`; changelogs 1.0 to 1.5 are unchanged and no EF Core migration exists.
- [ ] 12. `/openapi/v2.json` describes the endpoint with its `201`, `400`, `404`, `406` and, from slice 3, `409` responses; `/openapi/v1.json` is unchanged; `Api.V1.Tests` passes with no test or snapshot changed; after every slice the full gate passes and no NuGet package is added.

## Out of Scope

- Creating the subsidiary organisation in this call: it is created with `POST /organisations`.
- Generating or accepting a public subsidiary id (Open Question 1), and loading the ids the Azure account service holds.
- Ending a link (`DELETE` with `leaverDate`): the Confluence page describes it, and no Jira story exists for it yet.
- Reading subsidiaries (MAV-19, MAV-20), and showing them on `GET /organisations/{id}`.
- Joiner and leaver codes, the W&CE change code list and change reasons: the three change columns stay empty.
- A state on the link (pending, approved, redacted) and approval of a link (Open Question 5).
- An outbox event for the write (ADR-016): the repo has no outbox yet.
- An obligation year on the link.
- Services other than `rpd`.
- Checking that either organisation is validated in Defra ID (ADR-003): the API has no Defra ID link in this phase. Both must exist in the store.
- A `Location` header, and a message body on `401`, `403` or `404`.
- The file upload of the prototype, and any Companies House look-up.
- A response-time target: the story's NFR section names none.

## Decisions

| Topic | Decision |
|---|---|
| Stored here | The Organisations API owns the organisations and the relationships between them (Darren Walker, Slack, relayed 27 November 2026; ADR-003, Agreed). The Confluence question "should we even store the relationships" is answered by that |
| A subsidiary is an organisation | "A child organisation, with its own public ID" (ADR-159); the prototype creates it from a Companies House number, a name, an address and a nation, which is the v2 `POST /organisations`. So the request carries the `organisationId` of the Confluence contract, and Jira Scenarios 2 and 3 are about `organisationId` and `joinerDate` only |
| Per service | Relationships are identified per service (Darren Walker, Slack, relayed 27 November 2026), which is why the path has `<service>`. The link stores the service, and ADR-003's "an org has at most one parent" is applied as one current parent per service |
| Add, not replace | The call adds links; the Confluence page ends them with a separate `DELETE`. The story's "store what subsidiaries are currently associated" is not read as replacing the list |
| Two tables | `subsidiary` and `organisation_lifecycle`, as on the Miro board, with the joiner date on the lifecycle row (author, 27 November 2026, chosen over one `subsidiary` table that carried the date). This brings into scope two tables spec 0003 left out; spec 0003 is not edited |
| Beyond the board | `subsidiary_organisation_id` and `service` are added: the board's table has one organisation column, which the architecture site names as the parent, and no service. `ck_subsidiary_not_self` and the two unique indexes are added for the rules above |
| Column names | The board's `ChangeDate` and `ChangeCode` are kept as `change_date` and `change_code`; the architecture site calls the same columns `LeaverDate` and `LeaverCode`. The story that ends a link decides how `leaverDate` is stored |
| `joiner_date` not null | Every row this story writes has a joiner date. A later story that needs a row without one relaxes the column with a changeset |
| `joinerDate` | A date only, as the Confluence example `2026-09-01` shows (author, 27 November 2026). No obligation year: joiner and leaver dates are what is stored (Darren Walker, Slack, relayed 27 November 2026) |
| Version | A new v2-only endpoint. The story's "as this endpoint already exists all solutions built will need to be versioned" does not apply: no v1 endpoint has this path |
| Service list | `WasteServiceCodes.All` and a check constraint, the pattern of `OrganisationTypeCodes`, not configuration: one value today |
| Unknown service | `404` from routing through a route constraint, the same answer as an id that is not a GUID (spec 0006), so the path is checked before the body |
| Error bodies | `401`, `403` and `404` are empty, as specs 0005 and 0006 decided for v2. The messages Jira Scenarios 6 and 7 ask for are not delivered (Open Question 7) |
| Unknown subsidiary | `400` under the item's key, not `404`: the resource of the path exists and the fault is in the body |
| Check order | Routing, authentication, authorisation, version and body checks, then validation, then the parent, then the subsidiaries, then existing links, as spec 0008 orders its checks |
| All or nothing | One save for the request. A request with one item that fails stores nothing, so the caller can send it again unchanged |
| Status and `Location` | `201` for every success, as Jira Scenarios 1 and 4 ask. No `Location`: the call creates no single resource, and the read of subsidiaries is MAV-19 |
| Batch size | At most 100 items, the size ADR-011 gives for a bulk call. An assumption, with no source for this endpoint; the review can change it |
| Changed date | Updated in place on the current lifecycle row, so the earlier date is not kept. It follows the working answer to Open Question 4 |
| `409` body | One problem details body that lists every conflicting item with a `reason`, and the current parent for `HAS_ANOTHER_PARENT`, since the caller needs it to end that link |
| Circle check | Walking up from the parent through current links, in the service. Two concurrent requests that link A under B and B under A can both pass it (Risks) |
| Separate service | `ISubsidiaryService`, not more methods on `IPostgresOrganisationService`: one responsibility per class. It needs no "Postgres" in its name, as no Mongo service has the name |
| Working answers | The spec is written on the answer proposed for Open Questions 1 to 6, to be confirmed at the review of this spec (author, 27 November 2026) |
| Test data clearing | Both new tables reference `organisation`, and the `TRUNCATE` of spec 0009 has no `CASCADE`, so it must name them. This adds to "What to clear" of spec 0009, which is not edited |
| Outbox | No event is written: the repo has no outbox. The story that adds it covers this endpoint |

## Open Questions

Questions 1 to 6 are for Darren Walker (the API contract) and Jay Bradshaw (the story), at the review of this spec. Each gives the working answer the spec is written on.

1. **When is the subsidiary ID issued, and by whom?** In RPD today the producer gets a six-digit subsidiary ID first and then quotes it in the registration file, which is where the joiner date comes from. The Confluence page issues the ID from this call, which needs a `joinerDate`. The data model says six digits and the bulk example shows ten, and the Azure account service still issues these IDs. The UCD board shows the same order on today's screens: adding a single subsidiary ends with its ID and asks for no joiner date, and the joiner and leaver codes reach the regulators through the registration file only. It suggests that Manage Account keeps the known organisations and their IDs and leaves the yearly relationship to the registration file. So the question underneath is who calls this endpoint: only the processing of a registration submission, which has a joiner date, or also an "add a subsidiary" journey, which has none and needs the ID back. Working answer: this story generates none; `public_subsidiary_id` exists, stays null and is returned as `rpdExternalSubsidiaryId` once something sets it. Another answer changes "The response", the request and slice 2.
2. **Is `joinerDate` required?** If the ID must exist before the registration file, the first call has no date: today's add of a single subsidiary asks for a Companies House number, the subsidiary's details and its nation only (UCD board). Working answer: required, which fits a caller that processes a registration submission. If it becomes optional, a link without a date could be a `subsidiary` row with no `organisation_lifecycle` row until a submission reports the date. Another answer changes the request, the response, `joiner_date` and slices 1 and 2.
3. **Does the add carry a joiner code?** Submissions come with a joiner or leaver code (Darren Walker, Slack), the Confluence request has none, and ADR-011 says RPD codes are not the API's vocabulary. Working answer: no code; `change_code` stays empty.
4. **What happens when a link already exists?** (a) Same parent and date: nothing changes. (b) Same parent, another date: the date is updated, as the Confluence page suggests. (c) Another parent in the service: `409`, and the caller ends the old link first. Jira Scenario 4 repeats Scenario 1, so it does not say. On (b): the UCD board records the regulators' need for an audit trail of joiners and leavers for each compliance year, and an update in place does not keep the earlier date; if it must be kept, the change is a new lifecycle row and a `valid_to` on the old one. On (c): no endpoint ends a link yet, so the caller cannot do so (Risks). Another answer changes slice 3 only.
5. **Does the link have a state?** ADR-003 gives a relationship a state, and the story says "persist with the correct state". The UCD board lists "is there any kind of approval process?" among its own open questions, and records that regulators accept or reject a group through the registration file only. Working answer: none in this story; a link is current from the moment it is stored, and state arrives with MAV-23. Another answer adds a column in slice 1.
6. **Which organisation is `organisation_lifecycle.organisation_id` for a subsidiary link?** The board gives the table one organisation column. Working answer: the parent, the same value as `subsidiary.organisation_id`, since history is read for the group. Another answer changes slice 1.
7. **Jira wording.** After the review the story needs: a "Given" and the `/openapi/v2.json` link in Scenario 1; the rule of Question 4 in Scenario 4; the missing Scenario 5; one case for each of an unknown parent and an unknown service in Scenario 6; `POST` and the right path in Scenario 7; and empty bodies for `401` and `404`. Owner: the story's author. It does not block development.

## Dependencies

- MAV-11 (spec 0005), MAV-12 (spec 0006), MAV-42 (spec 0007) and MAV-17 (spec 0008), all merged: the v2 create the tests use to store organisations, and changelog 1.5, which 1.6 follows.
- Spec 0009 (MAV-17, in progress): whichever of its slice and slice 1 of this spec is built second adds the two new tables to the `TRUNCATE` (Decisions, "Test data clearing").
- The answers to Open Questions 1 to 6. Questions 2, 5 and 6 can change slice 1, Question 1 slice 2 and Question 4 slice 3.
- The slices deploy in order, and slice 3 should follow slice 2 closely (Risks).
- MAV-19 and MAV-20 read these tables. No story exists yet for ending a link, and a subsidiary cannot change parent until one is delivered (Risks).

## Risks

| Risk | Level | Mitigation |
|---|---|---|
| An open question is answered differently after development starts | Medium | The spec is reviewed before slice 1 starts; each question names the slice it changes; no service consumes v2 yet |
| Between slices 2 and 3 a repeat add gets `500` and a circle can be stored | Low | No service consumes v2 yet; slice 3 follows directly; the pull request of slice 2 says so in "How to test" |
| Two concurrent requests link A under B and B under A, and both pass the circle check | Low | Links are written by one consumer processing submissions; the database cannot express the rule; a later story can serialise subsidiary writes per service if it is seen |
| A subsidiary that moves to another group cannot be added to its new parent: the old link gives `409` and no endpoint ends a link. The UCD board shows the same block in RPD today, where a subsidiary stays linked after its leaver code is sent, the group it joined cannot take it on, and the helpdesk has to step in | Medium | The story that ends a link (the Confluence `DELETE` with `leaverDate`) is raised and delivered before a service consumes this endpoint; until then a link is ended in the database only; the `409` names the current parent |
| The decision to store relationships here is revisited: handling them per service is called "a bit of a red flag" in the same Slack thread, the UCD board suggests leaving the yearly relationship to the registration file, and it records earlier research in which users did not see their legal entities as parent and child | Medium | The service is on the row, so the data can be handed to a service; the tables are used by this endpoint only until MAV-19 |
| `organisation_lifecycle` is created before its wider use is designed, and a later story needs another shape | Medium | Only the columns the board shows are added, and only `joiner_date` is written; a later changeset can alter a table with little data |
| The IDs the Azure account service issues clash with IDs issued here | Low | None is issued here (Open Question 1) |
| A consumer built to Jira Scenarios 6 and 7 expects a message body | Low | The v2 OpenAPI document shows empty `401` and `404` responses; Open Question 7 updates the story |

## Notes

- Branches: `MAV-18-add-subsidiaries-to-organisation-spec` for this spec, then `MAV-18-add-subsidiaries-to-organisation-slice-1` to `-slice-3`.
- The Jira comment on the story ("Is a subsidiary an organisation in its own right?") is answered by Decisions, "A subsidiary is an organisation".
- The architecture site, the prototype and the UCD board are named by title and not linked: this repository is public.
- The UCD board was read after the first draft of this spec. It changes no working answer: it adds evidence to Open Questions 1, 2, 4 and 5 and two entries to Risks. Its design suggestions are marked on the board as suggestions, not decisions.
- The pull request of slice 1 says in "How to test" that changelog 1.6 must be applied.
- Ids and dates in the examples are synthetic.

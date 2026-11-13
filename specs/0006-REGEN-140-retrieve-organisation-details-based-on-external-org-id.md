# REGEN-140: Retrieve organisation details based on external Org ID

| | |
|---|---|
| Type | Feature: a v2 `GET /organisations/{id}` that returns one organisation and its current addresses from Postgres, added to the v2 OpenAPI document. No schema change, no change to the v2 `POST /organisations` response, no change for v1 clients |
| Priority | P1 (High): the second endpoint of REGEN-125, the story is In Progress, and the `Location` header of the v2 create cannot be followed until it lands |
| Parent | REGEN-125 Access to org data via Org API |
| Risk | Low for delivery: no schema change, and the routing is the pattern spec 0004 already tests. One product risk (see Risks) |
| Source | Jira story REGEN-140 as read on 9 November 2026 (business rules and Scenarios 1 to 6), its description as updated on 12 November 2026 to match these decisions (Open Question 2), and its comments; the summary comment on the story of 12 November 2026 (comment 944637, "Summary of decisions so far"), which records the decisions below; the "Retrieve Single Organisation by ID" section of the Confluence page "Organisations API - waste-organisations" as read on 12 November 2026, after `homeNation` was renamed `regulatorNation`; the gap review of 9 November 2026 and the refinement answers of 12 November 2026 recorded in Decisions; specs 0003, 0004 and 0005 |

## Problem/Opportunity

The v2 API can create an organisation in Postgres (spec 0005) but cannot read one back. `GET /organisations/{id}` exists in v1 only: it reads MongoDB, so it answers `404` for an organisation the v2 create stored, and with `version=2` it answers `406`. The `Location` header of the v2 create therefore leads nowhere. REGEN-140 adds the v2 read, so the Organisations API can become the source of organisation data for the services that consume it.

What is already in place:

- **Versioning (spec 0004):** `Accept: application/vnd.defra.waste-organisations+json; version=2` selects v2; an endpoint declares v2 with `.WithApiVersions(2)`; `ApiVersionMatcherPolicy` routes by version and `ApiVersioningMiddleware` returns `406` "Unsupported API version" after authorisation.
- **v1 read:** `src/Api/Endpoints/Organisations/Get.cs` maps `/organisations/{id:guid}` to the Mongo-backed `IOrganisationService`. Its behaviour is pinned by `tests/Api.V1.Tests/Scenarios/GetOrganisationTests.cs` (ORG-GET-01 to ORG-GET-08): an empty `404` for an unknown id and for an id that is not a GUID.
- **v2 create (spec 0005):** `Post.cs`, `IPostgresOrganisationService` (create and two reference look-ups, no read), the response DTOs `OrganisationResponse` and `AddressResponse`, `AddressLine`, and the tables `organisation`, `address`, `organisation_address`, `organisation_type` and `address_usagetype`. `ux_organisation_address_current` allows one link per organisation and address kind with no `valid_to`.
- **A test that expects this story:** `CreateOrganisationErrorTests.Created_ShouldNotBeReadableByV2GetYet` in `Api.IntegrationTests` pins the `406` "until REGEN-140 adds a v2 GET".
- **Authentication:** Basic or JWT bearer, scopes from the ACL, `PolicyNames.Read` for reads.

Who benefits:

- **RPD (Submit Data) and Meet Obligations:** read an organisation by its id from one contract.
- **Manage Account and every client of the v2 create:** the `Location` it returns resolves.
- **The team:** the v2 OpenAPI document gains the operation the Jira scenarios refer to as "swagger documentation".

Three things the story first asked for are not delivered here, by decision of 12 November 2026: organisation state (Scenarios 5 and 6 and the rule "latest approved or rejected record"), because the v2 schema holds no state; a `400` for an id that is not a GUID (Scenario 3), which stays the empty `404` v1 gives; and a message body on `401` and `404`. All v1 behaviour stays exactly as it is.

## Proposed Solution

Add a v2-only `GET /organisations/{id}` beside the v1 one. It reads the organisation by its own id from Postgres with its organisation type and its current address links, maps them to a response of its own shape, and returns `200` with an `organisations` array of one item, or an empty `404`. It writes nothing. v2 code sits in the existing folders, as spec 0005 decided.

### The request

`GET /organisations/{id}` with `Accept: application/vnd.defra.waste-organisations+json; version=2`, no body and no query parameters, credentials with the `read` scope.

`id` is the organisation's own id: the UUIDv7 the v2 create generates and returns in `Location`. "External Org ID" in the story title means this id (12 November 2026). It is accepted in every form v1 accepts, since the route keeps the `{id:guid}` constraint: lower or upper case, with or without hyphens (ORG-GET-08).

### The response

`200 OK`, `Content-Type: application/json`:

```json
{
  "organisations": [
    {
      "id": "0199c0f6-1f7a-7c8e-9f3a-5b2d8c4e7a10",
      "name": "Acme Waste Ltd",
      "tradingName": "Acme",
      "companiesHouseNumber": "12345678",
      "regulatorNation": "GB-ENG",
      "organisationType": "LTD",
      "addresses": [
        {
          "addressTypes": ["REGISTERED", "PRINCIPAL"],
          "addressLine": "Unit 4, Mill House, 12A High Street, Testville, Testshire, AB1 2CD",
          "subBuildingName": "Unit 4",
          "buildingName": "Mill House",
          "buildingNumber": "12A",
          "street": "High Street",
          "locality": "Testville",
          "region": "Testshire",
          "postalCode": "AB1 2CD",
          "countryIso": "GB"
        },
        {
          "addressTypes": ["CORRESPONDENCE"],
          "addressLine": "12 Harbour Road, Hong Kong",
          "buildingNumber": "12",
          "street": "Harbour Road",
          "locality": "Hong Kong",
          "countryIso": "HK"
        }
      ]
    }
  ]
}
```

`organisations` always holds exactly one item on a `200`.

#### Organisation fields

| Field | Read from | Notes |
|---|---|---|
| `id` | `organisation.id` | Lower case with hyphens, whatever form the request used |
| `name` | `organisation.name` | Omitted when null |
| `tradingName` | `organisation.trading_name` | Omitted when null |
| `companiesHouseNumber` | `organisation.companies_house_number` | Omitted when null |
| `regulatorNation` | `organisation.regulator_nation` | `GB-ENG`, `GB-SCT`, `GB-WLS` or `GB-NIR`; the story's and the Confluence page's earlier `homeNation` |
| `organisationType` | `organisation_type.code` through `organisation.organisation_type_id` | One of the ten codes of spec 0005. Omitted when the row has no type: the column is nullable, although the v2 create always sets it |
| `validTo` | `organisation.valid_to` | Omitted when null, which it is for every organisation the API can store today. The organisation is returned whatever its `validTo` is |
| `addresses` | `organisation_address` joined to `address` and `address_usagetype` | Current addresses only (below). An empty array, never omitted, when the organisation has none |

The organisation's `valid_from`, `created_at` and `updated_at` are not returned (12 November 2026), although the v2 create's `201` carries them; the address items carry no dates. Timestamps are ISO 8601 with an offset, as the v2 create writes them. Null fields are omitted (`JsonIgnoreCondition.WhenWritingNull`), so consumers must treat an absent field as null. The Confluence fields `rpdExternalOrganisationId`, `rpdExternalSubsidiaryId`, `isComplianceSchemeOperator` and `complianceSchemeMembership` have no column and are not returned.

#### Addresses

Only the organisation's latest addresses are returned: the current address for each address kind, which in the database is an `organisation_address` row with `valid_to IS NULL`. `ux_organisation_address_current` guarantees at most one such row per kind, so a kind appears at most once in the response. A link that has ended is not returned.

Each item is one address with the kinds it currently serves as:

| Field | Read from | Notes |
|---|---|---|
| `addressTypes` | `address_usagetype.name` of each link in the item | `REGISTERED`, `PRINCIPAL`, `AUDIT`, `SERVICE_OF_NOTICE`, `CORRESPONDENCE` |
| `addressLine` | Computed by `AddressLine.Build`, as in the v2 create | Not stored |
| `subBuildingName`, `buildingName`, `buildingNumber`, `street`, `poBox`, `locality`, `region`, `postalCode`, `countryIso` | The `address` row | Each omitted when null |

The link's `valid_from` and `valid_to` are not returned (12 November 2026), so an item has the same fields as an address of the v2 create's `201`. Links are grouped by address alone: an address the organisation currently uses for several kinds is one item with several `addressTypes`, whenever each kind was linked. Straight after a create the items are the ones the `201` returned.

The order of `addresses`, and of `addressTypes` within an item, is not guaranteed (12 November 2026). The OpenAPI descriptions say so, and tests compare them without regard to order.

### Errors

| Case | Status | Body |
|---|---|---|
| `id` is a GUID and no `organisation` row has it, including the empty GUID and an organisation that exists only in MongoDB | `404` | Empty (Jira Scenario 2; no message, as v1) |
| `id` is not a GUID | `404` | Empty, from routing, as v1 (ORG-GET-03): the route constraint matches no endpoint, so the answer is the same with or without credentials. Jira Scenario 3 as reworded on 12 November 2026; it first asked for `400` (Decisions) |
| No or invalid credentials | `401` | Empty (Jira Scenario 4; no message, as v1 and the v2 create) |
| Credentials without the `read` scope | `403` | Empty |
| `version=2` is not requested: no `Accept` version, or `version=1` | v1's response | The v1 endpoint answers from MongoDB exactly as today; an organisation the v2 create stored is a `404` there |
| A version above 2, or a malformed one | `406` | `detail: "Unsupported API version"` (spec 0004) |
| An unexpected failure | `500` | Problem details, as every other error |

Authentication and authorisation run before the version check and the handler, so a request for a GUID gets `401` or `403` before `406` or `404`. An id that is not a GUID matches no endpoint and gets routing's `404` before any of them, in every version.

### Routing

The v2 endpoint is mapped on `/organisations/{id:guid}`, the same route text as v1's (12 November 2026). `ApiVersionMatcherPolicy` groups endpoints by `RoutePattern.RawText`: with the same text, the v2 `GET` replaces the v1 `GET` in the v2 branch, and the v1 `PUT` stays there as a fallback, so a v2 `PUT` still gets `406`. An unconstrained v2 route would count as another route; the v1 `GET` would then stay in the v2 branch and, being constrained, outrank it, and a valid v2 `GET` would get `406`. `ApiVersionMatcherPolicy` and `ApiVersioningMiddleware` do not change.

With the constraint, an id that is not a GUID matches no route and never reaches the handler, so it gets routing's empty `404` in v2 as in v1. No second route is added to turn that into a `400` (12 November 2026). Two handlers on one constrained route, one per version, is the case `ApiVersionMatcherPolicyRoutingTests` already covers, so no routing test is added.

| Request | No version or `version=1` | `version=2` | Any other version |
|---|---|---|---|
| `GET /organisations/<GUID>` | v1 handler (unchanged) | v2 handler, where it is `406` today | `406` (unchanged) |
| `GET /organisations/<not a GUID>` | `404` from routing (unchanged) | `404` from routing (unchanged) | `404` from routing (unchanged) |

### Persistence

`IPostgresOrganisationService` gains `Task<Organisation?> GetAsync(Guid id, CancellationToken cancellationToken)`. It reads through `PostgresDbContext` with EF Core LINQ, untracked: the `organisation` row by id, its `OrganisationType`, and its `Addresses` filtered to `ValidTo == null`, each with its `Address` and `UsageType`. It returns null when no row has the id. One round trip; an organisation has at most five current links.

Nothing is written, to Postgres or to MongoDB, and MongoDB is not read: an organisation that exists only there is a `404` from v2 until data is migrated (REGEN-157). No changelog file is added and no entity changes.

### Code layout

| Path | Content |
|---|---|
| `src/Api/Endpoints/Organisations/GetDetails.cs` | `MapOrganisationsGetDetails`: `MapGet("/organisations/{id:guid}", Handle)` with `.WithName("GetOrganisationDetails")`, `.WithTags("Organisations")`, summary and description, `.Produces<GetOrganisationDetailsResponse>()`, `.Produces(404)`, `.ProducesProblem(500)`, `.RequireAuthorization(PolicyNames.Read)` and `.WithApiVersions(2)`. The handler calls `GetAsync` and returns `TypedResults.NotFound()` or `TypedResults.Ok(organisation.ToDetailsResponse())` |
| `src/Api/Dtos/GetOrganisationDetailsResponse.cs` | The envelope: `organisations`, an array of `OrganisationDetailsResponse` |
| `src/Api/Dtos/OrganisationDetailsResponse.cs`, `AddressDetailsResponse.cs` | The two shapes above, as records with XML documentation that the OpenAPI document picks up. `AddressDetailsResponse` has the fields of `AddressResponse` and is still its own type (Decisions). `RegulatorNation` and `AddressType` are reused. `OrganisationResponse`, `AddressResponse` and `CreateOrganisationResponse` are not changed and not reused (12 November 2026) |
| `src/Api/Mapping/OrganisationDetailsExtensions.cs` | `ToDetailsResponse(this Organisation)`: the organisation fields, the current links grouped by address, `AddressLine.Build` for each item. `CreateOrganisationExtensions.cs` is not changed; its one-line `AsUtc` is repeated rather than shared (rule of three) |
| `src/Api/Services/IPostgresOrganisationService.cs`, `PostgresOrganisationService.cs` | `GetAsync` (Persistence above) |
| `src/Api/Endpoints/Endpoints.cs` | Calls `MapOrganisationsGetDetails()` |
| `README.md` | The "API v2" paragraph names both v2 endpoints and this spec |

No v1 endpoint, DTO, mapping or service file changes. No NuGet package is added, so `.github/dependabot.yml` does not change. Nothing new is logged: names and addresses are personal data and never reach the log.

### OpenAPI

`/openapi/v2.json` gains `GET /organisations/{id}`: the `id` path parameter as a UUID, the `200` schema, the empty `404`, and the `Accept` header parameter and `406` that `AcceptHeaderOperationTransformer` adds to every v2 operation. `OpenApiTests.OpenApiV2_VerifyAsExpected` is re-verified after its diff is reviewed. `/openapi/v1.json` does not change: `ShouldInclude` keeps v2-only operations out, and `OpenApi_VerifyAsExpected` and X-CONTRACT-01 pass with no diff.

### Tests

| Project | Test |
|---|---|
| `Api.Tests` (new `Endpoints/Organisations/GetDetailsTests.cs`, with a substitute `IPostgresOrganisationService`) | Found: `200` and a Verify snapshot of the body, in which the organisation has no `validFrom`, `createdAt` or `updatedAt` and no address has `validFrom` or `validTo`. Service returns null: `404`, empty. No credentials: `401`, empty. A client without `read`: `403`, empty. No `Accept` version and `version=1`: the v1 Mongo service is called and the Postgres service is not. `version=3`: `406`. `not-a-guid` with `version=2`: `404`, empty, with and without credentials, and neither service is called |
| `Api.Tests` (new `Mapping/OrganisationDetailsExtensionsTests.cs`) | Kinds of one address are one item, also when their links have different `valid_from` values; two addresses are two items; no links is an empty array; null address parts, a null organisation `validTo` and a missing organisation type are omitted; an organisation with `validTo` maps it with a UTC offset; an address item has no date; the envelope holds one item |
| `Api.Tests` (`Services/PostgresOrganisationServiceTests.cs`) | The read's query filters links on `valid_to IS NULL`, checked without a database (for example through `ToQueryString()` on the model-only context the file already builds), so the new lines count towards unit coverage |
| `Api.Tests` (`Endpoints/OpenApi`) | `OpenApiV2_VerifyAsExpected` re-verified; `OpenApi_VerifyAsExpected` unchanged |
| `Api.IntegrationTests` (new `Scenarios/GetOrganisationDetailsTests.cs`, against Compose, creating its own organisations through the v2 `POST`) | Create, then `GET` the `Location` with `version=2`: `200`, the organisation fields the two responses share equal the `201`'s, the organisation has no `validFrom`, `createdAt` or `updatedAt`, no address has `validFrom` or `validTo`. The id in upper case and without hyphens: `200`. A link ended through `PostgresDbContext`: its kind is no longer returned; with every link ended, `addresses` is `[]`. A kind re-linked to another address: only the new address is returned for it. Two kinds of one address given different `valid_from` values: still one item. `valid_to` set on the organisation: still `200`, with `validTo`. An unknown GUID and the empty GUID: `404`, empty. An organisation created through v1 `PUT`: `404` from v2, `200` from v1. No credentials: `401`, empty. `WriteOnlyOAuth`: `403`, empty. `version=3`: `406`. `not-a-guid` with `version=2`: `404`, empty, with and without credentials |
| `Api.IntegrationTests` (`Scenarios/CreateOrganisationErrorTests.cs`) | `Created_ShouldNotBeReadableByV2GetYet` is removed: the gap it pinned is closed and the new tests cover the read. `Created_ShouldNotBeFoundByV1Get` stays |
| `Api.V1.Tests` | No test or snapshot changes; every test passes as it is |

### Work breakdown

One pull request: no schema change and one concern. The estimate is about 800 non-generated changed lines, more than half of them tests, which is at the size guide of `doc/rules/git-workflow.md` (Risks).

| Slice | Work | Verified by |
|---|---|---|
| 1 | The whole spec: the three DTOs, `OrganisationDetailsExtensions`, `GetAsync`, `GetDetails.cs` on the constrained route, `Endpoints.cs`, the README paragraph, the v2 OpenAPI snapshot and every test in Tests; `Created_ShouldNotBeReadableByV2GetYet` removed | `Api.Tests`, then `Api.IntegrationTests` and `Api.V1.Tests` against Compose, pass with no v1 test or snapshot changed; `dotnet csharpier check .` passes. Acceptance criteria 1 to 12 |

## Acceptance Criteria

- [ ] 1. `GET /organisations/{id}` with `Accept: application/vnd.defra.waste-organisations+json; version=2`, credentials with the `read` scope and the id of an organisation stored in Postgres returns `200` with an `organisations` array holding that one organisation; the id is accepted in upper case and without hyphens (Jira Scenario 1).
- [ ] 2. The organisation carries `id`, `name`, `tradingName`, `companiesHouseNumber`, `regulatorNation`, `organisationType`, `validTo` and `addresses` with the stored values; null fields are omitted; the organisation's `validFrom`, `createdAt` and `updatedAt` are not returned, nor are `rpdExternalOrganisationId`, `rpdExternalSubsidiaryId`, `isComplianceSchemeOperator` and `complianceSchemeMembership`.
- [ ] 3. `addresses` holds only addresses whose `organisation_address` link has no `valid_to`, each with its `addressTypes`, computed `addressLine`, address parts, and neither the link's `validFrom` nor its `validTo`; the kinds of one address are one item; an organisation with no current link returns `"addresses": []`.
- [ ] 4. An organisation whose own `valid_to` is set is still returned, with `validTo`.
- [ ] 5. A GUID no `organisation` row has, including the empty GUID and the id of an organisation that exists only in MongoDB, returns `404` with an empty body (Jira Scenario 2).
- [ ] 6. An id that is not a GUID returns `404` with an empty body in v2, with or without credentials, as it does in v1 (Jira Scenario 3 as reworded on 12 November 2026; it first asked for `400`).
- [ ] 7. A request with no or invalid credentials returns `401` with an empty body (Jira Scenario 4); one without the `read` scope returns `403` with an empty body.
- [ ] 8. Without `version=2` the request is answered by the v1 endpoint exactly as today; a version above 2 returns `406`; the `Location` header of a v2 create, followed with `version=2`, returns `200`.
- [ ] 9. The endpoint reads Postgres only and writes nothing; no changelog file, entity or EF Core migration is added; no state, approval or rejection logic exists.
- [ ] 10. The v2 `POST /organisations` response is unchanged: its snapshots pass with no diff, and `OrganisationResponse`, `AddressResponse` and `CreateOrganisationResponse` are not modified.
- [ ] 11. `/openapi/v2.json` describes `GET /organisations/{id}` with the `Accept` header, the `200` schema and the `404` and `406` responses; `/openapi/v1.json` is unchanged and both v1 document snapshots pass with no diff.
- [ ] 12. `Api.Tests`, `Api.IntegrationTests` and `Api.V1.Tests` pass locally and in `check-pull-request.yml`; no v1 test or `*.verified.*` snapshot of `Api.V1.Tests` is edited; `dotnet csharpier check .` and a warnings-as-errors build pass; no NuGet package is added.

## Out of Scope

- Organisation state: Jira Scenarios 5 and 6 ("awaiting acceptance", "rejected") and the business rule "returns latest approved or rejected record based on approval/rejection date". The schema holds no state or approval dates; state is a later story (REGEN-150 is the existing one).
- Any schema change.
- `rpdExternalOrganisationId`, `rpdExternalSubsidiaryId`, `isComplianceSchemeOperator`, `complianceSchemeMembership`.
- The organisation's `validFrom`, `createdAt` and `updatedAt`, and an address's `validFrom` and `validTo`, in the response.
- Look-up by any other identifier (the six-digit RPD id, the Companies House number); REGEN-144 asks which ids teams use.
- A v2 `GET /organisations` (search), update, and any other v2 endpoint.
- Reading MongoDB from v2, migrating v1 or RPD organisations into Postgres, or dual write (REGEN-157).
- Ended addresses and address history.
- A guaranteed order of `addresses` or `addressTypes`.
- A `400` for an id that is not a GUID (Jira Scenario 3), and a second route to produce one.
- A message body on `401`, `403` or `404`.
- Any change to the v2 `POST /organisations` request or response.
- A response-time target: the story names none, and none is measured here.
- Changes to `ApiVersionMatcherPolicy` or `ApiVersioningMiddleware`.

## Decisions

| Topic | Decision |
|---|---|
| Version | A v2 endpoint; v1 `GET /organisations/{id}` is frozen (9 November 2026) |
| No organisation state | The story continues without state (12 November 2026, replacing the question first put to the team that day). No schema change. Scenarios 5 and 6 and the "latest approved or rejected record" rule move to a later story |
| Status for state cases | If state arrives later, "awaiting acceptance" and "rejected" return `409`, never the story's `406`: spec 0004 reserves `406` for an unsupported API version (12 November 2026). Unused in this story |
| The id | The organisation's own GUID, which is what "external Org ID" means here (12 November 2026) |
| Envelope | An `organisations` array with one item, as Jira Scenario 1 and the Confluence page say (12 November 2026). This supersedes spec 0005 in Open Question 8 and in its Dependencies, which say REGEN-140 returns that spec's `organisation` envelope; spec 0005 is not edited |
| Own shape | `GET` has its own DTOs; the `POST` response does not change (12 November 2026). This supersedes the same Open Question 8 of spec 0005 in "returns this spec's `organisation` shape": the organisation has no `validFrom`, `createdAt` or `updatedAt`, and although the address items now have the same fields as the create's, they stay a type of their own: their order is not promised, and the two responses can then change independently |
| Fields | The fields the v2 create stores, with `regulatorNation`; the four Confluence fields with no column are out (12 November 2026). The Confluence page was updated for the rename |
| Data source | Postgres only (12 November 2026). An organisation that exists only in MongoDB is a `404` in v2, accepted |
| `404` | Empty body for a GUID that is not found, as v1; no message (12 November 2026) |
| `401` and `403` | Empty bodies, the pattern of v1 and the v2 create (12 November 2026) |
| Route | The v2 endpoint keeps `{id:guid}`; `ApiVersionMatcherPolicy` is not changed (12 November 2026, chosen over comparing route templates without their constraints) |
| Id that is not a GUID | An empty `404`, as v1 (12 November 2026, reversing the answer earlier that day that asked for a `400` with validation problem details). The constrained route never sees such an id, so the `400` needed a second v2-only route; that was proposed and declined in favour of v1's behaviour. Jira Scenario 3 was reworded to match on 12 November 2026 |
| GUID forms | The forms v1 accepts, which the shared constraint gives (12 November 2026) |
| Latest addresses only | Only the current address per kind is returned (12 November 2026, reversing the answer earlier that day to return every address). Read as links with no `valid_to` (Open Question 1) |
| Address dates | Address items carry no dates: neither `validFrom` nor `validTo` of the `organisation_address` link is returned (12 November 2026, changing the answers earlier that day that put both on the item and then kept both). `validTo` would be null on every current link anyway |
| Address grouping | One item per address, with every kind it currently serves as. This follows from dropping the dates: the earlier proposal of separate items for kinds linked at different times was only needed while an item carried the link's start date |
| Address order | Not guaranteed (12 November 2026) |
| Organisation `validTo` | The organisation is returned whatever its `validTo` (12 November 2026) |
| Organisation dates | The organisation's `validFrom`, `createdAt` and `updatedAt` are not returned; `validTo` stays (12 November 2026, changing the field list agreed earlier that day, which had all four) |
| Response time | No target in this story (12 November 2026) |
| Pin test | `Created_ShouldNotBeReadableByV2GetYet` is removed with the gap it pinned; it is an integration test of spec 0005, not a v1 test |

## Open Questions

1. **"Latest" as "no `valid_to`".** Flagged on 12 November 2026 and not objected to. Under this reading a kind whose only link has ended has no address in the response. Owner: the story's author.
2. **Jira and Confluence wording.** The Jira part is done: the story description was updated on 12 November 2026. Scenario 1 points at `/openapi/v2.json`; Scenarios 2 and 4 expect an empty body; Scenario 3 expects an empty `404`; Scenarios 5 and 6 and the rule "latest approved or rejected record" are struck through with a note that state is left to a later story (REGEN-150). Still open: the Confluence page says the data returned is "the latest approved data", which waits on the state story.

## Dependencies

- REGEN-167 (spec 0004, merged): versioning, `WithApiVersions`, the matcher policy and the middleware.
- REGEN-141 (specs 0003 and 0005, merged): the schema, the entities, `IPostgresOrganisationService`, `AddressLine`, and the v2 create the tests use to store organisations.
- Nothing else has to land first. The state story (REGEN-150) and data migration (REGEN-157) build on this endpoint.

## Risks

| Risk | Level | Mitigation |
|---|---|---|
| Consumers expect state: an organisation RPD has not approved is returned like any other | Medium | Decided on 12 November 2026 and recorded on the story; the OpenAPI description says the stored organisation is returned as it is; state is a later story |
| The v2 `GET` leaks into `/openapi/v1.json`, or changes v1 routing | Low | `ShouldInclude` already filters by `ApiVersionsMetadata`, and a v1 and a v2 handler on one constrained route is the case spec 0004 tests; both v1 document snapshots and all of `Api.V1.Tests` must pass with no diff |
| A consumer built to Jira Scenario 3 expects `400` for a malformed id and gets `404` | Low | The v2 OpenAPI document shows `404` and no `400`; the story wording was corrected on 12 November 2026 (Open Question 2) |
| A client relies on the order of `addresses` | Low | The OpenAPI description says the order is not guaranteed; tests do not depend on it |
| The pull request exceeds the size guide | Low | Estimated at about 800 non-generated lines, more than half of them tests. If it runs over, the Work breakdown is re-sliced before review: the DTOs, mapping and service read with their unit tests first, then the endpoint |
| The read's query is exercised only against Compose and unit coverage drops | Low | The query's filter is unit-tested without a database (Tests) |

## Notes

- Branch: `REGEN-140-get-organisation-by-id`.
- Local commands, as in `AGENTS.md`: build and run `Api.Tests`; then `docker compose up --build -d`, run `Api.IntegrationTests`, then `Api.V1.Tests` on its own, then `docker compose down -v --remove-orphans`. Host port 5432 must be free.
- The v1 operation is named `GetOrganisation`; endpoint names are unique across the application, hence `GetOrganisationDetails`.
- The integration tests end and re-date links through `PostgresDbContext` because no v2 endpoint changes an organisation yet.

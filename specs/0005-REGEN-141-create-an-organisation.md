# REGEN-141: Create an Organisation

| | |
|---|---|
| Type | Feature: the first v2 endpoint, `POST /organisations`, which stores an organisation and its addresses in Postgres, with a v2 OpenAPI document. Plus a schema change (`db.changelog-1.2.xml`) that the endpoint's rules need, including the organisation type list re-seeded from the Confluence page "Organisation Types and Subtypes". No change for v1 clients |
| Priority | P1 (High): the first endpoint of REGEN-125 Access to org data via Org API, and what Manage Account registers organisations through |
| Parent | REGEN-125 Access to org data via Org API |
| Risk | Medium (see Risks) |
| Source | Jira story REGEN-141 as edited on 2 November 2026 (Scenarios 3, 4 and 5 reworded), its comments (team decisions of 20, 21 and 22 October 2026, and the huddle summary of 26 October 2026 posted on 3 November 2026), the "Create Single Organisation" section of the Confluence page "Organisations API - waste-organisations" as updated on 3 November 2026, version 36 (addresses as an array, each item with an `addressTypes` array listing every kind the address serves as, confirmed as the rule on 3 November 2026), the Confluence page "Organisation Types and Subtypes" (W&CE Manage Account space, Gareth Gower, updated 4 September 2026; adopted as the reference for organisation types on 3 November 2026), the "Defra Address Model" and "Remapping" sections of the Confluence page "Companies House - Address Cleansing" (Integration space), and the refinement answers of 2 November 2026 recorded in Decisions |

## Problem/Opportunity

The API has no way to create an organisation in Postgres. v1 has `PUT /organisations/{id}`, which writes to MongoDB with a client-supplied id and needs a registration in the body. REGEN-141 adds the v2 create endpoint described on the Organisations API page: the server generates the id, the body carries the organisation and its addresses in the Defra standard address model, and the data lands in the tables that spec 0003 built.

What is already in place:

- **Versioning (REGEN-167, spec 0004):** `Accept: application/vnd.defra.waste-organisations+json; version=2` selects v2; an endpoint declares v2 with `.WithApiVersions(2)`; v1 clients never see v2-only endpoints.
- **Schema (spec 0003):** `organisation`, `organisation_type` (40 seeded codes, re-seeded to ten by this story), `address_usagetype` (five seeded kinds), `address` and `organisation_address`, with their EF Core entities and `TimestampInterceptor`.
- **Authentication:** Basic or JWT bearer, scopes from the ACL, `PolicyNames.Write` for writes.

Three decisions of 2 November 2026 do not fit the 1.1 schema, so this story also adds a `db.changelog-1.2.xml`: an organisation without a Companies House number may have only a `tradingName` (so `organisation.name` cannot stay `NOT NULL`), `addressLine` is no longer stored but computed, and the country name (`address.country_name`) is no longer stored. The five address usage type names move to the upper-case form the API uses on the wire. A fourth change was decided on 3 November 2026: the organisation types are the ten on the Confluence page "Organisation Types and Subtypes", not the 36 Companies House `company_type` values plus four of our own that spec 0003 seeded, so the 1.2 changelog re-seeds `organisation_type`. Spec 0003 is not amended; this spec records what changes and why.

Who benefits:

- **Manage Account:** registers an organisation with one call and gets its id back.
- **RPD and other W&CE services:** create organisations through the same contract (Confluence: "pEPR Submit Data - creation of a subsidiary?", "Manage Account - initial organisation registration/enrolment").
- **The team:** the API documentation is generated from the code, so the Jira acceptance criteria ("as per swagger documentation") have something to point at. No hand-maintained OpenAPI contract file is kept: the Confluence page is where the contract is agreed with the wider W&CE teams (huddle of 26 October 2026).

All v1 behaviour stays exactly as it is.

## Proposed Solution

Add `POST /organisations` as a v2-only endpoint beside the v1 `GET /organisations` (search). It validates the body, maps it onto the Postgres entities, saves them in one transaction and returns `201 Created` with the stored organisation and a `Location` header. v2 code sits beside the v1 code in the existing folders, with no separate folder or namespace (huddle of 26 October 2026: the ring-fencing of 22 October 2026 stays lightweight). A second OpenAPI document, `/openapi/v2.json`, describes the v2 API; the v1 document does not change.

### The request

`POST /organisations` with `Accept: application/vnd.defra.waste-organisations+json; version=2` and `Content-Type: application/json`. The body wraps the organisation in an `organisation` object, as on the Confluence page:

```json
{
  "organisation": {
    "name": "Acme Waste Ltd",
    "tradingName": "Acme",
    "companiesHouseNumber": "12345678",
    "homeNation": "GB-ENG",
    "organisationType": "LTD",
    "addresses": [
      {
        "addressTypes": ["REGISTERED", "PRINCIPAL"],
        "subBuildingName": "Unit 4",
        "buildingName": "Mill House",
        "buildingNumber": "12A",
        "street": "High Street",
        "locality": "Testville",
        "region": "Testshire",
        "postalCode": "AB1 2CD",
        "poBox": "PO Box 99",
        "countryIso": "GB"
      },
      {
        "addressTypes": ["CORRESPONDENCE"],
        "buildingNumber": "12",
        "street": "Harbour Road",
        "locality": "Hong Kong",
        "countryIso": "HK"
      }
    ]
  }
}
```

#### Organisation fields

| Field | Required | Rule | Stored in |
|---|---|---|---|
| `name` | For a Companies House type (Organisation types below) | Non-blank string | `organisation.name` |
| `tradingName` | For every other type | Non-blank string | `organisation.trading_name` |
| `companiesHouseNumber` | For a Companies House type; must be absent for every other type | Upper-cased, then must match `^[A-Z0-9]{2}[0-9]{6}$` (eight characters: two letters or digits and six digits, for example `12345678`, `SC123456`, `NI012345`, `OC123456`, `LP123456`) | `organisation.companies_house_number` |
| `homeNation` | Yes | One of `GB-ENG`, `GB-SCT`, `GB-WLS`, `GB-NIR` | `organisation.home_nation` |
| `organisationType` | Yes | One of the ten codes in `OrganisationTypeCodes.All` (Organisation types below), exactly as published (upper snake case, like `addressTypes`) | `organisation.organisation_type_id`, looked up by code |
| `addresses` | Yes | One to five items; each item carries one or more kinds in `addressTypes`; each kind at most once in the request | One `address` row per item and one `organisation_address` row per kind in the item |

The `name`/`tradingName` rule (refinement answer, 2 November 2026): an organisation on Companies House has a registered `name` and may trade under another; a sole trader has no Companies House number and only a `tradingName`. So `name` is mandatory for a Companies House type and `tradingName` is mandatory for every other type. Both may be sent. A string that is present must be non-blank; whitespace-only is a validation error, not "absent". Since 3 November 2026 the rule is keyed on the type rather than on the number's presence, because the type now decides whether a number is present at all; the outcome is the same for every valid request.

No field has a length cap, matching the `text` columns of spec 0003 ("no lengths until the payloads are final"). Every string is trimmed before validation and storage. `companiesHouseNumber` and `countryIso` are upper-cased; nothing else is changed.

#### Organisation types

`organisationType` is required because the type decides which identifier and which address kind the request must carry. The ten types are those on the Confluence page "Organisation Types and Subtypes" (W&CE Manage Account space, updated 4 September 2026), which the team adopted as the reference on 3 November 2026 in place of the 36 Companies House `company_type` values plus four of our own that spec 0003 seeded (Decisions):

| Code | Meaning | Registered with (page) | `companiesHouseNumber` | Address kind the request must carry |
|---|---|---|---|---|
| `LTD` | Private limited company | Companies House | Required | `REGISTERED` |
| `PLC` | Public limited company | Companies House | Required | `REGISTERED` |
| `LIMITED_PARTNERSHIP` | Limited partnership | Companies House | Required | `REGISTERED` |
| `LLP` | Limited liability partnership | Companies House | Required | `REGISTERED` |
| `CIC` | Community interest company | Companies House and the CIC Regulator | Required | `REGISTERED` |
| `SOLE_TRADER` | Sole trader | HMRC | Must be absent | `PRINCIPAL` |
| `PARTNERSHIP` | General partnership | HMRC | Must be absent | `PRINCIPAL` |
| `REGISTERED_SOCIETY` | Co-operative or community benefit society | Financial Conduct Authority (Mutuals Public Register) | Must be absent | `PRINCIPAL` |
| `CHARITABLE_ORGANISATION` | Charitable incorporated organisation or charitable trust | Charity Commission | Must be absent | `PRINCIPAL` |
| `OVERSEAS_ORGANISATION` | Overseas organisation not registered in the UK | No UK regulator | Must be absent | `PRINCIPAL` |

The first five are the Companies House types: the page names the Companies House number as their identifier and the registered address as the address required to create them. For the other five the page names a different identifier (a UTR, a registered society number, a charity registration number, or none) and the principal address. The API accepts no identifier other than the Companies House number in this story, so for those five the number must be absent (a `400` under `organisation.companiesHouseNumber` naming the type), and the other identifiers are a follow-up (Open Questions). The page's partner details for the three partnership types (names, individual or company, roles, minimum counts) are not in the schema and are out of scope.

`OrganisationTypeCode` gains a `RegisteredAtCompaniesHouse` flag, true for the first five, and the three type-dependent rules (name, number, address kind) read that one flag rather than lists of codes, so adding a type later is one entry and one changeset. The page lists no public body and no unincorporated association, so the 1.1 codes `public-body` and `unincorporated-association` go with the Companies House codes the page does not list (Open Questions).

#### Address fields

Each item of `addresses` is an address in the Defra standard address model, with the country flattened to a `countryIso` string (Decisions below), plus its kind:

| Field | Required | Rule | Stored in |
|---|---|---|---|
| `addressTypes` | Yes | A non-empty array of distinct values from `REGISTERED`, `PRINCIPAL`, `AUDIT`, `SERVICE_OF_NOTICE`, `CORRESPONDENCE` (the Confluence enum), listing every kind the address serves as; a kind may appear in one item of the request only. One item with all five kinds is valid; an empty array or a repeated kind is a validation error (refinement answer, 3 November 2026) | One `organisation_address` row per kind, its `usage_type_id` looked up by name |
| `subBuildingName` | See premises | | `address.sub_building_name` |
| `buildingName` | See premises | | `address.building_name` |
| `buildingNumber` | See premises | A string: `12A` and `3-5` are building numbers | `address.building_number` |
| `poBox` | See premises | | `address.po_box` |
| `street` | Yes | Non-blank | `address.street` |
| `locality` | Yes | Non-blank. The town or city | `address.locality` |
| `region` | No | The county, state or province | `address.region` |
| `postalCode` | When `countryIso` is `GB` | Non-blank for a UK address; optional otherwise, because some countries have no postcodes. No format check, because it also holds non-UK codes | `address.postal_code` |
| `countryIso` | Yes | Upper-cased, then two letters (`^[A-Z]{2}$`, ISO 3166-1 alpha-2). `GB` is the United Kingdom, Northern Ireland included. A plain string, not the address model's `country` object (Decisions below) | `address.country_iso` |

Premises (refinement answer, 2 November 2026): at least one of `subBuildingName`, `buildingName`, `buildingNumber` and `poBox` must be non-blank, so the premises can be identified. This mirrors `ck_address_premises` once `address_line` is dropped from it (Schema below), so a request that passes validation cannot fail that constraint.

The request must carry the kind the organisation type requires (Organisation types above) in some item's `addressTypes`: `REGISTERED` for a Companies House type, `PRINCIPAL` for every other type, reported under `organisation.addresses` when missing. Any of the other kinds may be added, in the same item or in others, up to five items. A company whose registered and principal addresses are the same premises sends one item with `["REGISTERED", "PRINCIPAL"]`, and the address is stored once (Confluence page, 3 November 2026). Sending the same premises twice as two items with different kinds is allowed too and stores two `address` rows.

`addressLine` is not an input. It is computed on output (The response). If a client sends it, it is ignored like any other unknown member.

#### Fields the client cannot set

`id`, `validFrom`, `validTo`, `createdAt`, `updatedAt`, `isUkAddress` and `addressLine` are set by the API. If a request carries any of them, or any other unknown member such as `mainActivitySicCode` (dropped on 20 October 2026) or `countryName`, the member is ignored, as `System.Text.Json` does by default and as v1 does. Rejecting unknown members would make adding an optional request field a breaking change for clients that are ahead of the server.

### The response

`201 Created`, `Content-Type: application/json`, `Location: /organisations/{id}`, and the stored organisation in the same envelope:

```json
{
  "organisation": {
    "id": "0199c0f6-1f7a-7c8e-9f3a-5b2d8c4e7a10",
    "name": "Acme Waste Ltd",
    "tradingName": "Acme",
    "companiesHouseNumber": "12345678",
    "homeNation": "GB-ENG",
    "organisationType": "LTD",
    "validFrom": "2026-11-02T10:15:30.123+00:00",
    "createdAt": "2026-11-02T10:15:30.123+00:00",
    "updatedAt": "2026-11-02T10:15:30.123+00:00",
    "addresses": [
      {
        "addressTypes": ["REGISTERED", "PRINCIPAL"],
        "addressLine": "Unit 4, Mill House, 12A High Street, PO Box 99, Testville, Testshire, AB1 2CD",
        "subBuildingName": "Unit 4",
        "buildingName": "Mill House",
        "buildingNumber": "12A",
        "street": "High Street",
        "locality": "Testville",
        "region": "Testshire",
        "postalCode": "AB1 2CD",
        "poBox": "PO Box 99",
        "countryIso": "GB",
        "isUkAddress": true
      },
      {
        "addressTypes": ["CORRESPONDENCE"],
        "addressLine": "12 Harbour Road, Hong Kong",
        "buildingNumber": "12",
        "street": "Harbour Road",
        "locality": "Hong Kong",
        "countryIso": "HK",
        "isUkAddress": false
      }
    ]
  }
}
```

- `id` is a UUIDv7 generated by the API (`Guid.CreateVersion7()`, as spec 0003 requires).
- `validFrom`, `createdAt` and `updatedAt` come from `TimestampInterceptor`, so on create all three are the same instant. They are ISO 8601 with an offset, like v1's registration `created`/`updated`. `validTo` is `null` on create and omitted.
- Null fields are omitted (`JsonIgnoreCondition.WhenWritingNull`): `tradingName`, `companiesHouseNumber`, `validTo` and any absent address part. Consumers must treat an absent field as null.
- `isUkAddress` is the generated column read back after the save: `true` for `GB`, `false` for any other country.
- `addressTypes` lists the kinds in the order they were sent.
- `addressLine` is computed, not stored, in postal order (refinement answer, 2 November 2026): `subBuildingName`, `buildingName`, then `buildingNumber` and `street` joined by one space, then `poBox`, `locality`, `region`, `postalCode`, with blank parts skipped and the rest joined by `, `. The country is not part of it, as in the Remapping table on the address cleansing page. The Remapping table's own order (`region` before `locality`, no `poBox`) is not used: the user asked for postal order, and a PO box address with no other premises would otherwise lose the part that identifies it.
- Fields on the Confluence GET response that have no column are not returned: `rpdExternalOrganisationId`, `rpdExternalSubsidiaryId`, `isComplianceSchemeOperator`, `complianceSchemeMembership`.
- `Location` is `/organisations/{id}`, matching v1's `Results.Created`. Until REGEN-140 (v2 `GET /organisations/{id}`) lands, following it with `version=2` returns `406` (the v1 `GET` does not support v2) and without a version returns v1's `404` (MongoDB). Accepted (refinement answer, 2 November 2026; Open Questions).

### Errors

| Case | Status | Body |
|---|---|---|
| A required field is missing or blank, a value is outside its set or pattern, `organisationType` is missing or unknown, `companiesHouseNumber` is missing for a Companies House type or present for another, the address kind the type requires is missing, `addressTypes` is empty or repeats a kind (in one item or across items), `addresses` is empty, a UK address has no postcode, or an address has no premises | `400` | Validation problem details (`application/problem+json`): `status`, `title` "One or more validation errors occurred.", `traceId`, and `errors`, a map from the JSON path of each failing field to its messages, for example `organisation.addresses[1].postalCode` or `organisation.tradingName`. Every failing field is reported, not only the first (Jira Scenario 2) |
| Malformed JSON, a value of the wrong JSON type (a number for `name`, a string for `addresses` or for `addressTypes`), or an empty body | `400` | Problem details with a `detail` string from the existing exception handler, as v1's `400` (Jira Scenario 3 as reworded on 2 November 2026 requires only the status) |
| A `Content-Type` other than JSON | `415` | Framework default |
| No or invalid credentials | `401` | Empty, as v1 (X-BODY-01) and Jira Scenario 5 as reworded on 2 November 2026 |
| Credentials without the `write` scope | `403` | Empty, as v1 |
| `version=2` is not requested: no `Accept` version, or `version=1` | `405` with `Allow: GET` | v1 does not have `POST /organisations` (spec 0004, X-ROUTE-01) |
| A version above 2, or a malformed one | `406` | `detail: "Unsupported API version"` (spec 0004) |
| An unexpected failure, including a database constraint the validation did not catch | `500` | Problem details, as every other error |

Duplicates are not detected: a second request with the same details creates a second organisation. Jira Scenario 4 was moved to a future story on 2 November 2026 (REGEN-164 is the candidate), and the schema has no unique constraint to raise a `409` from (Out of Scope).

The `errors` keys use the JSON property names (camelCase, with the `organisation.` prefix and the array index), not the C# property names, so a client can match them to what it sent. If the framework's validation emits C# names, the implementation maps them before the response is written; the snapshot tests pin the keys.

### Validation

`AddValidation()` is already registered (`Program.cs`), so the minimal API validation runs the data annotations on the request records and returns the validation problem details above before the handler is called. The rules that one attribute cannot express are `IValidatableObject` implementations on the request records, reported under the same keys:

- On the organisation, from the type's `RegisteredAtCompaniesHouse` flag: `name` required for a Companies House type, `tradingName` for any other; `companiesHouseNumber` required for a Companies House type and rejected for any other; `REGISTERED` among the kinds of some item for a Companies House type, `PRINCIPAL` for any other, reported under `organisation.addresses`. Each kind at most once across the items' `addressTypes`, reported under the later item's `addressTypes` key, naming the kind and the index of the item that already carries it. An unknown or missing `organisationType` reports only the type error, not the three rules that depend on it.
- On each address: `addressTypes` present, non-empty and without a repeated kind; `postalCode` required when `countryIso` is `GB`; at least one premises field.

`organisationType` is checked against `OrganisationTypeCodes.All` in validation (a `400` naming the field, as spec 0003 anticipates), not by the foreign key; the handler then looks the code up for its id. `addressTypes` is an array of the C# enum `AddressType`, which has `JsonStringEnumConverter` and `JsonStringEnumMemberName` for the five values, like v1's `RegistrationType`; an unknown value in the array, or a string where the array should be, is a deserialisation error (`400` with a `detail`), and the enum's name is also the `address_usagetype.name` the handler looks up (Schema below).

### Persistence

One `PostgresDbContext` per request, one `SaveChangesAsync`, so the organisation, one `address` row per item and one `organisation_address` row per kind (each pointing at its item's `address_id`) are one transaction: either all are stored or none is. The handler does not set `ValidFrom`, `CreatedAt` or `UpdatedAt`; `TimestampInterceptor` does, from `TimeProvider`. The `address_usagetype` and `organisation_type` rows are looked up by name and code in the same context, five and ten rows, not cached; a code that is in `OrganisationTypeCodes.All` but missing from the table is a `500` and a drift the existing integration test catches.

Nothing is written to MongoDB. v1 `GET /organisations/{id}` cannot read a v2 organisation (REGEN-157, dual write, is To Do).

### Schema: Liquibase changelog

| Path | Content |
|---|---|
| `changelog/db.changelog-1.2.xml` | New. The changesets below, and a last changeset that tags the database `v1.2.0` |
| `changelog/db.changelog.xml` | Includes `db.changelog-1.2.xml` after `db.changelog-1.1.xml` |

| Changeset | Change | Rollback |
|---|---|---|
| `1.2-organisation-name-nullable` | `dropNotNullConstraint` on `organisation.name`, then `ck_organisation_name` (`sql`): `COALESCE(btrim(name), '') <> '' OR (companies_house_number IS NULL AND COALESCE(btrim(trading_name), '') <> '')`. A row has a non-blank `name`, or has no Companies House number and a non-blank `trading_name`: the API rule in the database's terms | Drop the check, `addNotNullConstraint` |
| `1.2-address-drop-address-line` | Drop `ck_address_premises`; `dropColumn` `address.address_line`; add `ck_address_premises` again over `building_number`, `building_name`, `sub_building_name` and `po_box` only. The constraint is dropped and recreated explicitly rather than left to Postgres's automatic drop with the column, so the rollback is deterministic | Drop the new check, `addColumn address_line text`, add the 1.1 check. Data in `address_line` is not restored; no environment holds any yet |
| `1.2-address-drop-country-name` | `dropColumn` `address.country_name` | `addColumn country_name text`; data not restored |
| `1.2-address-usagetype-upper-case` | `update` each seeded `address_usagetype` row by its fixed id: `Registered` to `REGISTERED`, `Audit` to `AUDIT`, `ServiceOfNotice` to `SERVICE_OF_NOTICE`, `Principal` to `PRINCIPAL`, `Correspondence` to `CORRESPONDENCE` | `update` back to the 1.1 names |
| `1.2-organisation-type-reseed` | `delete` the 34 `organisation_type` rows whose codes are not among the ten, by their fixed 1.1 ids; `update` the six kept rows by id to their upper-case codes (`ltd` to `LTD`, `plc` to `PLC`, `limited-partnership` to `LIMITED_PARTNERSHIP`, `llp` to `LLP`, `sole-trader` to `SOLE_TRADER`, `partnership` to `PARTNERSHIP`), as `1.2-address-usagetype-upper-case` does for the usage types; then `insert` the four new codes `CIC`, `REGISTERED_SOCIETY`, `CHARITABLE_ORGANISATION` and `OVERSEAS_ORGANISATION` with ids `00000000-0000-0000-0001-000000000041` to `...044`, in the order of `OrganisationTypeCodes.All`. `fk_organisation_organisation_type` (`ON DELETE RESTRICT`) would stop the delete if an organisation pointed at a removed row; none does, because no environment holds organisation rows yet | `delete` the four, `update` the six back to their 1.1 codes, `insert` the 34 with their 1.1 ids and codes |
| `1.2-tag-v1.2.0` | `tagDatabase` `v1.2.0` | Automatic |

`db.changelog-1.0.xml` and `db.changelog-1.1.xml` are not edited (README: a published changeset is never changed). The check constraints are `sql` changes with explicit `rollback`, as in 1.1. `dotnet csharpier check .` covers `changelog/*.xml` locally and CI does not (AGENTS.md), so run it after editing.

What does not change: `is_uk_address` stays generated from `country_iso`; `ck_address_uk_postal_code` and `ck_address_country_iso` stay; `ux_organisation_address_current` stays and is what makes "one address per kind" hold in the database as well as in validation. Several `organisation_address` rows sharing one `address_id`, which is how an item with several kinds is stored, need no schema change: the 1.1 join table already allows it, and `ix_organisation_address_address_id` serves the read-back.

### EF Core entities and tests that follow the schema

| File | Change |
|---|---|
| `src/Api/Data/Postgres/OrganisationTypeCodes.cs` | The ten codes of the Confluence page, in upper snake case, with their meanings and the `RegisteredAtCompaniesHouse` flag on `OrganisationTypeCode`; the remarks cite the page instead of Companies House's `constants.yml` |
| `src/Api/Data/Postgres/Entities/Organisation.cs` | `Name` becomes `string?` |
| `src/Api/Data/Postgres/Entities/Address.cs` | `AddressLine` and `CountryName` removed, with their column mappings |
| `tests/Api.Tests/Data/OrganisationTypeCodesTests.cs` | Ten entries; exactly `LTD`, `PLC`, `LIMITED_PARTNERSHIP`, `LLP` and `CIC` flagged as Companies House types; codes and meanings still unique; codes in upper snake case (`^[A-Z0-9]+(_[A-Z0-9]+)*$`) rather than the Companies House kebab-case |
| `tests/Api.Tests/Data/PostgresDbContextModelTests.cs` | Column lists without `address_line` and `country_name`; the in-memory usage type named `REGISTERED` |
| `tests/Api.Tests/Data/TimestampInterceptorTests.cs` | The in-memory usage type named `REGISTERED`, for consistency only; the test never touches the table |
| `tests/Api.IntegrationTests/Scenarios/OrganisationSchemaTests.cs` | Usage types looked up by the upper-case names; `AddressLine` and `CountryName` assertions removed; the "no premises" and "blank premises" cases still rejected |
| `tests/Api.IntegrationTests/Scenarios/DatabaseChangelogTests.cs` | The five usage types are the upper-case names; a new test finds the `v1.2.0` tag; `OrganisationType_ShouldHoldThePublishedCodes` is unchanged and now expects the ten codes through `OrganisationTypeCodes.All` |

These are integration and unit tests, not v1 tests, so changing them is allowed. No v1 test or snapshot changes.

### Code layout

v2 code sits in the existing folders beside the v1 code, told apart by its type names and by `.WithApiVersions(2)`, not by a separate folder or namespace. The team meeting of 22 October 2026 agreed to ring-fence v1 code; the huddle of 26 October 2026 kept that lightweight: a complete isolation of v1 code into separate files or folders is not intended. This story changes no v1 endpoint, DTO, mapping or service file because a new endpoint does not need to, which is a consequence, not a rule. The data layer (`Data/Postgres`) is shared.

| Path | Content |
|---|---|
| `src/Api/Endpoints/Organisations/Post.cs` | `MapOrganisationsPost`, beside `Get.cs`, `Put.cs` and `Search.cs`: `MapPost("/organisations", Handle)` with `.WithName("CreateOrganisation")`, `.WithTags("Organisations")`, summary and description, `.Produces<CreateOrganisationResponse>(201)`, `.ProducesValidationProblem(400)`, `.ProducesProblem(500)`, `.RequireAuthorization(PolicyNames.Write)`, `.WithApiVersions(2)` |
| `src/Api/Dtos/` | New files in the existing `Defra.WasteOrganisations.Api.Dtos` namespace: `CreateOrganisationRequest` (`organisation`), `OrganisationRequest`, `AddressRequest`; `CreateOrganisationResponse` (`organisation`), `OrganisationResponse`, `AddressResponse`; enums `HomeNation` (the four ISO 3166-2:GB codes; v1's `BusinessCountry` is not reused) and `AddressType`, of which `AddressRequest.AddressTypes` and `AddressResponse.AddressTypes` are arrays. None of the names collides with v1's `Organisation`, `Address` or `BusinessCountry`, which are not reused because their fields and rules differ |
| `src/Api/Mapping/CreateOrganisationExtensions.cs` | Beside `OrganisationExtensions.cs`. Request to entities (ids, trimming, upper-casing, usage type and organisation type ids; one `Address` per item and one `OrganisationAddress` per kind in its `addressTypes`); entities to response, including `AddressLine` building and `addressTypes` regrouped from the `OrganisationAddress` rows of each `Address` |
| `src/Api/Services/IPostgresOrganisationService.cs`, `PostgresOrganisationService.cs` | `Create(Organisation, CancellationToken)` plus the two reference lookups. Named after its store, so it does not collide with the v1 Mongo `IOrganisationService`, which is not extended because it is Mongo-backed |
| `src/Api/Endpoints/Endpoints.cs` | Calls `MapOrganisationsPost()` after the v1 mappings |
| `src/Api/Program.cs` | Registers the Postgres service; the second OpenAPI document and ReDoc instance (below). The exception handler needs no change: it already strips the `Dtos` namespace, which the new DTOs share |

The handler receives the validated request, maps it, calls the service, maps the stored entities and returns `TypedResults.Created($"/organisations/{id}", response)`. It never reads the version (spec 0004).

### OpenAPI and ReDoc

Two documents from the same code:

| Document | Content | Served at |
|---|---|---|
| `v1` (existing) | The five v1 endpoints only. `OpenApiOptions.ShouldInclude` keeps an operation when its endpoint supports version 1 (`ApiVersionsMetadata.For`), which excludes the v2-only `POST`. The generated document is byte-for-byte what it is today, so the snapshots in `Api.Tests` and `Api.V1.Tests` (X-CONTRACT-01) do not change | `/openapi/v1.json`, ReDoc at `/redoc` |
| `v2` (new) | Operations whose endpoint supports version 2: for now `POST /organisations` only. `Info.Version` is `2.0.0`, and the description says how to select v2 (the `Accept` media type and `version=2`), what the default is, and that `406` is returned for an unsupported version. Each operation documents the `Accept` header as a required header parameter with that value, and the `406` response. The `organisationType` schema lists the ten codes with their meanings from `OrganisationTypeCodes.All`, so consumers build their labels from one source (spec 0003), and its description says which codes need a Companies House number and a `REGISTERED` address and which need a `PRINCIPAL` address and no number. `addressTypes` is an array of the five-value enum with `minItems` 1 and `uniqueItems` true, and its description says that one item lists every kind the address serves as | `/openapi/v2.json`, ReDoc at `/redoc/v2` |

Both paths are already exempt from the version check (`/openapi` and `/redoc` prefixes, spec 0004). `AddOpenApi("v2", ...)` reuses `OpenApiDocumentTransformer`, which reads the document name to set the right `Info`, and adds a v2-only operation transformer for the `Accept` header and the `406`. The v1 document keeps `Version` `0.0.1` and its transformers as they are. `UseReDoc` is called a second time with `RoutePrefix = "redoc/v2"` and `SpecUrl = "/openapi/v2.json"`.

### Tests

Endpoint tests are in `Api.IntegrationTests` (refinement answer, 2 November 2026), against the Compose stack like `OrganisationTests`, through `HttpClient` to `http://localhost:8080` with the `IntegrationTest` JWT and the v2 `Accept` header, and `PostgresDbContext` from `AddPostgres` to check rows. Tests create their own organisations and assert on their own ids, so no Postgres clean-up is added and the Mongo clean-up is not involved. `Testing.Endpoints.Organisations` gains `Post()`.

| Project | Test |
|---|---|
| `Api.IntegrationTests` (new `Scenarios/CreateOrganisationTests.cs`, beside `OrganisationTests.cs`) | Full payload (`LTD` with a Companies House number, one UK address with every part serving as `REGISTERED` and `PRINCIPAL`, a `CORRESPONDENCE` non-UK address without postcode): `201`, `Location` is `/organisations/{id}`, body matches a Verify snapshot with `id` and timestamps scrubbed; the three timestamps are equal; the `organisation` row, two `address` rows and three `organisation_address` rows exist with the sent values, the `REGISTERED` and `PRINCIPAL` rows sharing one `address_id`, `valid_from` set, `valid_to` null, `is_uk_address` true and false |
| | One address with all five kinds: `201`, `addressTypes` returned in the sent order, one `address` row and five `organisation_address` rows. All five kinds as five items: `201`, five `address` rows. The same premises as two items with different kinds: `201`, two `address` rows |
| | Each of the ten codes with the number and address kind its flag requires (a theory over `OrganisationTypeCodes.All`): `201` with the code returned, pinning the flag of every code |
| | Sole trader: `SOLE_TRADER` with `tradingName`, no `companiesHouseNumber` and a `PRINCIPAL` address: `201`, `name` absent from the body and null in the row |
| | `name` and `tradingName` both present on an `LTD`: `201` with both; both present on a `SOLE_TRADER`: `201` with both |
| | Lower-case `companiesHouseNumber` and `countryIso`: `201`, stored and returned upper-case; values with surrounding whitespace are stored trimmed |
| | `addressLine`, `validFrom`, `createdAt`, `id` and `mainActivitySicCode` in the request: `201`, ignored, server values returned |
| | Validation `400`s, each checking `errors` holds the expected key and the body is a Verify snapshot: no `organisation`; `name` missing on an `LTD`; `tradingName` missing on a `SOLE_TRADER`; blank `name`; `homeNation` missing; `homeNation` `GB-XXX`; `organisationType` missing; unknown `organisationType` (reported alone); `companiesHouseNumber` missing on an `LTD`; `companiesHouseNumber` present on a `SOLE_TRADER`; `companiesHouseNumber` `1234567`; `companiesHouseNumber` `IP12345R`; an `LTD` with a `PRINCIPAL` address only; a `SOLE_TRADER` with a `REGISTERED` address only; `addresses` missing; `addresses` empty; `addressTypes` missing; `addressTypes` empty; `REGISTERED` twice in one item; `REGISTERED` in two items; `street` missing; `locality` missing; `countryIso` missing; `countryIso` `GBR`; `GB` without `postalCode`; `GB` with blank `postalCode`; an address with `street`, `locality`, `postalCode` and `countryIso` only (no premises). Several failures in one request report every key |
| | An unknown value in `addressTypes`, `addressTypes` as a string, malformed JSON, `name` as a number, empty body: `400` problem details with a `detail` |
| | `Content-Type: text/plain`: `415` |
| | No credentials: `401`, empty body. Read-only client: `403`, empty body |
| | No `Accept` version and `version=1`: `405` with `Allow: GET`. `version=3`: `406` with the versioning body |
| | Nothing is written to MongoDB: the created id is not found by v1 `GET /organisations/{id}` (`404`) |
| | A `GET /organisations/{id}` for the created id with `version=2`: `406`, pinning the accepted gap until REGEN-140 |
| `Api.IntegrationTests` (changed, above) | `OrganisationSchemaTests`, `DatabaseChangelogTests` (`v1.2.0` tag, upper-case usage types) |
| `Api.Tests` (new `Mapping/AddressLineTests.cs` and `Endpoints/Organisations/PostTests.cs`, beside the v1 tests) | `AddressLine` builder: full address; number and street only; named building only; PO box only; non-UK without postcode; blank parts skipped. Request validation unit tests for the `IValidatableObject` rules (the name, number and address kind rules for both values of the flag; an unknown type reported alone; `addressTypes` empty, repeated in one item, repeated across items; postcode; premises) and for the `errors` keys being JSON paths. `OpenApiTests.OpenApiV2_VerifyAsExpected` snapshot of `/openapi/v2.json`; `OpenApi_VerifyAsExpected` (v1) unchanged |
| `Api.Tests` (changed) | `PostgresDbContextModelTests` column lists |
| `Api.V1.Tests` | No change. Every test and snapshot passes as it is, including X-ROUTE-01 (`POST /organisations` is `405` for v1), X-BODY-01 and X-CONTRACT-01 |

### Supporting changes

| File | Change |
|---|---|
| `README.md` | A short "API v2" note under "API versioning": the `Accept` header value, `/openapi/v2.json` and `/redoc/v2`; and `db.changelog-1.2.xml` in the "Postgres schema changes" section |
| `.github/dependabot.yml` | No change. No package is added |
| `compose.yml`, `Dockerfile` | No change. `changelog/` is already mounted into the `liquibase` service |

### Work breakdown

| Slice | Work | Verified by |
|---|---|---|
| 1 | `db.changelog-1.2.xml` and its include; `OrganisationTypeCodes` and its tests; entity changes; `PostgresDbContextModelTests`, `OrganisationSchemaTests` and `DatabaseChangelogTests` updated | `docker compose up --build -d` applies `v1.2.0`; `Api.Tests` and `Api.IntegrationTests` pass |
| 2 | v2 DTOs, validation and `AddressLine` builder with unit tests | `Api.Tests` passes |
| 3 | Mapping, service, endpoint, `Program.cs` wiring; the create tests | `Api.IntegrationTests` passes |
| 4 | OpenAPI v2 document, `ShouldInclude` on v1, ReDoc v2; snapshots | `Api.Tests` passes with the v1 snapshot unchanged |
| 5 | README; full run | `Api.Tests`, `Api.IntegrationTests` and then `Api.V1.Tests` pass against `docker compose up --build -d`, with no v1 test or snapshot edited; `dotnet csharpier check .` passes |

## Acceptance Criteria

- [ ] `POST /organisations` with `Accept: application/vnd.defra.waste-organisations+json; version=2`, valid credentials with the `write` scope and a valid body returns `201 Created`, `Location: /organisations/{id}` and the stored organisation in an `organisation` envelope with a server-generated UUIDv7 `id`, `validFrom`, `createdAt`, `updatedAt`, and each address with its `addressTypes`, computed `addressLine` and `isUkAddress` (Jira Scenario 1).
- [ ] The organisation, one `address` row per address and one `organisation_address` row per kind in its `addressTypes` are stored in one transaction, with `valid_from`, `created_at` and `updated_at` set by the API and `valid_to` null.
- [ ] `organisationType` is required and is one of the ten codes; for `LTD`, `PLC`, `LIMITED_PARTNERSHIP`, `LLP` and `CIC` the request must carry `name`, `companiesHouseNumber` and a `REGISTERED` address, and for the other five it must carry `tradingName` and a `PRINCIPAL` address and no `companiesHouseNumber`; `homeNation` and `addresses` (one to five items, each with a non-empty `addressTypes`, each kind once in the request) are required; `street`, `locality` and `countryIso` are required on every address, `postalCode` on a `GB` address, and at least one of `subBuildingName`, `buildingName`, `buildingNumber` and `poBox` on every address.
- [ ] A request that breaks any rule above, or sends an unknown `organisationType`, an invalid `companiesHouseNumber`, `homeNation` or `countryIso`, returns `400` with validation problem details whose `errors` names every failing field by its JSON path (Jira Scenario 2).
- [ ] Malformed JSON or a value of the wrong type returns `400` with problem details (Jira Scenario 3).
- [ ] A request with no token returns `401` with an empty body (Jira Scenario 5); one without the `write` scope returns `403` with an empty body.
- [ ] `POST /organisations` without `version=2` returns `405` with `Allow: GET`; a version above 2 returns `406`.
- [ ] `companiesHouseNumber` and `countryIso` are stored upper-case; strings are trimmed; unknown request members, `addressLine`, `validFrom`, `createdAt`, `updatedAt` and `id` included, are ignored.
- [ ] `addressLine` is built in postal order from the stored parts and is not stored.
- [ ] `db.changelog-1.2.xml` makes `organisation.name` nullable with `ck_organisation_name`, drops `address.address_line` and `address.country_name`, recreates `ck_address_premises` without `address_line`, renames the five usage types to `REGISTERED`, `PRINCIPAL`, `AUDIT`, `SERVICE_OF_NOTICE` and `CORRESPONDENCE`, re-seeds `organisation_type` with the ten codes (the six kept rows keeping their ids with upper-cased codes), and tags `v1.2.0`; 1.0 and 1.1 are unchanged; no EF Core migration exists.
- [ ] `/openapi/v2.json` describes the v2 API with `POST /organisations`, the `Accept` header, the `406`, and the ten `organisationType` codes with their meanings and rules; `/redoc/v2` renders it; `/openapi/v1.json` is unchanged (both v1 snapshots pass).
- [ ] v2 code sits in the existing `Endpoints`, `Dtos`, `Mapping` and `Services` folders with no separate v2 folder or namespace; no v1 endpoint, DTO, mapping or service file changes, because none needs to.
- [ ] Nothing is written to MongoDB; no `409` or duplicate check exists.
- [ ] `Api.Tests`, `Api.IntegrationTests` and `Api.V1.Tests` pass locally and in `check-pull-request.yml`; no v1 test or snapshot is edited; `dotnet csharpier check .` (including `changelog/*.xml`) and a warnings-as-errors build pass.
- [ ] No NuGet package is added.

## Out of Scope

- Duplicate detection and `409` (Jira Scenario 4, moved to a future story on 2 November 2026; REGEN-164).
- v2 `GET /organisations/{id}` and `GET /organisations` (REGEN-140); the `Location` header is not resolvable by v2 until then.
- `PATCH` with `changeReason`, subsidiaries, compliance schemes, bulk endpoints (other sections of the Confluence page).
- Writing to MongoDB or any dual write (REGEN-157).
- Identifiers other than the Companies House number: the UTR of a sole trader or of partners, the registered society number, the charity registration number (the page names them; the API has no field for them yet).
- Partner names, whether each partner is an individual or a company, partner roles and minimum counts for the three partnership types (the page requires them; the schema has no partner table).
- Mapping a Companies House lookup result (`company_type` and `company_subtype`) onto the ten codes; the consumer that performs the lookup sends the code.
- Site addresses (not in the schema).
- `mainActivitySicCode`, external ids (`rpdExternalOrganisationId`, `rpdExternalSubsidiaryId`), `isComplianceSchemeOperator`, `complianceSchemeMembership`, `organisationSubType`.
- `countryName` on the wire or `country_name` in the schema.
- A client-settable `validFrom`.
- Field length caps and a country reference list.
- Echoing the version in the response `Content-Type`; declaring v2 on the five v1 endpoints (each v2 story declares its own).
- Non-functional targets (blank on the ticket).

## Decisions

| Topic | Decision |
|---|---|
| Body shape | `organisation` envelope with an `addresses` array, each item carrying an `addressTypes` array, as on the Confluence page updated 3 November 2026 (version 36). Version 34 of 2 November 2026 had a single `addressType` per item; the earlier keyed `addresses` object and top-level `registeredAddress` fields are not used |
| One address, several kinds | An item lists every kind the address serves as and is stored once, with one `organisation_address` row per kind (refinement answer, 3 November 2026, matching the Confluence example; an earlier answer the same day had asked for one kind per item and was reversed). A kind may appear in one item only, so an organisation still has at most one current address per kind ("no two addresses of the same type", refinement answer of 2 November 2026) |
| `addressTypes` values | `REGISTERED`, `PRINCIPAL`, `AUDIT`, `SERVICE_OF_NOTICE`, `CORRESPONDENCE`, used on the wire, in code and in `address_usagetype.name` (refinement answer, 2 November 2026), so no mapping table between three spellings |
| Name rule | `name` mandatory for a Companies House type, `tradingName` mandatory for every other type: the refinement answer of 2 November 2026 ("name with a Companies House number, otherwise tradingName"), keyed on the type since 3 November 2026 because the type now decides the number; `organisation.name` becomes nullable with `ck_organisation_name`, which the rule still satisfies |
| `addressLine` | Not stored; computed in postal order on output; ignored on input (refinement answers, 2 November 2026). Dropped from `ck_address_premises` |
| Premises | At least one of `subBuildingName`, `buildingName`, `buildingNumber`, `poBox`, enforced in validation and by the recreated check, so a valid request cannot hit a `500` on that constraint (recommendation accepted 2 November 2026) |
| `countryIso` | A plain two-letter string, required on every address; `country_name` dropped (refinement answer, 2 November 2026). The address model's `country` object was first kept with `iso` only, so that `name` could return later; on 3 November 2026 it was flattened to `countryIso`, because an object around one value is noise for the client, and a `countryName` can be added beside it later without a breaking change. The column stays `address.country_iso` |
| `postalCode` | Required only when `countryIso` is `GB`, matching `ck_address_uk_postal_code` |
| Mandatory address fields | `street`, `locality`, `countryIso` on every address (refinement answer, 2 November 2026), plus the premises rule; `region` optional |
| Mandatory address kind | A `REGISTERED` address for a Companies House type, a `PRINCIPAL` address for every other type, from the page's "Address Required for Org Creation" column (3 November 2026); the other kinds optional |
| Organisation types | The ten types of the Confluence page "Organisation Types and Subtypes" (3 November 2026), replacing the 36 Companies House `company_type` values plus four of our own that spec 0003 seeded on the team decision of 20 October 2026. The page lists what the service accepts, with the registration authority, identifier and required address per type, which the Companies House list cannot say; a consumer that offers these ten choices would never send the other 30 codes. Spec 0003 is not edited; this row records what it supersedes |
| Type codes | Upper snake case, like `addressTypes` (`SERVICE_OF_NOTICE`), so the two enumerations on the wire follow one convention (3 November 2026); spec 0003's kebab-case Companies House format is superseded. `LTD`, `PLC`, `LIMITED_PARTNERSHIP`, `LLP`, `SOLE_TRADER` and `PARTNERSHIP` are the 1.1 rows `ltd`, `plc`, `limited-partnership`, `llp`, `sole-trader` and `partnership`, keeping their ids with the code upper-cased; the first four map from a Companies House lookup result by upper-casing and replacing `-` with `_`. `PARTNERSHIP` is the page's "General Partnership", kept as the short code because the limited forms have their own codes; `CIC`, `REGISTERED_SOCIETY`, `CHARITABLE_ORGANISATION` and `OVERSEAS_ORGANISATION` are new. The 1.1 `charitable-incorporated-organisation` is not kept because the page's row also covers charitable trusts. Nothing is renamed or removed after publication: the v2 API is not yet published |
| `organisationType` | Required; validated against `OrganisationTypeCodes.All`; drives the name, number and address kind rules through one `RegisteredAtCompaniesHouse` flag |
| `companiesHouseNumber` by type | Required for the five Companies House types, rejected for the other five (the page's identifier column, 3 November 2026). A registered society's number is on the Companies House register too, but the client holds the FCA number and the API has no field for it yet, so none is accepted for that type in this story |
| `companiesHouseNumber` pattern | `^[A-Z0-9]{2}[0-9]{6}$` after upper-casing: the eight-character Companies House format, a prefix of two letters or digits (`SC`, `NI`, `OC`, `LP`, or two digits of an eight-digit number) and six digits. The `IP[0-9]{5}R` form of registered societies on the Companies House register is not accepted, because no type that may carry a number uses it |
| Normalisation | Trim every string; upper-case `companiesHouseNumber` and `countryIso`; nothing else; no length caps |
| Unknown members | Ignored, as `System.Text.Json` and v1 do; never a `400` |
| Server-set fields | `id`, `validFrom`, `validTo`, `createdAt`, `updatedAt`, `isUkAddress`, `addressLine` (refinement answer, 2 November 2026) |
| `201` body | The stored organisation in the same envelope, nulls omitted, timestamps as ISO 8601 with offset (as v1 registrations) |
| `Location` | `/organisations/{id}`, as v1; not resolvable by v2 until REGEN-140 (accepted 2 November 2026) |
| `400` shape | Validation problem details with `errors` keyed by JSON path for field failures; the existing `detail` problem details for unreadable bodies. v1's single-`detail` shape is not copied for field errors because Jira Scenario 2 asks for each missing field |
| `401`/`403` | Empty bodies, as v1 (refinement answer, 2 November 2026) |
| Duplicates | Out of scope; no `409` (refinement answer, 2 November 2026) |
| OpenAPI | A second document `v2` filtered by `ApiVersionsMetadata`, and `ShouldInclude` on `v1` so the v2-only operation stays out of it; ReDoc at `/redoc/v2` (refinement answer, 2 November 2026). Both documents are generated from the code, with summaries and descriptions in the endpoint and DTO declarations; no hand-written OpenAPI file is kept, and the Confluence page remains the contract agreed with the wider teams (huddle of 26 October 2026) |
| Code layout | v2 code in the existing folders, told apart by type names and `.WithApiVersions(2)`; no separate folder or namespace. The ring-fencing of 22 October 2026 was kept lightweight on 26 October 2026: a complete isolation of v1 code into separate files or folders is not intended. v1 files stay as they are only because nothing in them needs to change |
| Tests | Endpoint tests in `Api.IntegrationTests`, against Compose (refinement answer, 2 November 2026); pure logic unit-tested in `Api.Tests` |
| Schema spec | Spec 0003 is not amended; the 1.2 changes are recorded here (refinement answer, 2 November 2026) |
| Schema change in this story | The 1.2 changelog is a hidden task of this story, not a separate ticket: tasks that support the delivery of a story stay in it (huddle of 26 October 2026) |
| Reference lookups | Queried per request, not cached; two tiny tables |
| Transaction | One `SaveChangesAsync`, so a partial organisation is never stored |

## Open Questions

To close with the wider team during implementation or on follow-up stories (huddle of 26 October 2026: a story is not expected to have every answer up front, and gaps are closed while implementing); none blocks this story:

1. **Types the page does not list.** Public bodies (local authorities, NHS trusts, government bodies) and unincorporated associations (clubs, societies) handle waste, and spec 0003 seeded both. They are removed with the Companies House codes the page does not list; if the service accepts them, a row on the page and a changeset inserting the code bring them back without a breaking change.
2. **Companies House lookups outside the ten.** A lookup can return a company limited by guarantee, an unlimited company, a royal charter body, an overseas company with a UK establishment or a legacy type, none of which is a code. The consumer that performs the lookup must map (a guarantee company to `LTD`?) or reject; the same applies to whatever an RPD migration holds. A CIC comes back as `ltd` or `plc` with `company_subtype` `community-interest-company` and maps to `CIC`.
3. **Charities.** The page bundles the charitable incorporated organisation (incorporated) with the charitable trust (unincorporated) under the Charity Commission, which registers charities in England and Wales only; `homeNation` also covers Scotland (OSCR) and Northern Ireland (the Charity Commission for Northern Ireland). A charitable company has a Companies House number and a charity number and is an `LTD` here. The page should say which register applies per nation.
4. **Other identifiers.** The page names a UTR for sole traders and partners (marked "??" on the page), a registered society number and a charity registration number. None has a field; a later story adds them, as separate fields or as a registration number with its authority. UTRs of individuals are personal data, so collecting them needs a privacy decision first.
5. **Partners.** The three partnership types require partner names, individual or company, roles and minimum counts. There is no partner table; this needs its own story and spec.
6. **`homeNation` for an overseas organisation.** It stays required, read as the nation whose regulator the organisation deals with; the page does not say. The `PRINCIPAL` address of an overseas organisation is not cross-checked against `countryIso`.
7. **Registered name for societies and charities.** They have a registered name with the FCA or the Charity Commission, yet the rule asks them for `tradingName` like a sole trader, because the 2 November 2026 rule keyed `name` to Companies House. If `name` should be mandatory for them too, it is a validation change only; `ck_organisation_name` allows both.
8. **Read-back.** REGEN-140 should declare `.WithApiVersions(2)` on a v2 `GET /organisations/{id}` that reads Postgres and returns this spec's `organisation` shape, rebuilding each address's `addressTypes` from its `organisation_address` rows, so `Location` resolves.
9. **Confluence pages.** The "Create Single Organisation" request (version 36, 3 November 2026) still has a stray `]` after the `organisation` object and no `201` response; the `<structured_address_data>` placeholder should list the fields in this spec, with `countryIso` as a string rather than the address model's `country` object. The "Organisation Types and Subtypes" page spells "Cooperative/CCommunity Benefit Scheme", which should read "Co-operative or Community Benefit Society", and has no subtype column despite its title; CIC is the one row that is a Companies House subtype.
10. **Jira wording.** Scenario 5 reads `/organisations/` with a trailing slash; Scenario 2's example list is blank; the description links "Update Single Organisation by ID" to a `...waste-organisations-backend` page.

## Dependencies

- REGEN-167 (merged, PR 217): versioning, `WithApiVersions`, the matcher policy and the middleware.
- Spec 0003 (merged): the 1.1 schema and entities this story changes and writes to.
- Nothing else has to land first. REGEN-140 and REGEN-157 build on this story.

## Risks

| Risk | Level | Mitigation |
|---|---|---|
| The v2-only `POST` leaks into `/openapi/v1.json` and breaks X-CONTRACT-01 | Medium | `ShouldInclude` on the v1 document filters by `ApiVersionsMetadata`; both v1 snapshot tests must pass unchanged |
| Validation and the check constraints drift, so a valid request gets a `500` | Medium | Every constraint has a validation rule in this spec, and the integration tests send each boundary case; `OrganisationSchemaTests` pins the constraints themselves |
| The framework's validation keys are C# names, not JSON paths | Low | Mapped before the response is written if so; the `400` snapshots pin the keys |
| Dropping `address_line` and `country_name` loses data | Low | No environment holds organisation rows yet; the rollback recreates the columns empty |
| Renaming the usage types breaks code that looks them up by the old names | Low | No production code looks a usage type up yet. `OrganisationSchemaTests` and `DatabaseChangelogTests` query by the names and change in slice 1; two `Api.Tests` files only name an in-memory row |
| A client sends one premises as two items with different kinds instead of one item with two kinds | Low | Allowed and stored as two `address` rows; the v2 OpenAPI description of `addressTypes` says how to share one address, and REGEN-140 reads either shape back correctly |
| The type list changes after clients integrate: a type the page lacks (public body) is wanted, or a row is split | Medium | Adding a code is one entry and one changeset, non-breaking; removing or renaming one is breaking, so Open Questions 1 to 3 are to be settled before external consumers go live |
| A consumer's Companies House lookup returns a type that is none of the ten | Medium | The consumer maps or rejects before calling (Open Question 2); the API's `400` names the field, so an unmapped value is visible at once |
| The W&CE-wide versioning approach ends up different from header versioning: the huddle of 26 October 2026 kept room for a change of direction, with a conversation about versioning still pending | Low | The handler never reads the version. Only `.WithApiVersions(2)` on the endpoint and the `Accept` parameter in the v2 OpenAPI document tie the endpoint to the scheme, one place each |
| The `405` for v1 clients looks like a missing feature to someone testing by hand | Low | Documented in the v2 OpenAPI description and README: send the `Accept` header |

## Notes

- Branch name: `feature/REGEN-141`.
- Local commands, as in `AGENTS.md`: build and run `Api.Tests`; then `docker compose up --build -d`, run `Api.IntegrationTests`, then `Api.V1.Tests` on its own, then `docker compose down -v --remove-orphans`. Host port 5432 must be free.
- `OpenApiOptions.ShouldInclude` is the hook the `Microsoft.AspNetCore.OpenApi` package offers for filtering `ApiDescription`s into a document; the endpoint's `ApiVersionsMetadata` is in `ActionDescriptor.EndpointMetadata`.
- `RouteHandlerOptions.ThrowOnBadRequest` is already `true`, so body deserialisation failures reach the exception handler and become problem details with a `detail`, as in v1.
- `AddressLine` examples: `Unit 4, Mill House, 12A High Street, PO Box 99, Testville, Testshire, AB1 2CD`; `12 Harbour Road, Hong Kong`; `Rose Farm, Little Testing, AB1 2CD`; `PO Box 99, Testville, AB1 9ZZ`.

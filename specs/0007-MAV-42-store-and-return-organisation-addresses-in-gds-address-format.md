# MAV-42: Store and return organisation addresses in GDS address format

| | |
|---|---|
| Type | Improvement: the v2 `POST /organisations` and `GET /organisations/{id}` take, store and return an address in the GOV.UK Design System "multiple text inputs" format instead of the nine parts of the Defra standard address model. The Postgres `address` table changes with them. The version stays 2; nothing changes for v1 clients, and nothing else about an organisation changes |
| Priority | P1 (High): it changes the v2 contract, which is cheap only while no service has integrated with v2, and Manage Account needs it (19 November 2026). Jira shows "Lowest" although the story is In Progress |
| Parent | MAV-10 Access to org data via Org API |
| Risk | Medium (see Risks) |
| Source | Jira story MAV-42 as read on 19 November 2026 (business rules, tech considerations, six open questions, Scenarios 1 to 11; no comments); the "multiple text inputs" and postcode guidance of the GOV.UK Design System pattern "Addresses", as the story cites it; the review of DEFRA/waste-organisations pull request 252 that the story names as its trigger; the refinement answers of 19 November 2026 recorded in Decisions; specs 0003, 0005 and 0006 |

## Problem/Opportunity

The v2 API stores and returns an address as nine parts (`subBuildingName`, `buildingName`, `buildingNumber`, `street`, `poBox`, `locality`, `region`, `postalCode`, `countryIso`) plus a computed `addressLine`. Consumers mostly display an address to a user, so they would have to rebuild the lines from the parts. The review of pull request 252 asked for the GDS address format instead, and the team decided to adopt it. MAV-42 changes the create, the retrieve and the storage together, so the `POST` and the `GET` keep one address shape.

What is already in place:

- **The address type on the wire (specs 0005 and 0006):** `src/Api/Dtos/AddressRequest.cs` (the nine parts, the premises rule, the postcode required for `GB`) and `src/Api/Dtos/AddressResponse.cs`, which both v2 endpoints return (spec 0006, Decisions, "One address type"), so the response changes in one place.
- **Mapping:** `src/Api/Mapping/CreateOrganisationExtensions.cs` (request to entity, normalisation, the `AddressParts` key that merges two items holding the same address, entity to `AddressResponse`), `src/Api/Mapping/OrganisationDetailsExtensions.cs` (the read, which calls the same `ToResponse`) and `src/Api/Mapping/AddressLine.cs` (the computed line).
- **Persistence:** `PostgresOrganisationService.FindAddressAsync` looks an address up by its nine parts, and the create retries once on a violation of `ux_address_values`.
- **Schema (changelogs 1.1 and 1.2):** `address` with the nine columns, `ck_address_premises`, `ck_address_uk_postal_code`, `ck_address_country_iso` and the unique index `ux_address_values` over the nine columns with nulls equal. The entity is `src/Api/Data/Postgres/Entities/Address.cs`.
- **Postcode handling:** `NormalisePostalCode` upper-cases and collapses whitespace. There is no format check and punctuation is kept.
- **Versioning (spec 0004), authentication and the v2 OpenAPI document:** unchanged by this story, apart from the address schemas in `/openapi/v2.json`.

Who benefits:

- **RPD and Manage Account:** send and show an address as the lines a user typed or will read, with no rebuilding.
- **Users of those services:** a postcode typed in any common form is accepted.
- **The team:** one address format across the frontends that follow the GOV.UK pattern and the API behind them.

No service consumes v2 yet and no environment holds v2 data that must be kept (19 November 2026), so v2 changes in place and the stored rows are reset.

## Proposed Solution

Replace the address members of the two v2 DTOs, the entity, the mapping, the look-up and the `address` columns with the GDS fields. The schema moves in three steps so that `main` works after every pull request and a schema change never shares one with behaviour: add the new columns (slice 1), switch the API to them (slice 2), remove the old columns (slice 3).

### The address

Each item of `addresses`, in the request and in both responses:

| Field | Required | Rule | Stored in |
|---|---|---|---|
| `addressTypes` | Yes | Unchanged (spec 0005) | `organisation_address`, unchanged |
| `addressLine1` | Yes | Non-blank | `address.address_line_1` |
| `addressLine2` | No | | `address.address_line_2` |
| `townOrCity` | Yes | Non-blank | `address.town_or_city` |
| `county` | No | For a non-UK address, the state or province | `address.county` |
| `postcode` | When `countryIso` is `GB` | See Postcodes | `address.postcode` |
| `countryIso` | Yes | Unchanged: upper-cased, then two letters. Not part of the GOV.UK pattern, kept from the current model | `address.country_iso`, unchanged |

```json
{
  "addressTypes": ["REGISTERED", "PRINCIPAL"],
  "addressLine1": "Unit 4, Mill House",
  "addressLine2": "12A High Street",
  "townOrCity": "Testville",
  "county": "Testshire",
  "postcode": "AB1 2CD",
  "countryIso": "GB"
}
```

- Every string is trimmed. A blank or absent optional field is stored as null and omitted from the response, as today.
- `addressLine1`, `addressLine2`, `townOrCity`, `county` and `postcode` are capped at 255 characters, with the message spec 0005 uses.
- The premises rule goes: no field of the new format identifies premises apart from `addressLine1`, which is required.
- `addressLine` is no longer returned and `AddressLine` is deleted.
- The old members (`subBuildingName`, `buildingName`, `buildingNumber`, `street`, `poBox`, `locality`, `region`, `postalCode`) are unknown members and are ignored, like `addressLine` and every other unknown member (spec 0005). A request in the old format therefore gets `400` naming `addressLine1` and `townOrCity`; a request that sends both formats is stored from the GDS fields alone.
- Non-UK addresses are accepted in the same fields.
- Everything else about `addresses` is as spec 0005 and spec 0006 state it: one to five items, each kind once, the kind the organisation type requires, the order of the response.

### Postcodes

For an address whose `countryIso` is `GB`:

1. Every character that is not a letter or a digit is removed (spaces anywhere, hyphens, dashes, brackets, full stops) and the rest is upper-cased.
2. Nothing left: `400`, "The postcode field is required when countryIso is GB."
3. The result must match `^(GIR0AA|[A-Z]{1,2}[0-9][A-Z0-9]?[0-9][A-Z]{2})$`: an outward code of the form A9, A99, AA9, AA99, A9A or AA9A and an inward code 9AA. Otherwise `400`, "The postcode field must be a full UK postcode."
4. It is stored and returned as the outward code, one space, and the last three characters: `ab1-2cd`, ` AB12CD ` and `(ab1) 2cd.` all become `AB1 2CD`.

The check is of the format only. A well-formed postcode that does not exist is accepted; no list of postcode areas and no external service is used.

For any other `countryIso`, `postcode` is optional and is not checked against a format; it is upper-cased with each run of whitespace inside it made one space, as `postalCode` is today. When `countryIso` is missing or invalid, only that error is reported and the postcode is not checked.

The rule lives in one place, `src/Api/Dtos/Validation/UkPostcode.cs` (`static bool TryNormalise(string? value, out string? postcode)`), used by the validation rule on `AddressRequest` and by the mapping. The same class exposes `static bool IsBlank(string? value)`, true when nothing is left after step 1, so the validation rule can tell the "required" case from the "full UK postcode" case (planning decision, 20 November 2026).

### Address equality

Two addresses are the same when, after the normalisation above, all six stored parts are equal: `addressLine1`, `addressLine2`, `townOrCity`, `county`, `postcode` and `countryIso`, compared exactly, a null equal to a null. This is spec 0005's rule on the new fields. Two request items that are the same address are stored once and come back as one item with the kinds of both (Jira Scenario 9); an address another organisation already stored is reused; `ux_address_values` enforces it and the create's retry on that index stays as it is.

### Errors

As spec 0005, with these keys and messages for an address item `n`:

| Case | Key | Message |
|---|---|---|
| `addressLine1` missing or blank | `organisation.addresses[n].addressLine1` | The addressLine1 field is required. |
| `townOrCity` missing or blank | `organisation.addresses[n].townOrCity` | The townOrCity field is required. |
| `GB` and `postcode` missing or blank | `organisation.addresses[n].postcode` | The postcode field is required when countryIso is GB. |
| `GB` and `postcode` not in the UK format | `organisation.addresses[n].postcode` | The postcode field must be a full UK postcode. |
| A free-text field over 255 characters | The field's path | As spec 0005 |

Messages keep the API's existing style (19 November 2026); the story's "Enter a full UK postcode" was an example, and a frontend writes its own wording. Every failing field is reported. Nothing is reported under the address item's own key any more, since the premises rule is gone. `401`, `403`, `405`, `406`, `415` and the unreadable-body `400` are unchanged for both endpoints.

### Schema: Liquibase changelog

Two new files, each included at the end of `changelog/db.changelog.xml`. Check constraints and the index are `sql` changes with an explicit `rollback`, as in 1.1 and 1.2. No published changeset is edited.

`changelog/db.changelog-1.3.xml` (slice 1, expand):

| Changeset | Change | Rollback |
|---|---|---|
| `1.3-reset-organisation-rows` | Delete every row of `organisation_address`, then `organisation`, then `address`, in that order because the foreign keys restrict. The reference tables are untouched | None: the rows are not restored |
| `1.3-address-add-gds-columns` | `addColumn` `address_line_1`, `address_line_2`, `town_or_city`, `county`, `postcode`, all `text`, nullable | Drop the five columns |
| `1.3-address-drop-part-checks` | Drop `ck_address_premises` and `ck_address_uk_postal_code`: a row written from the GDS fields has none of the old parts | Add both back in their 1.2 form |
| `1.3-ux-address-values` | Drop `ux_address_values` and create it again, under the same name, over the nine old columns and the five new ones, `NULLS NOT DISTINCT`. Rows written either way stay unique, and the service's retry, which reads the index name, works throughout | Drop it and create the 1.2 index |
| `1.3-tag-v1.3.0` | `tagDatabase` `v1.3.0` | Automatic |

`changelog/db.changelog-1.4.xml` (slice 3, contract):

| Changeset | Change | Rollback |
|---|---|---|
| `1.4-delete-nine-part-rows` | Delete every organisation linked to an `address` row whose `address_line_1` is null, with all its links, then those `address` rows: anything the old code stored between the deployment of slice 1 and of slice 2 | None |
| `1.4-address-drop-part-columns` | Drop `ux_address_values`, then `dropColumn` `sub_building_name`, `building_name`, `building_number`, `street`, `po_box`, `locality`, `region`, `postal_code`. The index is dropped explicitly rather than left to Postgres's automatic drop with the columns, so the rollback is deterministic | Add the eight columns, `text`, empty, then create the 1.3 index over the fourteen columns |
| `1.4-ux-address-values` | Create `ux_address_values` over `address_line_1`, `address_line_2`, `town_or_city`, `county`, `postcode`, `country_iso`, `NULLS NOT DISTINCT` | Drop it |
| `1.4-address-gds-checks` | `ck_address_address_line_1` and `ck_address_town_or_city`: the column is not null and not blank. `ck_address_uk_postcode`: `country_iso IS DISTINCT FROM 'GB' OR COALESCE(btrim(postcode), '') <> ''` | Drop the three checks |
| `1.4-tag-v1.4.0` | `tagDatabase` `v1.4.0` | Automatic |

`ck_address_country_iso`, `organisation_address` and its index `ux_organisation_address_current` do not change. The UK postcode format is checked by the API only, not by a constraint. Run `dotnet csharpier check .` locally after editing a changelog file.

### Code layout

| Path | Slice | Change |
|---|---|---|
| `changelog/db.changelog-1.3.xml`, `db.changelog.xml` | 1 | As above |
| `src/Api/Data/Postgres/Entities/Address.cs` | 1 | Gains `AddressLine1`, `AddressLine2`, `TownOrCity`, `County`, `Postcode` with their columns; `HasIndex` lists the fourteen columns. Nothing reads or writes the new properties yet |
| `src/Api/Dtos/AddressRequest.cs` | 2 | The members of "The address"; the premises rule removed; the postcode rule calls `UkPostcode` |
| `src/Api/Dtos/AddressResponse.cs` | 2 | The same members; `addressLine` removed |
| `src/Api/Dtos/Validation/UkPostcode.cs` | 2 | New (Postcodes) |
| `src/Api/Mapping/CreateOrganisationExtensions.cs` | 2 | Maps the GDS fields to the new properties and back; `AddressParts` holds the six parts; `NormalisePostalCode` becomes the `GB` and non-`GB` rules of Postcodes |
| `src/Api/Mapping/AddressLine.cs` | 2 | Deleted, with `tests/Api.Tests/Mapping/AddressLineTests.cs` |
| `src/Api/Services/PostgresOrganisationService.cs` | 2 | `FindAddressAsync` compares the six parts |
| `src/Api/Endpoints/Organisations/Post.cs`, `GetDetails.cs`, `CreateOrganisationSchemaTransformer.cs` | 2 | Descriptions only, where they name an old part or `addressLine` |
| `README.md` | 2 | The "API v2" paragraph names this spec; "Postgres schema changes" names changelogs 1.3 and, in slice 3, 1.4 |
| `changelog/db.changelog-1.4.xml`, `db.changelog.xml` | 3 | As above |
| `src/Api/Data/Postgres/Entities/Address.cs` | 3 | The eight old properties and their columns removed; `HasIndex` lists the six columns; the summary no longer cites the Defra address model |

`OrganisationDetailsExtensions.cs` needs no change: it calls the create's `ToResponse`. No v1 endpoint, DTO, mapping or service changes: v1's `Address` DTO and the Mongo entity are other types. A comment that records one of these decisions points at "spec 0007, Decisions". No NuGet package is added. Nothing new is logged; addresses never reach the log.

### OpenAPI

`/openapi/v2.json`: the `AddressRequest` and `AddressResponse` schemas carry the GDS members; in `AddressRequest`, `addressLine1`, `townOrCity`, `countryIso` and `addressTypes` are required; in `AddressResponse`, only `addressTypes` is required and every GDS member is nullable, like `countryIso` today, because until changelog 1.4 adds its checks a row written by the old code has no `address_line_1` (Risks) and is returned with its types and country only (planning decision, 20 November 2026); the `postcode` description states the rules of Postcodes; `addressLine` and the old parts are gone. `OpenApiTests.OpenApiV2_VerifyAsExpected` is re-verified in slice 2 after its diff is read. `/openapi/v1.json` does not change: `OpenApi_VerifyAsExpected` and X-CONTRACT-01 pass with no diff.

### Tests

`*.verified.*` snapshots are frozen artefacts, so the ones this story changes on purpose are named here. All of them belong to v2 tests in `Api.Tests` and `Api.IntegrationTests` and change in slice 2 only: the snapshots of `PostTests`, `GetDetailsTests`, `OpenApiTests.OpenApiV2_VerifyAsExpected`, `CreateOrganisationTests` and `CreateOrganisationValidationTests`, each accepted after its diff shows the address fields, keys and messages of this spec and nothing else. A scenario that is renamed gets a new snapshot file and its old one is deleted. No other snapshot changes, and none in `tests/Api.V1.Tests`; a difference anywhere else means stop and ask. Test data is synthetic, as it is today.

| Project | Slice | Test |
|---|---|---|
| `Api.Tests` (`Data/PostgresDbContextModelTests.cs`) | 1, 3 | Column lists and the index definition follow each changelog |
| `Api.IntegrationTests` (`Scenarios/DatabaseChangelogTests.cs`) | 1, 3 | The `v1.3.0` tag, then the `v1.4.0` tag |
| `Api.IntegrationTests` (`Scenarios/OrganisationSchemaTests.cs`) | 1 | The cases for `ck_address_premises` and `ck_address_uk_postal_code` removed; a second row with the same fourteen values is rejected by `ux_address_values`; two rows that differ in one new column are accepted. The create and read tests pass unchanged, which shows the API still works |
| `Api.Tests` (new `Dtos/UkPostcodeTests.cs`) | 2 | A theory of accepted inputs with their stored form (lower case, no space, extra spaces at the start, middle and end, hyphen, brackets, full stop, every outward form, `GIR 0AA`), and of rejected inputs (too short, too long, letters where digits belong, punctuation only) |
| `Api.Tests` (`Dtos/CreateOrganisationRequestValidatorTests.cs`, `CreateOrganisationRequestTests.cs`) | 2 | `addressLine1` and `townOrCity` missing and blank; `GB` with no, blank and malformed postcode; non-`GB` with no postcode and with a value that is not a UK postcode, both valid; 256 characters in each free-text field; no premises error; keys are JSON paths |
| `Api.Tests` (`Mapping/CreateOrganisationExtensionsTests.cs`, `OrganisationDetailsExtensionsTests.cs`) | 2 | Fields trimmed; blank and absent `addressLine2` and `county` are one address; two items that differ only in postcode spelling (`ab12cd`, `AB1 2CD`) are one `Address`; a non-`GB` postcode keeps its punctuation; the response has the GDS members and no `addressLine` |
| `Api.Tests` (`Endpoints/Organisations/PostTests.cs`, `GetDetailsTests.cs`, `Endpoints/OpenApi`) | 2 | Payloads and snapshots in the GDS format; `WhenStreetAndTradingNameMissing_ShouldReportBoth` and `WhenSecondAddressIsUkWithoutPostalCode_ShouldBeValidationProblem` renamed after `addressLine1` and `postcode`; the v1 OpenAPI snapshot unchanged |
| `Api.Tests` (`Services/PostgresOrganisationServiceTests.cs`) | 2 | The look-up and the retry with the new properties |
| `Api.IntegrationTests` (`Scenarios/CreateOrganisationTests.cs`, `Services/PostgresOrganisationServiceTests.cs`) | 2 | Full payload: `201`, body snapshot, and the `address` rows hold the GDS columns with the eight old columns null. No `addressLine2` and no `county`: `201`, both absent from the body and null in the row. Postcode sent as `ab1-2cd`: returned and stored as `AB1 2CD`. Same address in two items, one with the postcode spelt differently: one row, one item with both kinds. A second organisation with a stored address: still one row. An address that differs in one field, or only in case: a second row. Old parts sent beside the GDS fields: `201`, ignored. A non-UK address without postcode: `201` |
| `Api.IntegrationTests` (`Scenarios/CreateOrganisationValidationTests.cs`) | 2 | Scenarios `StreetMissing`, `LocalityMissing`, `GbWithoutPostalCode` and `GbWithBlankPostalCode` become `AddressLine1Missing`, `TownOrCityMissing`, `GbWithoutPostcode` and `GbWithBlankPostcode`; `NoPremises` is removed; new `GbWithMalformedPostcode` and `OldFormatAddress` (only the nine parts sent, with `countryIso` `GB`: `400` naming `addressLine1`, `townOrCity` and, because the item is `GB` without a `postcode`, `postcode`); `SeveralFailures` re-verified |
| `Api.IntegrationTests` (`Scenarios/GetOrganisationDetailsTests.cs`) | 2 | The addresses of the `200` equal those of the `201`, in the GDS fields, with no `addressLine` |
| `Api.IntegrationTests` (`Scenarios/OrganisationSchemaTests.cs`) | 3 | The eight old columns are gone; `ck_address_address_line_1`, `ck_address_town_or_city` and `ck_address_uk_postcode` each reject their case; `ux_address_values` rejects a second row with the same six values, including the same nulls, and accepts one that differs in one |
| `Api.V1.Tests` | all | No test or snapshot changes; every test passes as it is (Jira Scenario 10) |

### Work breakdown

Three pull requests, because the constraints of this repo keep a schema change apart from behaviour and the whole would exceed the size guide. Estimates in non-generated changed lines: about 350, 750 and 300.

| Slice | Work | Verified by |
|---|---|---|
| 1 | Expand. `db.changelog-1.3.xml` and its include; the five new properties and the fourteen-column index on the `Address` entity; the schema and model tests. Stops there: no DTO, validation, mapping or service change, and the API behaves exactly as before | `Api.Tests`; `Api.IntegrationTests` against Compose, where Liquibase applies `v1.3.0` and the unchanged create and read tests pass; `Api.V1.Tests`. Criteria 9 (the 1.3 part) and 11 |
| 2 | Switch. "The address", "Postcodes", "Address equality", "Errors" and "OpenAPI": the two DTOs, `UkPostcode`, the mapping, `FindAddressAsync`, `AddressLine` deleted, the descriptions, the README, and every slice 2 test and snapshot in Tests. The entity keeps the eight old properties, unused, and no changelog file is added | `Api.Tests`, `Api.IntegrationTests` and `Api.V1.Tests`; the v1 OpenAPI snapshots pass with no diff. Criteria 1 to 8, 10 and 11 |
| 3 | Contract. `db.changelog-1.4.xml` and its include; the eight old properties removed from the entity and the index reduced to six columns; the schema and model tests; the README names 1.4. No behaviour change | `Api.Tests`; `Api.IntegrationTests` against Compose, where Liquibase applies `v1.4.0` and the slice 2 tests pass unchanged; `Api.V1.Tests`. Criteria 9 (the 1.4 part) and 11 |

## Acceptance Criteria

- [ ] 1. A v2 `POST /organisations` whose addresses carry `addressLine1`, `addressLine2`, `townOrCity`, `county`, `postcode`, `countryIso` and `addressTypes` returns `201` with each address in those fields; the response has no `addressLine` and none of the eight old parts (Jira Scenario 1).
- [ ] 2. A v2 `GET /organisations/{id}` returns the organisation's current addresses in the same fields, equal to those of the `201` (Jira Scenario 2).
- [ ] 3. An address without `addressLine2` and `county` returns `201`; both are absent from the body and null in the stored row (Jira Scenario 3).
- [ ] 4. An address with `addressLine1` or `townOrCity` missing or blank, or with `countryIso` `GB` and `postcode` missing or blank, returns `400` with validation problem details naming each failing field by its JSON path; a free-text address field over 255 characters does the same; nothing is stored (Jira Scenario 4).
- [ ] 5. A `GB` postcode sent in lower case, without a space, with extra spaces or with punctuation, such as `ab1-2cd`, returns `201` and is stored and returned as `AB1 2CD` (Jira Scenario 5).
- [ ] 6. A `GB` postcode that does not match the UK format returns `400` with "The postcode field must be a full UK postcode." under `organisation.addresses[n].postcode` (Jira Scenario 6).
- [ ] 7. An address whose `countryIso` is not `GB` returns `201` without a postcode, and with a postcode that is not in the UK format, which is stored upper-cased with its punctuation kept.
- [ ] 8. The old address parts are ignored: sent beside the GDS fields the request returns `201` and nothing of them is stored or returned; sent alone it returns `400` naming `addressLine1` and `townOrCity` (Jira Scenario 7).
- [ ] 9. `address` rows hold `address_line_1`, `address_line_2`, `town_or_city`, `county`, `postcode` and `country_iso`. `db.changelog-1.3.xml` deletes the rows of `organisation_address`, `organisation` and `address`, adds the five columns and tags `v1.3.0`; `db.changelog-1.4.xml` drops the eight old columns, leaves `ux_address_values` over the six columns and the three checks of "Schema", and tags `v1.4.0`. Changelogs 1.0 to 1.2 are unchanged and no EF Core migration exists (Jira Scenario 8).
- [ ] 10. Two request items with the same six normalised values, postcode spelling aside, are stored as one `address` row and returned as one item with both `addressTypes`; an address another organisation stored is reused; an address that differs in one field or in case is a second row (Jira Scenario 9).
- [ ] 11. A request that does not ask for version 2 behaves as before on every `/organisations` endpoint: `Api.V1.Tests` passes with no test or snapshot changed, `/openapi/v1.json` is unchanged, and `/openapi/v2.json` describes the GDS address for both endpoints with the postcode rules (Jira Scenarios 10 and 11).
- [ ] 12. For both endpoints a caller without valid credentials still gets `401`, one without the scope `403`, and a version above 2 `406`; after every slice the full gate passes and no NuGet package is added.

## Out of Scope

- v1 endpoints and their data in MongoDB.
- A new API version: v2 changes in place.
- Migrating stored v2 addresses to the new fields: the rows are reset.
- A UPRN on an address. The GOV.UK pattern asks services that store property information to identify addresses by UPRN; it is left to a later story and can be added as an optional member without breaking clients (19 November 2026).
- An address look-up, validation against an external service, a list of real postcode areas, and any check that a postcode exists.
- A format check for non-UK postcodes, and country-specific address formats.
- Rejecting the old address parts by name.
- Duplicate detection across organisations (MAV-17).
- A computed one-line address.
- Changes to `addressTypes`, to the address kind an organisation type requires, or to any organisation field.
- GOV.UK wording of error messages; that is the frontend's.
- An endpoint that updates an address.

## Decisions

All taken in refinement on 19 November 2026 unless a date is given.

| Topic | Decision |
|---|---|
| Address format | The "multiple text inputs" format of the GOV.UK Design System pattern "Addresses", with `countryIso` and `addressTypes` kept (team decision recorded on the story). This supersedes spec 0005 in its "Address fields", in the Decisions "Premises", "Mandatory address fields", "`postalCode`" and "`addressLine`", and in the nine-part key of "Address equality" and "Shared addresses", whose rules otherwise stand on the six new parts; it supersedes spec 0006 in the address members of its response and in "`addressLine` computed". Neither spec is edited |
| Version | v2 changes in place: no service consumes v2 yet, and only v1 is frozen. The story's "all solutions will need to be versioned" is met by v1 staying as it is |
| Stored data | Reset, not migrated: no environment holds v2 data to keep. Organisations are deleted with their addresses, so none is left without the address its type requires |
| Old parts | Ignored as unknown members, as spec 0005 decided for every unknown member; no special rejection. Jira Scenario 7 is settled as "ignored" |
| `addressLine` | Dropped: the lines are returned directly, and the name would confuse beside `addressLine1` |
| Non-UK addresses | Accepted in the same fields, with `postcode` optional and unchecked and `county` used for a state or province. `OVERSEAS_ORGANISATION` must carry a `PRINCIPAL` address (spec 0005), so they cannot be refused |
| "Not a real postcode" | A check of the UK format only. Validation against an external service is out of the story's scope, and a list of postcode areas would need maintaining |
| Postcode input | Every character that is not a letter or digit is ignored, as the pattern asks. This widens spec 0005's rule of 6 November 2026, which upper-cased and collapsed whitespace only |
| Stored postcode | A `GB` postcode is normalised to the expected format, one space between the outward and the inward part, and its punctuation is not kept: `AB1 2CD` (team decision, reported on 19 November 2026, confirming the refinement answer of that day). Equality and the response then do not depend on how a client typed it. The decision covers `GB` postcodes only: a non-UK postcode has no outward and inward part, and a hyphen can be part of the code, so it keeps its punctuation and is upper-cased with its whitespace collapsed, as before |
| Messages | The API's existing style, naming the field; not the GOV.UK wording |
| UPRN | A later story |
| Free-text cap | 255 characters, as spec 0005; `ux_address_values` still needs it |
| Slices | Expand, switch, contract, so that no pull request mixes schema with behaviour and `main` works after each. The story expected "the schema change first, then the behaviour"; the removal of the old columns is the third step that makes that possible |
| Index name | `ux_address_values` keeps its name through 1.3 and 1.4, so the create's retry needs no change |
| Column names | `address_line_1`, `address_line_2`, `town_or_city`, `county`, `postcode`: the wire names in snake case |
| Priority | P1, not Jira's "Lowest" |

## Open Questions

None is open. All three were answered on 19 November 2026:

1. ~~**Jira wording.**~~ Done: the story's description now carries the answer to each of its six open questions, the postcode and slicing decisions, and Scenarios 7 and 8 without "TBC"; its priority was changed from Lowest to High.
2. ~~**Confluence.**~~ Done: on the page "Organisations API - waste-organisations", the address placeholder in the examples of "Retrieve Single Organisation by ID" and "Create Single Organisation" was replaced by the GDS fields. The page never listed the nine parts. The examples of "Update Single Organisation" and "Get Subsidiaries for Organisation" keep the placeholder: those endpoints are not part of this story.
3. ~~**Deployed environments.**~~ Confirmed: no environment holds v2 organisations that must be kept, so the reset can be deployed without a check per environment.

## Dependencies

- MAV-11 (spec 0005, as REGEN-141) and MAV-12 (spec 0006, as REGEN-140), both merged: the endpoints, the shared `AddressResponse` and the schema this story changes.
- Nothing else has to land first. A later update endpoint and the UPRN story build on the new address.
- The slices deploy in order. Slice 3 must not be deployed before slice 2 is running everywhere.

## Risks

| Risk | Level | Mitigation |
|---|---|---|
| The reset deletes organisations someone wanted | Medium | Decided on 19 November 2026 on the answer that none is kept, confirmed the same day for the deployed environments (Open Question 3); the pull request's "How to test" says the changelog deletes the rows |
| A service has started to integrate with the nine-part v2 and breaks | Medium | The answer of 19 November 2026 is that none has; the v2 OpenAPI document changes with slice 2, and the Confluence page already shows the GDS fields (Open Question 2) |
| An organisation is created by the old code between the deployments of slices 1 and 2, and its address has no `address_line_1` | Low | Slice 2 returns such an address with its types and country only; `1.4-delete-nine-part-rows` removes it before the checks are added |
| Between slices 1 and 3 the database does not enforce the premises or UK postcode rule | Low | The API's validation does, in both the old and the new code; 1.4 adds the final checks |
| The UK format rejects a genuine but unusual postcode | Low | The pattern covers every current outward form and `GIR 0AA`; `UkPostcodeTests` lists them; a non-UK territory postcode is sent with its own `countryIso` and is not checked |
| Stripping punctuation turns a mistyped value into a valid postcode | Low | Accepted: it is what the GOV.UK pattern asks for, and the response shows the stored form |
| A v2 snapshot is accepted with an unintended change | Low | Tests names the snapshots that may change and what their diff may show; any other difference is a stop |
| Slice 2 runs over the size guide | Low | Estimated at about 750 lines, most of them tests. If it runs over, the figure goes on the pull request's `Size:` line and the reviewers decide; the split point would be the postcode rules |

## Notes

- Branches: `MAV-42-gds-address-format-slice-1`, `-slice-2`, `-slice-3`.
- The key MAV-42 also appears on commit `fe8a339`, "Migrate to the Manage Account AI toolkit", which is unrelated to this story.
- Stories of the earlier specs were renumbered: REGEN-141 is MAV-11 and REGEN-140 is MAV-12; the spec file names keep the old keys.
- Postcode examples: `ab1-2cd`, `AB12CD`, ` ab1  2cd ` and `(AB1) 2CD.` are all `AB1 2CD`; `M1 1AA`, `B33 8TH`, `CR2 6XH`, `DN55 1PT`, `W1A 1HQ`, `EC1A 1BB` and `GIR 0AA` are valid; `AB1`, `12345` and `AB1 2CDE` are not.
- The pull request of slice 1 says in "How to test" that the changelog must be applied and that it deletes the v2 organisation rows.

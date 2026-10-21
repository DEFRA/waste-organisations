# REGEN-141: Organisation and Address Postgres Schema

| | |
|---|---|
| Type | Task: schema and data access. No change to what the API does for its current clients |
| Priority | P1 (High): a pre-task of REGEN-141 (Create an Organisation), which is in progress and cannot persist anything without these tables |
| Parent | REGEN-141 Create an Organisation, under REGEN-125 Access to org data via Org API |
| Risk | Medium (see Risks) |
| Source | Jira story REGEN-141 and its comments (stand-up notes of 16 October 2026, Ogonna Iwunze's questions), the "Data payloads" table on the Confluence page "Access to org data via Org API", the domain model on the Miro board (`uXjVHUfA4ZE=`, as a screenshot on 16 October 2026), and the refinement answers of 16 October 2026, and the team decisions on organisation type of 20 October 2026 (REGEN-141 comment) |

## Problem/Opportunity

REGEN-141 adds a v2 `POST /organisations` that stores organisations in Postgres. REGEN-146 gave the API a Postgres connection, an empty `PostgresDbContext` and a Liquibase changelog that only tags the baseline `v1.0.0`. There are no tables yet, so the create endpoint has nowhere to write.

The domain model on the Miro board describes the tables. This story builds the organisation-specific part of it: `organisation`, `organisation_type`, `organisation_address`, `address_usagetype` and `address`, with the EF Core entities that map them. The endpoint stories then start from tables, entities and tests that already work.

Who benefits:

- **Engineers building v2:** the create, get and update endpoints map payloads onto existing entities instead of designing the schema as they go.
- **The team:** the schema is reviewed once, with the questions the board leaves open recorded instead of settled implicitly by whoever writes the first endpoint.

All MongoDB behaviour stays exactly as it is.

### The domain model (Miro)

The board's tables in scope, as drawn:

| Table | Columns |
|---|---|
| ORGANISATION | OrganisationId (PK, UUID), OrganisationName, TradingName, CompaniesHouseNumber, HomeNationCode, NationCode, MainActivitySic, OrganisationTypeCode, OrganisationSubTypeCode, OrganisationType (all VARCHAR), OrganisationSize (INTEGER), Turnover (BIGINT), ValidFrom, ValidTo, CreatedAt, UpdatedAt (TIMESTAMP) |
| ORGANISATION_TYPE | OrganisationTypeId (PK, UUID), OrganisationId (FK, UUID), IsAPartnership (BOOLEAN), OrganisationType (VARCHAR: "Partnership, limited liability, etc.") |
| ORGANISATION_ADDRESS | OrganisationAddressId (PK), OrganisationId (FK), AddressId (FK), UsageTypeId (FK) (all UUID), ValidFrom, ValidTo, CreatedAt, UpdatedAt |
| ADDRESS_USAGETYPE | UsageTypeId (PK, UUID), OrganisationAddressId (FK, UUID), UsageType (VARCHAR: Registered, Audit, ServiceOfNotice, Principal, Organisation, Site) |
| ADDRESS | AddressId (PK, UUID), AddressLine1, AddressLine2, City, County, Postcode, Country, StateProvinceOrRegion, PhoneNumber (all VARCHAR), ValidFrom, ValidTo, CreatedAt, UpdatedAt. Note on AddressLine1: "+ Postcode = natural key for dedupe" |

Relationships: an organisation "has an" organisation type, "uses" organisation addresses; an organisation address "has a type" (usage type) and "uses" an address.

The board also has SUBSIDIARY, ORGANISATION_LIFECYCLE, SchemeOperator, SchemeMember, ComplianceScheme and ComplianceSchemeLifeCycle. They are out of scope (Out of Scope).

## Proposed Solution

Add a `db.changelog-1.1.xml` that creates the five tables and seeds the organisation types and address usage types, and map them with EF Core entities in `PostgresDbContext`. The data the API exposes, as described on the Organisations API Confluence page, drives the schema; the Miro board, derived from the data RPD holds in Synapse, is a reference only (team decisions, 20 October 2026). The changes from the board and the conventions are in Decisions: snake_case names, `text` and `timestamptz` types, UUIDv7 keys set by the application, no circular foreign key between `organisation_address` and `address_usagetype`, `ON DELETE RESTRICT`, address history kept through `valid_from`/`valid_to` on `organisation_address`, and timestamps set by the application, not by database defaults.

### Conventions (all tables)

- Table and column names are snake_case and unquoted: `OrganisationId` becomes `organisation_id`, `PostalCode` becomes `postal_code`.
- Primary keys are `uuid` with no default. The API sets them with `Guid.CreateVersion7()`, so keys are time-ordered and index well.
- Every table's own key is the column `id` and the entity property `Id`, matching the Mongo `Organisation` entity, the v1 DTOs and the identifiers on the Organisations API Confluence page (PR review). A reference to another table keeps its name: column `<thing>_id`, property `<Thing>Id` (for example `organisation_address.organisation_id` and `OrganisationAddress.OrganisationId`). The board names keys after their table (`OrganisationId`, `AddressId`); this spec does not.
- The board's VARCHAR columns are `text`. No lengths until the payloads are final: a length is a constraint, and the Confluence payloads are expected to evolve.
- TIMESTAMP columns are `timestamptz`. DATE columns (none in scope) would stay `date`.
- `created_at` and `updated_at` are `timestamptz NOT NULL` with no default. A database default would never apply: EF Core sends every mapped column in an `INSERT`, so an unset `DateTime` would be stored as `-infinity` (PR review). Only the service sets them: `TimestampInterceptor` (EF Core entities) writes them from `TimeProvider`, ignoring any value on the entity; there is no trigger.
- Where a table has `valid_from` and `valid_to`: `valid_from` is `timestamptz NOT NULL` with no default, set by `TimestampInterceptor` when the caller leaves it unset; `valid_to` is nullable, and `NULL` means "current". A check constraint enforces `valid_to IS NULL OR valid_to > valid_from`.
- Every foreign key is `ON DELETE RESTRICT`. History is closed with `valid_to`, never removed by a cascade.
- Every foreign key column has an index; Postgres does not create one for a foreign key. Where a unique constraint already covers the column, its index serves and no second one is added.
- The tables live in the `public` schema, like `databasechangelog`.

### Tables

#### `organisation`

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `uuid` | NOT NULL | PK |
| `name` | `text` | NOT NULL | The organisation's name; `name` in the Confluence payloads |
| `trading_name` | `text` | NULL | |
| `companies_house_number` | `text` | NULL | Not every organisation type is on Companies House (stand-up, 16 October 2026) |
| `home_nation` | `text` | NOT NULL | `homeNation` on the API page. One of `GB-ENG`, `GB-SCT`, `GB-WLS`, `GB-NIR` (ISO 3166-2:GB), enforced by `ck_organisation_home_nation` |
| `organisation_type_id` | `uuid` | NULL | FK to `organisation_type`; index `ix_organisation_organisation_type_id`. The API's `organisationType` carries that row's `code` |
| `valid_from` | `timestamptz` | NOT NULL | Set by `TimestampInterceptor` |
| `valid_to` | `timestamptz` | NULL | |
| `created_at` | `timestamptz` | NOT NULL | Set by `TimestampInterceptor` |
| `updated_at` | `timestamptz` | NOT NULL | Set by `TimestampInterceptor` |

The board's `HomeNationCode` and `NationCode` are replaced by `home_nation`, and its `MainActivitySic` is not built (not an MVP field). Its `OrganisationSize` and `Turnover` are not built either, nor its `OrganisationTypeCode`, `OrganisationSubTypeCode` and `OrganisationType` columns, which `organisation_type_id` replaces (Decisions).

`name` and `home_nation` are the only business columns that are `NOT NULL`. The Jira questions also list "address" as mandatory; a row cannot require a child row, so the create endpoint enforces that.

No unique constraint on name, Companies House number or any combination: how duplicates are detected is still being decided (Open Questions).

#### `organisation_type`

A seeded reference list of legal forms: one row per form, shared by every organisation of that form (team decisions, 20 October 2026). Unlike the board, it is not a child of `organisation`: `organisation` points at it through `organisation_type_id`, so an organisation has at most one type.

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `uuid` | NOT NULL | PK |
| `code` | `text` | NOT NULL | Unique (`uq_organisation_type_code`) |

`organisationType` is the type in the legal sense, for example what Companies House returns (stand-up, 16 October 2026). The table is seeded with 40 codes:

- All 36 Companies House `company_type` values, as published in [`companieshouse/api-enumerations` `constants.yml`](https://github.com/companieshouse/api-enumerations/blob/master/constants.yml) (commit `98b9594`, 10 September 2026), legacy ones included, so any value a Companies House lookup or an RPD migration returns can be stored as it is.
- Four of our own, for forms Companies House does not register: `sole-trader`, `partnership` (unregistered, England and Wales), `unincorporated-association` and `public-body`. They must not be confused with Companies House's own `other` and `unregistered-company`.

Every code is equally valid; there is no "common" subset. Which codes a UI offers, and how it labels them, is up to the UI.

There is no `name` column. The API carries only the code, and each consumer owns its labels, English and Welsh, so a UI can handle Welsh as it sees fit (team decisions, 20 October 2026). A `name` can be added later as a nullable column and a changeset that fills it.

So that consumers can build their labels from one source, the codes and their meanings are published in the API contract:

- `OrganisationTypeCodes.All` (`src/Api/Data/Postgres/OrganisationTypeCodes.cs`), one entry per code with its description, is the source of truth. The v2 create and read endpoints, which are REGEN-141's own work and not this story's, publish it: the OpenAPI (Swagger) document lists the allowed `organisationType` values with those descriptions.
- Descriptions are Companies House's own, except where Companies House repeats one: `investment-company-with-variable-capital`, `icvc-securities`, `icvc-warrant` and `icvc-umbrella` all read "Investment company with variable capital", so each gets a description that tells them apart.
- An integration test fails if the codes in `organisation_type` differ from the C# list, so the seed and the published contract cannot drift.
- A new code is an `insert` changeset plus an entry in the C# list. A published code is never renamed or deleted; Companies House keeps its legacy values, and so does this table.

On create, an `organisationType` that is not in the list is rejected with `400`, naming the field (REGEN-141 scenario 3). Each consumer should agree a fallback for a code it does not yet know, for example showing "Other" and logging it, so that adding a code never breaks a UI.

Not built: a `name` (above), `is_a_partnership` (derivable from the partnership codes: `llp`, `limited-partnership`, `scottish-partnership`, `partnership`) and an organisation sub-type (not an MVP field) (team decisions, 20 October 2026).

#### `address_usagetype`

A seeded reference list. Unlike the board, it has no `organisation_address_id`: the board draws foreign keys in both directions between the two tables, which would make each row depend on the other. Only `organisation_address` points at it.

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `uuid` | NOT NULL | PK |
| `name` | `text` | NOT NULL | Unique (`uq_address_usagetype_name`) |

Seeded in the changelog with fixed UUID literals, one row each:

| `name` |
|---|
| `Registered` |
| `Audit` |
| `ServiceOfNotice` |
| `Principal` |
| `Correspondence` |

These are the five kinds of address on the API page: `registeredAddress`, `auditAddress`, `serviceOfNoticeAddress`, `principalAddress` and `correspondenceAddress`. The board's `Site` and `Organisation` are not seeded: `Site` is specific to RE/EX, and `Organisation` is on neither the API page nor the Confluence payloads. The team first agreed to drop `Audit` as well, as specific to RE/EX, but kept it after Darren found it in RPD's data (Ben Wakefield's data analysis; decided 21 October 2026). A new value is a new changeset that inserts a row, not a schema change.

The code looks a usage type up by `name`, not by a hard-coded id.

#### `address`

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `uuid` | NOT NULL | PK |
| `address_line` | `text` | NULL | `addressLine`: free text for anything the structured fields cannot hold |
| `sub_building_name` | `text` | NULL | `subBuildingName`, for example "Flat 2" or "Unit 4" |
| `building_name` | `text` | NULL | `buildingName` |
| `building_number` | `text` | NULL | `buildingNumber`. Text, not a number: "12A" and "3-5" are building numbers |
| `street` | `text` | NULL | `street` |
| `locality` | `text` | NULL | `locality`: the town or city, for example London, as in the Companies House model. The Mongo `Address` calls this `Town` |
| `region` | `text` | NULL | `region`, for example Surrey: the county, state or province |
| `postal_code` | `text` | NULL | `postalCode` |
| `po_box` | `text` | NULL | `po_box` |
| `country_iso` | `text` | NULL | `country.iso`: ISO 3166-1 alpha-2, for example `GB`. `ck_address_country_iso` allows only two upper-case letters |
| `country_name` | `text` | NULL | `country.name` |
| `is_uk_address` | `boolean` | NULL | `isUkAddress`. Generated: `GENERATED ALWAYS AS (country_iso = 'GB') STORED`, so `NULL` when the country is unknown |
| `created_at` | `timestamptz` | NOT NULL | Set by `TimestampInterceptor` |
| `updated_at` | `timestamptz` | NOT NULL | Set by `TimestampInterceptor` |

The columns are the Defra standard address model ("Defra Address Model" on the Confluence page "Companies House - Address Cleansing", Integration space), adopted now so the service matches the address model Defra expects going forward (team decisions, 20 October 2026). The model is not the board's, which reflects how RPD stores addresses today. RPD and RE/EX have not adopted the Defra model yet, so migrating their addresses will need translating to it later.

Every text column is nullable: the model marks no field as required, and which ones the create endpoint requires belongs to that story. The model's `country` is an object with `iso` and `name`, so it is two columns. A UK address is exactly one whose ISO 3166-1 code is `GB` (Northern Ireland included; the Crown Dependencies have their own codes), so `is_uk_address` is a stored generated column: it is a real, queryable column matching the Defra field, it can never contradict `country_iso`, and nobody sets it. EF maps it as computed, so it is never written and is read back after a save. Only the alpha-2 format of `country_iso` is checked; a reference list of countries can be added later.

The board's `PhoneNumber` is not built: a phone number is not part of an address, and neither the Defra model nor the API page has one.

`address` has no `valid_from`/`valid_to`, unlike the board. An address row belongs to exactly one `organisation_address` row (not shared), so its validity is the validity of that row, and keeping a second pair here would leave two places to close history with nothing keeping them in step (PR review). `organisation_address` is the only history.

Addresses are not shared between organisations or usage types: each `organisation_address` row has its own `address` row. The board's natural key for dedupe (`AddressLine1` + `Postcode`) is not built: the Defra model has no `AddressLine1`, and duplicate detection is a later story, which can add the index it needs.

#### `organisation_address`

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `uuid` | NOT NULL | PK |
| `organisation_id` | `uuid` | NOT NULL | FK to `organisation` |
| `address_id` | `uuid` | NOT NULL | FK to `address` |
| `usage_type_id` | `uuid` | NOT NULL | FK to `address_usagetype` |
| `valid_from` | `timestamptz` | NOT NULL | Set by `TimestampInterceptor` |
| `valid_to` | `timestamptz` | NULL | |
| `created_at` | `timestamptz` | NOT NULL | Set by `TimestampInterceptor` |
| `updated_at` | `timestamptz` | NOT NULL | Set by `TimestampInterceptor` |

History is kept: changing an organisation's registered address sets `valid_to` on the current `organisation_address` row and inserts a new one with a new `address`. A partial unique index, `ux_organisation_address_current`, on `(organisation_id, usage_type_id) WHERE valid_to IS NULL` allows one current address per organisation per usage type and any number of closed ones.

One address may carry several usage types for the same organisation only as separate `address` rows (no sharing).

### Schema: Liquibase changelog

| Path | Content |
|---|---|
| `changelog/db.changelog-1.1.xml` | New. One changeset per table, in dependency order (`organisation`, `organisation_type`, `address_usagetype`, `address`, `organisation_address`), changesets that insert the organisation types and the usage types, and a last changeset that tags the database `v1.1.0` |
| `changelog/db.changelog.xml` | Includes `db.changelog-1.1.xml` after `db.changelog-1.0.xml` |

Changesets use Liquibase's XML change types (`createTable`, `addForeignKeyConstraint`, `createIndex`, `insert`, `tagDatabase`). The partial unique index and the check constraints have no XML change type that expresses them, so they are `sql` changes with an explicit `rollback`. `db.changelog-1.0.xml` is not edited (README: a published changeset is never changed).

`dotnet csharpier check .` covers `changelog/*.xml` locally; CI does not (AGENTS.md), so run it after editing.

### EF Core entities

In `src/Api/Data/Postgres/Entities/`, namespace `Defra.WasteOrganisations.Api.Data.Postgres.Entities`, so the names do not collide with the Mongo entities in `Data/Entities`:

| Entity | Table | Navigations |
|---|---|---|
| `Organisation` | `organisation` | `OrganisationType?` (many-to-one, through `OrganisationTypeId`), `Addresses` (`List<OrganisationAddress>`) |
| `OrganisationType` | `organisation_type` | none |
| `AddressUsageType` | `address_usagetype` | none |
| `Address` | `address` | none |
| `OrganisationAddress` | `organisation_address` | `Organisation`, `Address`, `UsageType` |

- Entities are classes with settable properties, as EF Core expects for tracked entities; the Mongo records are not reused.
- One `IEntityTypeConfiguration<T>` per entity beside it, applied in `PostgresDbContext.OnModelCreating` with `ApplyConfigurationsFromAssembly` limited to that namespace (or listed explicitly). Each sets `ToTable`, `HasKey`, `HasColumnName` for every property, the relationships with `OnDelete(DeleteBehavior.Restrict)`, and `ValueGeneratedNever()` on the keys.
- Column names are explicit. No naming-convention package is added, so `.github/dependabot.yml` does not change.
- `PostgresDbContext` gains a `DbSet` per entity, and its "empty on purpose" comment is updated.
- The EF model must match the changelog exactly; EF never creates or migrates the schema.
- `TimestampInterceptor` (`src/Api/Data/Postgres/`), a `SaveChangesInterceptor` registered in `AddPostgres`, sets timestamps from `TimeProvider` (milliseconds, as on the Mongo side, so a value written equals the value read back). `created_at` and `updated_at` record when this service stored and last changed a row, so they are internal to it: on insert both become now whatever the entity holds; on update `updated_at` becomes now and `created_at` is left out of the `UPDATE`, so the stored value stands (PR review). `valid_from` is a business date a caller may set, for example a change that took effect earlier, so on insert it becomes now only when unset. An original date from another system, such as RPD's creation date in a migration, is a different fact and would get its own column. Entities opt in through `ITimestamped` (`Organisation`, `Address`, `OrganisationAddress`) and `IValidFrom` (`Organisation`, `OrganisationAddress`).
- Nothing in the API writes to these tables in this story. The create endpoint is REGEN-141's own work.

### Tests

In `Api.IntegrationTests`, against the Compose Postgres (`docker compose up --build -d`), extending the `DatabaseChangelogTests` pattern of resolving `PostgresDbContext` from `AddPostgres`:

- The `v1.1.0` tag is in `databasechangelog`.
- `address_usagetype` holds exactly the five seeded values.
- An organisation with an organisation type (by code) and two addresses (`Registered`, `Principal`) saved through `PostgresDbContext` reads back with every column and navigation equal, including `timestamptz` values as UTC.
- A second current `Registered` address for the same organisation is rejected by `ux_organisation_address_current`; after `valid_to` is set on the first, the second is accepted.
- An `organisation_address` with an unknown `usage_type_id` or `organisation_id` is rejected by its foreign key.
- Deleting an organisation that has addresses is rejected (`RESTRICT`).
- `valid_to` earlier than `valid_from` on `organisation_address` is rejected by the check constraint.
- `home_nation` outside the four UK nations, and a `country_iso` that is not two upper-case letters, are rejected by their check constraints.
- `is_uk_address` reads back `true` for `GB`, `false` for another country and `NULL` with no country.
- An organisation saved with `created_at` and `updated_at` already set stores the clock's time instead, gets `valid_from` from the clock when unset, and an update that also changes `created_at` moves only `updated_at`.
- `organisation_type` holds exactly the 40 seeded codes, equal to `OrganisationTypeCodes.All`.
- An `organisation` with an unknown `organisation_type_id` is rejected by its foreign key.

In `Api.Tests`, `OrganisationTypeCodesTests` checks that the list has 40 entries, that every code is unique and in the Companies House kebab-case format, and that every description is unique, so no two codes share a meaning.

`PostgresDbContext_ShouldConnectAndHaveNoEntities` changes to assert the five entity types instead of none. It is an integration test, not a v1 test. Tests create their own organisations with fresh ids and do not depend on each other's rows; the Mongo clean-up in the integration tests does not touch Postgres.

`Api.Tests` and `Api.V1.Tests` need no change and must still pass.

### Work breakdown

1. `db.changelog-1.1.xml` and its include; check with `docker compose up liquibase` and `psql`.
2. EF entities, configurations and `DbSet`s.
3. Integration tests.
4. README: a short note on the organisation tables and where their entities live.

## Acceptance Criteria

- [ ] `docker compose up --build -d` applies `db.changelog-1.1.xml` with no manual step, `databasechangelog` holds `v1.1.0`, and the API starts healthy.
- [ ] The five tables exist with the columns, types, nullability, defaults, foreign keys (`ON DELETE RESTRICT`), indexes and check constraints in this spec, in snake_case.
- [ ] `address_usagetype` holds `Registered`, `Audit`, `ServiceOfNotice`, `Principal` and `Correspondence`, and has no foreign key to `organisation_address`.
- [ ] Only one current (`valid_to IS NULL`) `organisation_address` per organisation and usage type can exist; closed rows are unlimited.
- [ ] `PostgresDbContext` maps the five tables, and an organisation with its type and addresses round-trips through it in the integration tests.
- [ ] `db.changelog-1.0.xml` is unchanged, and no EF Core migration exists.
- [ ] `Api.Tests`, `Api.IntegrationTests` and `Api.V1.Tests` pass locally and in `check-pull-request.yml`; no v1 test or snapshot is edited.
- [ ] `dotnet csharpier check .` (including `changelog/*.xml`) and a warnings-as-errors build pass.
- [ ] The Open Questions below are copied to REGEN-141 for refinement.

## Out of Scope

- The `POST /organisations` endpoint, its validation, versioning and responses (REGEN-141 itself).
- Duplicate detection and any unique constraint for it.
- `subsidiary`, `organisation_lifecycle` and the scheme tables (`scheme_operator`, `scheme_member`, `compliance_scheme`, `compliance_scheme_lifecycle`).
- External ids from RPD (`externalOrganisationId`, `externalSubsidiaryId`, the six-digit public organisation id): they are in the Confluence payloads but not on the board.
- SIC codes (`mainActivitySicCode`): not an MVP field.
- Translating RPD and RE/EX addresses to the Defra address model.
- A `name` on `organisation_type`, `is_a_partnership`, and an organisation sub-type.
- Migrating organisation data from MongoDB or from RPD.
- Triggers for `updated_at`, audit or change-delta tables.
- A read-only data source (`Postgres:ReadOnlyHost`).

## Decisions

| Decision | Why |
|---|---|
| `organisation_type` is a seeded reference list of codes (`id`, `code`), pointed at by `organisation.organisation_type_id`; no `name`, `is_a_partnership` or sub-type | Team decisions, 20 October 2026. The code makes Welsh, and labels generally, the UI's concern; the foreign key keeps every stored type valid. A `name` is cheap to add later |
| Seed all 36 Companies House `company_type` codes plus four of our own, with no common subset | Anything a Companies House lookup or an RPD migration returns can be stored as it is; one flat list keeps it simple |
| The codes and their meanings are published in the API contract, from a C# list the seed is tested against | Consumers build their labels from one source rather than guessing; the test stops the published list and the table drifting apart |
| `address_usagetype` is a reference list with no foreign key back to `organisation_address` | The board's two-way foreign keys are circular: neither row could be inserted first |
| Seed five usage types: `Registered`, `Audit`, `ServiceOfNotice`, `Principal`, `Correspondence` | The five kinds of address on the API page. `Site` is specific to RE/EX and `Organisation` is on neither the API page nor the payloads. `Audit` was first dropped, then kept because RPD holds audit addresses (team decisions, 20 October 2026; 21 October 2026) |
| `address` follows the Defra standard address model; `country` is `country_iso` (ISO 3166-1 alpha-2) plus `country_name`; no `phone_number` | Aligns with the address model Defra expects going forward rather than RPD's current one (team decisions, 20 October 2026). A phone number is not part of an address |
| `is_uk_address` is generated from `country_iso` | Keeps the Defra field in the table while making it impossible to contradict the country; storing a caller's value would allow `true` with `FR` |
| `home_nation` (ISO 3166-2:GB code), `NOT NULL`, limited by a check constraint to the four UK nations | `homeNation` on the API page replaces the board's `HomeNationCode` and `NationCode`; a fixed set of four is cheap for the database to enforce (team decisions, 20 October 2026) |
| No `main_activity_sic` | Not an MVP field (team decisions, 20 October 2026) |
| Keep history with `valid_from`/`valid_to`; one current address per organisation and usage type; addresses not shared | Matches the board's columns and the "records change deltas and timestamps" success metric, without the update anomalies of shared address rows |
| A table's own attributes are not prefixed with the table's name: `organisation.name`, `address_usagetype.name`, `organisation_type.code` (properties `Name`, `Code`) | The same rule as `id`: the table already says whose attribute it is. It matches `name` in the Confluence payloads (PR review) |
| No `organisation_size` or `turnover` on `organisation`, unlike the board | Producer size (large or small) is RPD's packaging classification, decided per registration year from thresholds such as turnover; the v1 API already holds it as a registration type, and registration data is moving to an RPD-owned Registrations service. This service stays agnostic of any one service's rules. Turnover means nothing without a financial year, and no Confluence payload has either field. (PR review) |
| Each table's own key is `id` / `Id`; references are `<thing>_id` / `<Thing>Id` | One convention across the Mongo entity, the DTOs and the documented API, where every object's identifier is `Id` (PR review) |
| No natural-key index on `address` | The board's `AddressLine1` + `Postcode` does not exist in the Defra model, and addresses are not shared; duplicate detection is a later story |
| No `valid_from`/`valid_to` on `address` | Not shared, so `organisation_address` already holds the period; one place to close history (PR review) |
| Timestamps set by `TimestampInterceptor` from `TimeProvider`, no database defaults | EF always writes every column, so a default never fires; the interceptor also keeps `updated_at` current on update, which a default cannot. Without a default, an `INSERT` outside EF that omits a timestamp fails `NOT NULL` instead of quietly taking the database clock (PR review) |
| snake_case names | Postgres convention; no quoting in SQL, `psql` or Liquibase |
| UUIDv7 set by the application, no column default | Time-ordered keys index well, and the entity has its id before it is saved |
| `text` and `timestamptz` | Payload field lengths are not known yet; `timestamptz` stores an instant, so UTC handling is unambiguous |
| `name` and `home_nation` are `NOT NULL` on `organisation` | Mandatory per the Jira questions; everything else waits for the payload to settle |
| `ON DELETE RESTRICT` | History is closed, not deleted; a cascade could silently remove it |
| EF entities in `Data/Postgres/Entities` | No name collision with the Mongo entities, which stay untouched |
| Integration tests, no rollback test | Covers what the endpoint stories depend on; rollback is out of scope |

## Open Questions

To raise on REGEN-141 and with Michael Asante:

1. ~~**Organisation type, twice.**~~ Answered 20 October 2026: `organisation_type` is a reference list of codes, and `organisation.organisation_type_id` is the only link (`organisation_type`, Decisions).
2. ~~**One type per organisation?**~~ Answered 20 October 2026: yes, as a single foreign key on `organisation`.
3. ~~**Usage type values.**~~ Answered 20 and 21 October 2026: `Correspondence` added, `Site` and `Organisation` dropped, `Audit` kept (`address_usagetype`, Decisions).
4. ~~**`nation_code` and `home_nation_code`.**~~ Answered 20 October 2026: both are replaced by `home_nation`, matching `homeNation` on the API page.
5. **Mandatory fields.** Are `name` and `home_nation` on `organisation` (plus at least one address, enforced by the endpoint) the right set? Which address fields does the create endpoint require?
6. **Duplicate detection.** Due at backlog refinement on 16 October 2026 (stand-up: a duplicate returns the matching organisation with 200). The answer may add a unique constraint in a later changeset.
7. **`valid_from`/`valid_to` on `organisation`.** What does closing an organisation mean, given `organisation_lifecycle` on the board?
8. **External ids.** Where do `externalOrganisationId` and `externalSubsidiaryId` live? They are in every Confluence payload but on no table in scope.

## Dependencies

- REGEN-146 (merged): Postgres, `PostgresDbContext`, Liquibase in Compose and the publish of `changelog/`.
- Answers to the Open Questions may add changesets later; none blocks this story.

## Risks

| Risk | Mitigation |
|---|---|
| The Open Questions change the model after the endpoint is built on it | Contested columns are nullable and no uniqueness is enforced on them, so a later changeset can add or drop them without moving data |
| The EF model and the changelog drift apart | The round-trip test reads and writes every column; a missing or misnamed column fails it |
| A published changeset turns out wrong | Never edit it; add a `db.changelog-1.2.xml` that corrects it |
| The board's domain model is only available as a screenshot | The model is transcribed in this spec (The domain model) so review does not depend on Miro access |

## Notes

- Mongo `Address.Town` is `address.locality` here; the mapping between the v1 and v2 shapes belongs to the endpoint stories.
- Postgres does not index foreign keys automatically; each FK column gets an index named `ix_<table>_<column>`.

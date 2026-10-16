# REGEN-141: Organisation and Address Postgres Schema

| | |
|---|---|
| Type | Task: schema and data access. No change to what the API does for its current clients |
| Priority | P1 (High): a pre-task of REGEN-141 (Create an Organisation), which is in progress and cannot persist anything without these tables |
| Parent | REGEN-141 Create an Organisation, under REGEN-125 Access to org data via Org API |
| Risk | Medium (see Risks) |
| Source | Jira story REGEN-141 and its comments (stand-up notes of 16 October 2026, Ogonna Iwunze's questions), the "Data payloads" table on the Confluence page "Access to org data via Org API", the domain model on the Miro board (`uXjVHUfA4ZE=`, as a screenshot on 16 October 2026), and the refinement answers of 16 October 2026 |

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

Add a `db.changelog-1.1.xml` that creates the five tables and seeds the address usage types, and map them with EF Core entities in `PostgresDbContext`. Follow the board, with the changes and conventions in Decisions: snake_case names, `text` and `timestamptz` types, UUIDv7 keys set by the application, no circular foreign key between `organisation_address` and `address_usagetype`, `ON DELETE RESTRICT`, and address history kept through `valid_from`/`valid_to`.

### Conventions (all tables)

- Table and column names are snake_case and unquoted: `OrganisationId` becomes `organisation_id`, `AddressLine1` becomes `address_line1`.
- Primary keys are `uuid` with no default. The API sets them with `Guid.CreateVersion7()`, so keys are time-ordered and index well.
- The board's VARCHAR columns are `text`. No lengths until the payloads are final: a length is a constraint, and the Confluence payloads are expected to evolve.
- TIMESTAMP columns are `timestamptz`. DATE columns (none in scope) would stay `date`.
- `created_at` and `updated_at` are `timestamptz NOT NULL DEFAULT now()`. The application sets `updated_at` when it changes a row; there is no trigger.
- Where a table has `valid_from` and `valid_to`: `valid_from` is `timestamptz NOT NULL DEFAULT now()`, `valid_to` is nullable, and `NULL` means "current". A check constraint enforces `valid_to IS NULL OR valid_to > valid_from`.
- Every foreign key is `ON DELETE RESTRICT`. History is closed with `valid_to`, never removed by a cascade.
- Every foreign key column has an index; Postgres does not create one for a foreign key.
- The tables live in the `public` schema, like `databasechangelog`.

### Tables

#### `organisation`

| Column | Type | Null | Notes |
|---|---|---|---|
| `organisation_id` | `uuid` | NOT NULL | PK |
| `organisation_name` | `text` | NOT NULL | |
| `trading_name` | `text` | NULL | |
| `companies_house_number` | `text` | NULL | Not every organisation type is on Companies House (stand-up, 16 October 2026) |
| `home_nation_code` | `text` | NOT NULL | |
| `nation_code` | `text` | NULL | See Open Questions |
| `main_activity_sic` | `text` | NULL | |
| `organisation_type_code` | `text` | NULL | Overlaps `organisation_type`: see Open Questions |
| `organisation_sub_type_code` | `text` | NULL | |
| `organisation_type` | `text` | NULL | |
| `organisation_size` | `integer` | NULL | |
| `turnover` | `bigint` | NULL | |
| `valid_from` | `timestamptz` | NOT NULL | Default `now()` |
| `valid_to` | `timestamptz` | NULL | |
| `created_at` | `timestamptz` | NOT NULL | Default `now()` |
| `updated_at` | `timestamptz` | NOT NULL | Default `now()` |

`organisation_name` and `home_nation_code` are the only business columns that are `NOT NULL`. The Jira questions also list "address" as mandatory; a row cannot require a child row, so the create endpoint enforces that.

No unique constraint on name, Companies House number or any combination: how duplicates are detected is still being decided (Open Questions).

#### `organisation_type`

As drawn on the board: a child of `organisation`, not a reference list.

| Column | Type | Null | Notes |
|---|---|---|---|
| `organisation_type_id` | `uuid` | NOT NULL | PK |
| `organisation_id` | `uuid` | NOT NULL | FK to `organisation`. Unique: the board says an organisation "has an" organisation type |
| `is_a_partnership` | `boolean` | NOT NULL | Default `false` |
| `organisation_type` | `text` | NULL | The legal form, for example "Partnership" or "Limited liability" |

`organisationType` is the type in the legal sense, for example what Companies House returns (stand-up, 16 October 2026). Why both this table and the type columns on `organisation` exist is an open question for Michael Asante (Open Questions); this story builds both and leaves both nullable so either can be dropped later without a data migration.

#### `address_usagetype`

A seeded reference list. Unlike the board, it has no `organisation_address_id`: the board draws foreign keys in both directions between the two tables, which would make each row depend on the other. Only `organisation_address` points at it.

| Column | Type | Null | Notes |
|---|---|---|---|
| `usage_type_id` | `uuid` | NOT NULL | PK |
| `usage_type` | `text` | NOT NULL | Unique |

Seeded in the changelog with fixed UUID literals, one row each:

| `usage_type` |
|---|
| `Registered` |
| `Audit` |
| `ServiceOfNotice` |
| `Principal` |
| `Organisation` |
| `Site` |

These are the board's values. The Confluence `POST /organisations` payload also has `correspondenceAddress`, which has no usage type here (Open Questions). A new value is a new changeset that inserts a row, not a schema change.

The code looks a usage type up by `usage_type`, not by a hard-coded id.

#### `address`

| Column | Type | Null | Notes |
|---|---|---|---|
| `address_id` | `uuid` | NOT NULL | PK |
| `address_line1` | `text` | NULL | |
| `address_line2` | `text` | NULL | |
| `city` | `text` | NULL | The Mongo `Address` calls this `Town` |
| `county` | `text` | NULL | |
| `postcode` | `text` | NULL | |
| `country` | `text` | NULL | |
| `state_province_or_region` | `text` | NULL | |
| `phone_number` | `text` | NULL | |
| `valid_from` | `timestamptz` | NOT NULL | Default `now()` |
| `valid_to` | `timestamptz` | NULL | |
| `created_at` | `timestamptz` | NOT NULL | Default `now()` |
| `updated_at` | `timestamptz` | NOT NULL | Default `now()` |

All address lines are nullable, as in the Mongo `Address` today.

Addresses are not shared between organisations or usage types: each `organisation_address` row has its own `address` row. The board's natural key (`address_line1` + `postcode`) is a non-unique index, `ix_address_address_line1_postcode`, so a later dedupe or lookup can use it; it is not a constraint (Decisions).

#### `organisation_address`

| Column | Type | Null | Notes |
|---|---|---|---|
| `organisation_address_id` | `uuid` | NOT NULL | PK |
| `organisation_id` | `uuid` | NOT NULL | FK to `organisation` |
| `address_id` | `uuid` | NOT NULL | FK to `address` |
| `usage_type_id` | `uuid` | NOT NULL | FK to `address_usagetype` |
| `valid_from` | `timestamptz` | NOT NULL | Default `now()` |
| `valid_to` | `timestamptz` | NULL | |
| `created_at` | `timestamptz` | NOT NULL | Default `now()` |
| `updated_at` | `timestamptz` | NOT NULL | Default `now()` |

History is kept: changing an organisation's registered address sets `valid_to` on the current `organisation_address` row and inserts a new one with a new `address`. A partial unique index, `ux_organisation_address_current`, on `(organisation_id, usage_type_id) WHERE valid_to IS NULL` allows one current address per organisation per usage type and any number of closed ones.

One address may carry several usage types for the same organisation only as separate `address` rows (no sharing).

### Schema: Liquibase changelog

| Path | Content |
|---|---|
| `changelog/db.changelog-1.1.xml` | New. One changeset per table, in dependency order (`organisation`, `organisation_type`, `address_usagetype`, `address`, `organisation_address`), a changeset that inserts the usage types, and a last changeset that tags the database `v1.1.0` |
| `changelog/db.changelog.xml` | Includes `db.changelog-1.1.xml` after `db.changelog-1.0.xml` |

Changesets use Liquibase's XML change types (`createTable`, `addForeignKeyConstraint`, `createIndex`, `insert`, `tagDatabase`). The partial unique index and the check constraints have no XML change type that expresses them, so they are `sql` changes with an explicit `rollback`. `db.changelog-1.0.xml` is not edited (README: a published changeset is never changed).

`dotnet csharpier check .` covers `changelog/*.xml` locally; CI does not (AGENTS.md), so run it after editing.

### EF Core entities

In `src/Api/Data/Postgres/Entities/`, namespace `Defra.WasteOrganisations.Api.Data.Postgres.Entities`, so the names do not collide with the Mongo entities in `Data/Entities`:

| Entity | Table | Navigations |
|---|---|---|
| `Organisation` | `organisation` | `OrganisationType?` (one-to-one), `Addresses` (`List<OrganisationAddress>`) |
| `OrganisationType` | `organisation_type` | `Organisation` |
| `AddressUsageType` | `address_usagetype` | none |
| `Address` | `address` | none |
| `OrganisationAddress` | `organisation_address` | `Organisation`, `Address`, `UsageType` |

- Entities are classes with settable properties, as EF Core expects for tracked entities; the Mongo records are not reused.
- One `IEntityTypeConfiguration<T>` per entity beside it, applied in `PostgresDbContext.OnModelCreating` with `ApplyConfigurationsFromAssembly` limited to that namespace (or listed explicitly). Each sets `ToTable`, `HasKey`, `HasColumnName` for every property, the relationships with `OnDelete(DeleteBehavior.Restrict)`, and `ValueGeneratedNever()` on the keys.
- Column names are explicit. No naming-convention package is added, so `.github/dependabot.yml` does not change.
- `PostgresDbContext` gains a `DbSet` per entity, and its "empty on purpose" comment is updated.
- The EF model must match the changelog exactly; EF never creates or migrates the schema.
- Nothing in the API writes to these tables in this story. The create endpoint is REGEN-141's own work.

### Tests

In `Api.IntegrationTests`, against the Compose Postgres (`docker compose up --build -d`), extending the `DatabaseChangelogTests` pattern of resolving `PostgresDbContext` from `AddPostgres`:

- The `v1.1.0` tag is in `databasechangelog`.
- `address_usagetype` holds exactly the six seeded values.
- An organisation with an organisation type and two addresses (`Registered`, `Principal`) saved through `PostgresDbContext` reads back with every column and navigation equal, including `timestamptz` values as UTC.
- A second current `Registered` address for the same organisation is rejected by `ux_organisation_address_current`; after `valid_to` is set on the first, the second is accepted.
- An `organisation_address` with an unknown `usage_type_id` or `organisation_id` is rejected by its foreign key.
- Deleting an organisation that has addresses is rejected (`RESTRICT`).
- `valid_to` earlier than `valid_from` is rejected by the check constraint.
- A second `organisation_type` row for the same organisation is rejected.

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
- [ ] `address_usagetype` holds `Registered`, `Audit`, `ServiceOfNotice`, `Principal`, `Organisation` and `Site`, and has no foreign key to `organisation_address`.
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
- `sicCode` beyond the board's `main_activity_sic`, and any reference list of SIC codes, nations or organisation types.
- Migrating organisation data from MongoDB or from RPD.
- Triggers for `updated_at`, audit or change-delta tables.
- A read-only data source (`Postgres:ReadOnlyHost`).

## Decisions

| Decision | Why |
|---|---|
| Build `organisation_type` as drawn: a child of `organisation`, alongside the type columns on `organisation` | Stay close to Michael's design; the overlap is recorded as an open question rather than resolved here |
| `address_usagetype` is a reference list with no foreign key back to `organisation_address` | The board's two-way foreign keys are circular: neither row could be inserted first |
| Seed the board's six usage types only | The board is the agreed model; `Correspondence` waits for refinement |
| Keep history with `valid_from`/`valid_to`; one current address per organisation and usage type; addresses not shared | Matches the board's columns and the "records change deltas and timestamps" success metric, without the update anomalies of shared address rows |
| Natural key (`address_line1` + `postcode`) is an index, not a constraint | Not shared means two organisations at one address need two rows |
| snake_case names | Postgres convention; no quoting in SQL, `psql` or Liquibase |
| UUIDv7 set by the application, no column default | Time-ordered keys index well, and the entity has its id before it is saved |
| `text` and `timestamptz` | Payload field lengths are not known yet; `timestamptz` stores an instant, so UTC handling is unambiguous |
| `organisation_name` and `home_nation_code` are `NOT NULL` | Mandatory per the Jira questions; everything else waits for the payload to settle |
| `ON DELETE RESTRICT` | History is closed, not deleted; a cascade could silently remove it |
| EF entities in `Data/Postgres/Entities` | No name collision with the Mongo entities, which stay untouched |
| Integration tests, no rollback test | Covers what the endpoint stories depend on; rollback is out of scope |

## Open Questions

To raise on REGEN-141 and with Michael Asante:

1. **Organisation type, twice.** `organisation_type` (with `organisation_id`, `is_a_partnership`, `organisation_type`) and the `organisation_type_code`, `organisation_sub_type_code` and `organisation_type` columns on `organisation` overlap. Is `organisation_type` needed (the Jira thread already asks), should it be a reference list instead of a child table, and which column is the source of truth for the Confluence `organisationType` and `organisationSubType`?
2. **One type per organisation?** This spec makes `organisation_type.organisation_id` unique, reading "has an" on the board. Confirm.
3. **Usage type values.** The board has `Organisation` and `Site`; the Confluence `POST /organisations` payload has `correspondenceAddress`, which the board does not. Add `Correspondence`? What are `Organisation` and `Site` addresses?
4. **`nation_code` and `home_nation_code`.** What is the difference, and is `homeNation` in the payload `home_nation_code`?
5. **Mandatory fields.** Are `organisation_name` and `home_nation_code` (plus at least one address, enforced by the endpoint) the right set?
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

- Mongo `Address.Town` is `address.city` here; the mapping between the v1 and v2 shapes belongs to the endpoint stories.
- Postgres does not index foreign keys automatically; each FK column gets an index named `ix_<table>_<column>`.

# MAV-17: Clear Postgres test data between integration test runs

| | |
|---|---|
| Type | Task: `Api.IntegrationTests` empties the three Postgres tables that hold test data once, at the start of each run, after checking that it has reached the Compose database. The API, the schema, the seeded reference data, Mongo clearing, `Api.V1.Tests` and the CI workflows are unchanged |
| Priority | P3 (Low): only local re-runs benefit, because CI starts a fresh stack for every run, and the README step from spec 0008 slice 1 already works round the problem (author, 25 November 2026) |
| Parent | MAV-10 Access to org data via Org API, through MAV-17 |
| Risk | Low (see Risks) |
| Source | Spec 0008 and the preflight of its slice 1, DEFRA/waste-organisations pull request 282 (25 November 2026), which suggested this work as a story of its own; the review comment on `tests/Testing/CompaniesHouseNumbers.cs` in that pull request (25 November 2026), which proposes generated numbers in place of the constants; the author's answers of 25 November 2026 recorded in Decisions |

## Problem/Opportunity

The Postgres integration tests leave their rows behind. Before spec 0008 that was harmless: every test sent address values unique to it, and nothing in the schema stopped two organisations sharing data. Spec 0008 slice 1 added the unique index `ux_organisation_companies_house_number`, and the integration tests store organisations with fixed Companies House numbers from `CompaniesHouseNumbers`. A second run against the same stack is now rejected by the index on every one of those numbers, so the whole suite fails.

Generating the numbers instead, as a review comment on pull request 282 proposes, does not remove the problem:

- A test checked against a saved snapshot keeps a fixed number. `CreateOrganisationTests.FullPayload_ShouldCreateOrganisationAndRows` stores `12345678` and its snapshot holds it, so a second run fails on it every time.
- A generator can keep numbers distinct within one run, but not from the rows earlier runs left behind. As those rows build up, an occasional run draws a number already stored and fails on the index, intermittently and without an obvious cause.

This spec works with either approach: it does not depend on how the tests choose their numbers.

Spec 0008 slice 1 worked round this in the README: rebuild the stack (`docker compose down -v --remove-orphans`, then `docker compose up --build -d`) before each run. A rebuild takes minutes, and a developer who forgets it sees index violations that look like a real defect.

CI is not affected: `.github/workflows/check-pull-request.yml` starts a fresh stack for each run. The benefit is to developers re-running the integration tests locally.

What is in place today:

- `tests/Api.IntegrationTests/IntegrationTestBase.cs` puts every integration test class in one xUnit collection, `[Collection("Integration Tests")]`, so the tests run one at a time. Its `BuildServiceProvider` connects to the Compose Postgres at `127.0.0.1:5432`, database `waste_organisations`, as `postgres`.
- `tests/Api.IntegrationTests/MongoTestBase.cs` empties the Mongo `Organisation` collection before each test. Postgres has nothing equivalent.
- Test data lives in `organisation_address`, `organisation` and `address`. Liquibase seeds `organisation_type` and `address_usagetype` and records its history in `databasechangelog` and `databasechangeloglock`.
- No test counts a whole table. Each counts through its own organisation's links or its own address suffix (the header comment of `CreateOrganisationTests`).

## Proposed Solution

### The fixture

A new xUnit v3 assembly fixture, `PostgresTestDataFixture` in `tests/Api.IntegrationTests/PostgresTestDataFixture.cs`, registered with `[assembly: AssemblyFixture(typeof(PostgresTestDataFixture))]`. It implements `IAsyncLifetime`, and its `InitializeAsync` runs once before the first test of the run:

1. Build a service provider with the Postgres settings of `IntegrationTestBase.BuildServiceProvider`. That method becomes `internal static` so the fixture can call it; its settings are not copied.
2. Check the database (see "The guard"). If the check fails, throw `InvalidOperationException` with a message naming the database and host reached and saying why it was refused. Nothing is truncated.
3. Run `TRUNCATE organisation_address, organisation, address` through `PostgresDbContext.Database.ExecuteSqlRawAsync`. The statement is a constant with no input, so the stack rule's ban on SQL built from strings does not apply. There is no `CASCADE`: if a later changelog adds a table that references one of the three, the truncate fails and the run stops, rather than emptying a table nobody listed.

`DisposeAsync` does nothing. The data of the last run stays in place, so a developer can inspect it after a failure.

### The guard

The fixture truncates only when both hold:

- `current_database()` is `waste_organisations`.
- The table `databasechangelog` exists and holds the tag `v1.0.0`, which this repo's first changelog writes.

The check is a static method, `PostgresTestDataFixture.IsComposeDatabase(string databaseName, IReadOnlyCollection<string> tags)`, so its decision can be tested without a database. The fixture reads the two facts from Postgres and passes them in. A missing `databasechangelog` table gives an empty tag list.

### README and comments

- `README.md`, "Running integration tests with dependencies": the `docker compose down -v --remove-orphans` line and the paragraph about clearing the stack, added by spec 0008 slice 1, are replaced by one sentence. It says the integration tests empty `organisation_address`, `organisation` and `address` at the start of each run, and only on the Compose database. `docker compose up --build -d` stays, because the stack must be running. The same applies to "Running all tests".
- The header comment of `CreateOrganisationTests` no longer says that address rows are never cleaned up. Rows are cleared once per run, so tests within a run still share the table, and the rule that every test sends address values unique to it stays.

### Tests

| Project | Test |
|---|---|
| `Api.IntegrationTests` (`PostgresTestDataFixtureTests.cs`) | `IsComposeDatabase` is true for `waste_organisations` with `v1.0.0` among the tags; false for another database name; false when `v1.0.0` is missing; false for an empty tag list |
| `Api.IntegrationTests` (`PostgresTestDataFixtureTests.cs`) | After a test stores an organisation with an address, a second call to the fixture's clear step leaves `organisation_address`, `organisation` and `address` empty. The row counts of `organisation_type`, `address_usagetype` and `databasechangelog` are the same before and after |
| `Api.IntegrationTests` (all) | The suite passes twice in a row against the same Compose stack without a rebuild |
| `Api.V1.Tests` | Passes after the integration tests, with no test or snapshot changed |

The test of the clear step calls a method of the fixture that `InitializeAsync` also uses, `ClearAsync`, so the truncate is tested as it runs. The test class carries the `[Collection("Integration Tests")]` of `IntegrationTestBase`, so it runs in the same sequence as the rest.

### Work breakdown

One pull request: about 150 non-generated changed lines across the fixture, its tests, one access change in `IntegrationTestBase`, the README and one comment, well within the size guide and one concern.

| Slice | Work | Verified by |
|---|---|---|
| 1 | "The fixture", "The guard", and "README and comments": `PostgresTestDataFixture` and its assembly registration, `IntegrationTestBase.BuildServiceProvider` made `internal`, `PostgresTestDataFixtureTests`, the README test sections and the `CreateOrganisationTests` header comment. No change to `src/`, `changelog/`, `compose.yml`, the workflows or `Api.V1.Tests` | `Api.Tests`; `Api.IntegrationTests` against Compose, run twice without a rebuild; `Api.V1.Tests` after it. Criteria 1 to 7 |

## Acceptance Criteria

- [ ] 1. `Api.IntegrationTests` passes twice in a row against the same Compose stack, with no `docker compose down` or rebuild between the runs.
- [ ] 2. At the start of a run, before the first test, `organisation_address`, `organisation` and `address` are emptied once.
- [ ] 3. Clearing leaves the row counts of `organisation_type`, `address_usagetype` and `databasechangelog` unchanged.
- [ ] 4. On a database other than `waste_organisations`, or one whose `databasechangelog` is missing or lacks the tag `v1.0.0`, the run fails with a message naming the database and the reason, and no table is truncated.
- [ ] 5. Mongo is still emptied before each test as before, and every existing integration test passes unchanged in what it asserts.
- [ ] 6. `Api.V1.Tests` passes after the integration tests with no test or snapshot changed; no `*.verified.*` file changes.
- [ ] 7. No file under `src/`, `changelog/`, `.github/workflows/` or `compose.yml` changes, no NuGet package is added, and the full gate passes.

## Out of Scope

- Clearing Postgres before each test. Tests within a run keep sharing the tables, so each still uses a Companies House number of its own.
- How the tests choose their Companies House numbers: constants, as spec 0008 slice 1 has them, or generated, as the review comment on pull request 282 proposes. That is decided in spec 0008 and its pull requests.
- Clearing Postgres after a run. The last run's data stays for inspection.
- Clearing Postgres for `Api.V1.Tests`, which uses only Mongo.
- Any change to how Mongo is cleared.
- Changes to the CI workflows, which already start a fresh stack.
- Making the Postgres connection settings of the integration tests configurable.

## Decisions

| Topic | Decision |
|---|---|
| Story | Delivered under MAV-17 as a follow-up of spec 0008, in a spec of its own rather than a third slice of 0008 (author, 25 November 2026) |
| When to clear | Once per run, before the first test, through an xUnit v3 assembly fixture (author, 25 November 2026). It removes every collision with an earlier run, whether a test uses a fixed number or a generated one, with one truncate per run rather than one per test. The tests within a run still need distinct numbers, which is spec 0008's concern. Clearing before each test was rejected, because it would add a truncate to every test and remove the last test's data before anyone could inspect it |
| Number approach | This spec does not choose between the constants of spec 0008 slice 1 and the generated numbers proposed in review of pull request 282, and holds for either (author, 25 November 2026) |
| What to clear | `organisation_address`, `organisation` and `address`, named in one `TRUNCATE` without `CASCADE`. A table added later is never cleared by accident, and one that references these makes the truncate fail loudly |
| Guard | The fixture refuses to truncate unless the database is `waste_organisations` and `databasechangelog` holds `v1.0.0` (author, 25 November 2026). The host is not checked separately: it is fixed at `127.0.0.1` in `IntegrationTestBase`. Checking `v1.0.0` rather than the latest tag means the guard does not change with each new changelog |
| Settings | The fixture reuses `IntegrationTestBase.BuildServiceProvider`, made `internal static`, so the Postgres settings live in one place |
| README step from spec 0008 | Spec 0008 slice 1 added a rebuild before each run to the README. This spec replaces it. Spec 0008 itself is not edited |

## Open Questions

None.

## Dependencies

- Spec 0008, both slices merged. Slice 1 adds the README step this spec replaces. Slice 2 adds the concurrency integration test, which must pass on a second run too.

## Risks

| Risk | Level | Mitigation |
|---|---|---|
| A developer points the tests at a Postgres that is not the Compose one and loses data | Low | The guard (criterion 4), and the constraint that host port 5432 is free for Compose |
| A test starts depending on another test's rows, now that the table is clean at the start of each run | Low | The tests still share the tables within a run, and the header comment of `CreateOrganisationTests` keeps the rule that each test uses data unique to it |
| xUnit v3 runs the assembly fixture before every filtered or single-test run as well | Low | That is the intended behaviour: any run starts clean |

## Notes

- The `GenerateStubs` test, skipped without an explicit filter, is unaffected.

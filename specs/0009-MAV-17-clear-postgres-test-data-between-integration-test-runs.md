# MAV-17: Clear Postgres test data between integration test runs

| | |
|---|---|
| Type | Task: `Api.IntegrationTests` empties the three Postgres tables that hold test data once, at the start of each run, after checking that it has reached the Compose database. The API, the schema, the seeded reference data, Mongo clearing, `Api.V1.Tests` and the CI workflows are unchanged |
| Priority | P3 (Low): only local re-runs benefit, because CI starts a fresh stack for every run, and the README step from spec 0008 slice 1 already works round the problem (author, 25 November 2026) |
| Parent | MAV-10 Access to org data via Org API, through MAV-17 |
| Risk | Low (see Risks) |
| Source | Spec 0008; the review comment on `tests/Testing/CompaniesHouseNumbers.cs` in that pull request (25 November 2026), which led spec 0008 slice 1 to generate its test numbers in place of per-test constants; the author's answers of 25 November 2026 recorded in Decisions |

## Problem/Opportunity

The Postgres integration tests leave their rows behind. Before spec 0008 that was harmless: every test sent address values unique to it, and nothing in the schema stopped two organisations sharing data. Spec 0008 slice 1 added the unique index `ux_organisation_companies_house_number`, and the integration tests store organisations with Companies House numbers from `CompaniesHouseNumbers`. After review, that class generates the numbers: `CompaniesHouseNumbers.Generate` never issues the same number twice in a run. That does not make a second run against the same stack pass:

- A test checked against a saved snapshot keeps a fixed number. `CreateOrganisationTests.FullPayload_ShouldCreateOrganisationAndRows` stores `CompaniesHouseNumbers.FullPayload` (`12345678`), and its snapshot holds it, so a second run fails on it every time.
- The generator keeps numbers distinct within one run, but not from the rows earlier runs left behind. As those rows build up, an occasional run draws a number already stored and fails on the index, intermittently and without an obvious cause.

This spec does not depend on how the tests choose their numbers, so it would hold if they went back to constants.

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
2. Check the database with `EnsureComposeDatabase` (see "The guard"). If the check fails, it throws `InvalidOperationException` and the run stops there, so the truncate is never reached.
3. Call `ClearAsync`, which runs `TRUNCATE organisation_address, organisation, address` through `PostgresDbContext.Database.ExecuteSqlRawAsync`. The statement is a constant with no input, so the stack rule's ban on SQL built from strings does not apply. There is no `CASCADE`: if a later changelog adds a table that references one of the three, the truncate fails and the run stops, rather than emptying a table nobody listed.

`DisposeAsync` does nothing.

### The guard

The fixture truncates only when both hold:

- `current_database()` is `waste_organisations`.
- The table `databasechangelog` exists and holds the tag `v1.0.0`, which this repo's first changelog writes.

The check is a static method, `PostgresTestDataFixture.EnsureComposeDatabase(string databaseName, string host, IReadOnlyCollection<string> tags)`, so it can be tested without a database. It returns when both hold, and otherwise throws `InvalidOperationException` with a message that names the database and host reached and the reason it was refused: the database name, or the missing `v1.0.0` tag. The fixture reads the database name and the tags from Postgres, takes the host from its connection settings, and passes them in. A missing `databasechangelog` table gives an empty tag list.

The `v1.0.0` tag is the guard that matters. `IntegrationTestBase.BuildServiceProvider` fixes the database name at `waste_organisations`, so the name check cannot fail while that setting stands; it stays as a cheap backstop if the setting is ever changed. Any database built from this repo's changelog holds `v1.0.0`, so the guard does not tell the Compose Postgres from a developer's own Postgres on port 5432 that they have run the API against. That case is accepted (Decisions, "Guard").

### README and comments

- `README.md`, "Running integration tests with dependencies": the `docker compose down -v --remove-orphans` line and the paragraph about clearing the stack, added by spec 0008 slice 1, are replaced by one sentence. It says the integration tests empty `organisation_address`, `organisation` and `address` at the start of each run, and only on the Compose database. `docker compose up --build -d` stays, because the stack must be running. The sentence goes below the command block of that section, where the paragraph was. "Running all tests" has no such paragraph, so it only loses its `docker compose down -v --remove-orphans` line. The same line under "Stopping and clearing local resources" stays.
- `doc/constraints.md`, "Tests": the bullet that says Postgres tests create their own distinct data becomes: "The Postgres integration tests empty `organisation_address`, `organisation` and `address` once at the start of each run; within a run each test still creates its own distinct data." The Mongo sentence of that bullet stays, as a bullet of its own.
- The header comment of `CreateOrganisationTests` no longer says that address rows are never cleaned up. Rows are cleared once per run, so tests within a run still share the table, and the rule that every test sends address values unique to it stays. Three other comments repeat the same statement and change with it: the header comments of `GetOrganisationDetailsTests` and `PostgresOrganisationServiceTests` (the integration one), and the comment on `CreateAsync` in `CreateOrganisationErrorTests` (Decisions, "Comments").

### Tests

| Project | Test |
|---|---|
| `Api.IntegrationTests` (`PostgresTestDataFixtureTests.cs`) | `EnsureComposeDatabase` returns for `waste_organisations` with `v1.0.0` among the tags. It throws `InvalidOperationException` for another database name, when `v1.0.0` is missing, and for an empty tag list, each time with a message that names the database, the host and the reason |
| `Api.IntegrationTests` (`PostgresTestDataFixtureTests.cs`) | The test assembly carries `[assembly: AssemblyFixture(typeof(PostgresTestDataFixture))]`, so the gate fails if the fixture stops being registered |
| `Api.IntegrationTests` (`PostgresTestDataFixtureTests.cs`) | After a test stores an organisation with an address, a second call to the fixture's clear step leaves `organisation_address`, `organisation` and `address` empty. The row counts of `organisation_type`, `address_usagetype` and `databasechangelog` are the same before and after |
| `Api.IntegrationTests` (all), by hand | The suite passes twice in a row against the same Compose stack without a rebuild. The gate and CI run it once on a fresh stack, so this check is manual: the slice's "How to test" carries it as a step |
| `Api.V1.Tests` | Passes after the integration tests, with no test or snapshot changed |

The test of the clear step calls a method of the fixture that `InitializeAsync` also uses, `ClearAsync`, so the truncate is tested as it runs. Because it clears the tables partway through a run, the rows left after a run are only those of the tests that ran after it. The test class carries the `[Collection("Integration Tests")]` of `IntegrationTestBase`, so it runs in the same sequence as the rest.

### Work breakdown

One pull request: about 170 non-generated changed lines across the fixture, its tests, one access change in `IntegrationTestBase`, the README, `doc/constraints.md` and four comments, well within the size guide and one concern.

| Slice | Work | Verified by |
|---|---|---|
| 1 | "The fixture", "The guard", and "README and comments": `PostgresTestDataFixture` and its assembly registration, `IntegrationTestBase.BuildServiceProvider` made `internal`, `PostgresTestDataFixtureTests`, the README test sections, the "Tests" bullet of `doc/constraints.md`, the `CreateOrganisationTests` header comment and the three comments that repeat it. No change to `src/`, `changelog/`, `compose.yml`, the workflows or `Api.V1.Tests`. The pull request's "How to test" includes running `Api.IntegrationTests` twice without a rebuild | `Api.Tests`; `Api.IntegrationTests` against Compose, and run a second time by hand without a rebuild; `Api.V1.Tests` after it. Criteria 1 to 7 |

## Acceptance Criteria

- [ ] 1. `Api.IntegrationTests` passes twice in a row against the same Compose stack, with no `docker compose down` or rebuild between the runs (checked by hand, as a step of the slice's "How to test").
- [ ] 2. At the start of a run, before the first test, `organisation_address`, `organisation` and `address` are emptied once.
- [ ] 3. Clearing leaves the row counts of `organisation_type`, `address_usagetype` and `databasechangelog` unchanged.
- [ ] 4. On a database other than `waste_organisations`, or one whose `databasechangelog` is missing or lacks the tag `v1.0.0`, the run fails with a message naming the database and the reason, and no table is truncated.
- [ ] 5. Mongo is still emptied before each test as before, and every existing integration test passes unchanged in what it asserts.
- [ ] 6. `Api.V1.Tests` passes after the integration tests with no test or snapshot changed; no `*.verified.*` file changes.
- [ ] 7. No file under `src/`, `changelog/`, `.github/workflows/` or `compose.yml` changes, no NuGet package is added, and the full gate passes.

## Out of Scope

- Clearing Postgres before each test. Tests within a run keep sharing the tables, so each still uses a Companies House number of its own.
- How the tests choose their Companies House numbers. Spec 0008 decides that (Decisions, "Test data"): generated numbers, with `FullPayload` the only constant.
- Clearing Postgres after a run. The next run clears it at its start, so clearing after a run would only add a second truncate.
- Clearing Postgres for `Api.V1.Tests`, which uses only Mongo.
- Any change to how Mongo is cleared.
- Changes to the CI workflows, which already start a fresh stack.
- Making the Postgres connection settings of the integration tests configurable.

## Decisions

| Topic | Decision |
|---|---|
| Story | Delivered under MAV-17 as a follow-up of spec 0008, in a spec of its own rather than a third slice of 0008 (author, 25 November 2026). Clearing test data is a change to the test harness, not to the duplicate-organisation behaviour spec 0008 delivers, so it is specified and reviewed on its own |
| When to clear | Once per run, before the first test, through an xUnit v3 assembly fixture (author, 25 November 2026). It removes every collision with an earlier run, whether a test uses a fixed number or a generated one, with one truncate per run rather than one per test. The tests within a run still need distinct numbers, which is spec 0008's concern. Clearing before each test was rejected, because it would add a truncate to every test for no gain: collisions within a run are already prevented by distinct numbers |
| Number approach | Spec 0008 slice 1 generates its test numbers, a change made in review of pull request 282 (author, 25 November 2026). This spec does not depend on that choice and would hold for constants too |
| What to clear | `organisation_address`, `organisation` and `address`, named in one `TRUNCATE` without `CASCADE`. A table added later is never cleared by accident, and one that references these makes the truncate fail loudly |
| Guard | The fixture refuses to truncate unless the database is `waste_organisations` and `databasechangelog` holds `v1.0.0` (author, 25 November 2026). The host is not checked separately: it is fixed at `127.0.0.1` in `IntegrationTestBase`. The database name is fixed there too, so in practice the `v1.0.0` tag is the guard; the name check is kept as a backstop. The guard therefore accepts any database built from this changelog on `127.0.0.1:5432`, including a developer's own Postgres that they have run the API against, and the team accepts that: the constraint that port 5432 is free for Compose is what keeps such a database out of reach. Telling the Compose database apart would need a marker only Compose creates, which means changing `compose.yml`, outside this spec. Checking `v1.0.0` rather than the latest tag means the guard does not change with each new changelog. Raised in review of pull request 283 (25 November 2026) |
| Settings | The fixture reuses `IntegrationTestBase.BuildServiceProvider`, made `internal static`, so the Postgres settings live in one place |
| Comments | Besides the header comment of `CreateOrganisationTests`, three comments in `Api.IntegrationTests` say that address rows are never cleaned up: the headers of `GetOrganisationDetailsTests` and `PostgresOrganisationServiceTests`, and the comment on `CreateAsync` in `CreateOrganisationErrorTests`. This slice corrects all four, because the fixture makes each of them false and a comment is kept true as the code changes (the toolkit's `rules/shared/common-coding.md`, "Comments"). Only the wording changes; no test changes. Assumed while planning the slice (26 November 2026), as the smaller risk against leaving three comments that contradict the fixture |
| Fixture shape | Left to the implementation plan, within "The fixture" and "The guard": `ClearAsync` is a static method that takes the `PostgresDbContext` and a cancellation token, so the test and `InitializeAsync` call the same code; the host passed to the guard is `PostgresOptions.DefaultHost` of the provider that `BuildServiceProvider` returns; a missing `databasechangelog` is found with `to_regclass`, a constant query, before the tags are read (assumed while planning the slice, 26 November 2026) |
| README step from spec 0008 | Spec 0008 slice 1 added a rebuild before each run to the README. This spec replaces it. Spec 0008 itself is not edited |

## Open Questions

None.

## Dependencies

- Spec 0008, both slices merged. Slice 1 adds the README step this spec replaces. Slice 2 adds the concurrency integration test, which must pass on a second run too.

## Risks

| Risk | Level | Mitigation |
|---|---|---|
| A developer runs the tests with their own Postgres on port 5432, built from this changelog, and loses its organisation and address rows | Low | The guard does not stop this case, because that database has the `v1.0.0` tag (Decisions, "Guard"). The constraint that host port 5432 is free for Compose keeps it out of reach; the guard (criterion 4) stops any other database |
| A test starts depending on another test's rows, now that the table is clean at the start of each run | Low | The tests still share the tables within a run, and the header comment of `CreateOrganisationTests` keeps the rule that each test uses data unique to it |
| xUnit v3 runs the assembly fixture before every filtered or single-test run as well | Low | That is the intended behaviour: any run starts clean |

## Notes

- The `GenerateStubs` test, skipped without an explicit filter, is unaffected.

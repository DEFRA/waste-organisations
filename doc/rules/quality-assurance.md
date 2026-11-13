# Quality assurance and test standards

Adapted for this repo from the DEFRA [quality assurance and test standards](https://github.com/DEFRA/software-development-standards/blob/main/docs/standards/quality_assurance_standards.md) and the testing guidance in the DEFRA [GitHub Copilot guide](https://github.com/DEFRA/software-development-standards/blob/main/docs/guides/github_copilot.md). Last synced 9 November 2026.

This repo is a backend API with no user interface. The DEFRA criteria about screens, the GOV.UK Design System, browsers and WCAG apply to the frontends that call it, not here. Everything else applies.

## Acceptance criteria

- Every story has positive **and** negative acceptance criteria written in its spec (`specs/NNNN-<ticket-id>-slug.md`) before development starts. The spec names the scenarios; the tests pin them.
- Default criteria for an API story: the contract matches the spec (status codes, problem details, response shape, OpenAPI document); server-side validation exists for every field; unauthorised and forbidden callers get `401`/`403`; no obvious performance issue (most requests well under a second); no existing behaviour has regressed, which the v1 tests prove.
- **Negative scenarios are as important as positive ones.** "A write client can create an organisation" implies "a read-only client cannot", "a missing required member is a `400` naming the JSON path" and "a version the endpoint does not support is a `406`". Test all of them.

## The test projects

| Project | What it pins | How it runs |
| --- | --- | --- |
| `tests/Api.Tests` | Units and in-process endpoint behaviour through `WebApplicationFactory`; the only coverage Sonar counts | `dotnet test` on the runner, no dependencies |
| `tests/Api.IntegrationTests` | Behaviour against real Postgres and Mongo, `[Trait("Category", "IntegrationTests")]` | After `docker compose up --build -d` |
| `tests/Api.V1.Tests` | The frozen v1 contract, black-box over HTTP, one `[Trait("Scenario", "ID")]` per spec 0001 scenario | After the integration tests, never alongside them |
| `tests/Testing` | Shared fixtures, endpoint helpers and JWT generation for the first two | Referenced, not run |

- Follow the testing pyramid: prove logic in `Api.Tests`, prove persistence and wiring in `Api.IntegrationTests`, prove the public contract in `Api.V1.Tests`. Do not reach for an integration test when a unit test proves the same thing.
- `Api.V1.Tests` has no project reference to `src/Api` or `tests/Testing` and must not gain one: production types would let a v2 change alter what the tests send and expect.
- Run order and commands are in "Local Build And Test Checks" and "V1 API Tests" in `AGENTS.md`.

## Writing tests

- Framework and libraries: xUnit v3, AwesomeAssertions, NSubstitute for doubles, AutoFixture for data, Verify for snapshots, `Microsoft.Extensions.TimeProvider.Testing` for time. Do not introduce another test framework, assertion or mocking library.
- Name tests by behaviour in the existing style: `Method_Scenario_ShouldExpectation` in unit and integration tests (`ToEntity_WithBlankAndAbsentRegion_ShouldGiveOneAddress`), `Action_WhenCondition_ReturnsOutcome` in v1 tests. A test name reads as a statement about the service.
- Arrange, act, assert, with the arrangement built from `tests/Testing/Fixtures` where a fixture exists.
- Assert on observable behaviour (status code, body, stored rows, problem details), not on implementation detail or on which internal method was called.
- Test data is synthetic. Never copy production data or real company, address or personal details into fixtures, snapshots or test names.
- Each test is independent and runs in any order. The Mongo integration tests clear the `Organisation` collection before each test (`MongoTestBase`); Postgres tests create their own distinct data. Never depend on another test's data.
- No skipped tests (`Skip =`) committed without a Jira reference in the reason.

## Snapshots (Verify)

- Snapshots live in `Snapshots/<TestClass>/` beside the test class and are committed as `*.verified.*`; `*.received.*` files are gitignored.
- When a snapshot changes, read the diff and decide whether the behaviour change is intended by the spec before accepting the received file. Never accept a snapshot to make a build green.
- v1 snapshots are frozen. A v1 difference means the API changed: stop and ask (see "V1 API Tests" in `AGENTS.md`).

## Coverage

- Unit coverage reported to SonarQube Cloud must stay at or above 90% on new code and must never decrease overall (currently about 98%).
- `[ExcludeFromCodeCoverage]` only on bootstrap wiring (extension methods that register services or middleware), never on logic.
- Do not chase a number: a test written only to raise coverage, a near-duplicate scenario, or a test that restates the framework is a liability. Each test should prove a behaviour someone cares about.
- Never weaken, delete or skip an assertion or test to make a build pass. A red test is reporting something; understand it first.

## Wider QA practice

- All stories undergo risk-based exploratory testing against the Docker Compose stack or a CDP environment.
- Performance testing (default tool: JMeter) covers load at 120% of the likely maximum, soak, and stress, for releases that change throughput characteristics.
- Keep the team aware of testing risks; produce a test completion report before a major release.

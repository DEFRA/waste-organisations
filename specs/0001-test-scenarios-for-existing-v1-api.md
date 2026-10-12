# REGEN-145: Add a protected test project for the existing (v1) Waste Organisations API

| | |
|---|---|
| Type | Task: a new test project and repository configuration. No change to what the API does |
| Priority | P1 (High): it blocks REGEN-140 |
| Risk | Low |
| Source | Jira story REGEN-145, and the 3-amigos summary and discussion notes added to it on 12 October 2026 |

## Problem

Manage Account will change this API for v2. Current clients must not be broken by that work.

Normally tests evolve with the code. That does not fit here, because the team inherited this code and is not really evolving it. In effect two services will run inside one, and the v1 one has to keep behaving as it does now.

The existing unit and integration tests cannot guarantee that. They use the API's internal types, they are expected to change as the code changes, and nothing makes them hard to change.

We need a set of v1 tests that is set in stone: either not changed, or changed only by a conscious, approved decision.

## Who benefits

- **Current clients of the API:** their behaviour is protected while v2 is built.
- **Engineers working on v2:** a failing v1 test tells them early, on their own machine, that they have changed something clients rely on.

## Proposed solution

Add a separate, black-box test project for v1, make it difficult to change, and run it wherever the normal tests run.

This story delivers the project, its protection, its pipeline steps, and the scenarios the integration tests already cover. Further scenarios are follow-up work (see "Follow-up work").

### The project

| | |
|---|---|
| Path | `tests/Api.V1.Tests/` |
| Framework | xUnit v3 on the Microsoft Testing Platform, AwesomeAssertions and Verify, as the other test projects use |
| Target | `http://localhost:8080`, the address the Docker Compose stack exposes |

### Rules for the project

1. **No reference to production code.** No project reference to `src/Api` or `tests/Testing`. If the tests used the API's own types, a v2 change to those types would change what the tests send and expect without anyone editing a v1 test.
2. **Requests are raw JSON.** Response bodies are checked with Verify snapshots; status codes and headers with explicit assertions.
3. **HTTP only.** The tests do not connect to MongoDB. They set up data through the API.
4. **Each test owns its data.** A test creates organisations with new ids and asserts only on those. It never relies on an empty database or on another test.

### Scenarios in this story

These are the scenarios in `tests/Api.IntegrationTests/Scenarios`, copied and rewritten to follow the rules above.

| ID | Scenario | Expected today | Copied from |
|---|---|---|---|
| V1-01 | Create an organisation, PUT it again with a second registration (`LARGE_PRODUCER`), then get it | 201, then 200; get returns both registrations in the order they were added | `OrganisationTests.OrganisationCreatedAndUpdated` |
| V1-02 | Create several organisations with different registrations; search with no filter, then with `registrations=LARGE_PRODUCER` | 200 both times. Unfiltered: every organisation the test created is returned. Filtered: of those, exactly the ones with a `LARGE_PRODUCER` registration | `OrganisationTests.OrganisationSearch` |
| V1-03 | Create an organisation, add registration `LARGE_PRODUCER-2026` as `REGISTERED`, change it to `CANCELLED`, then get it | 201, 201, 200. First registration: `created` equals `updated`. Second: `created` is later than the first, and `updated` is later than its own `created` | `RegistrationTests.RegistrationCreatedAndUpdated` |
| V1-04 | Create an organisation, add two registrations, delete the original one, then get it | The delete returns 204; get returns only the two added registrations | `RegistrationTests.RegistrationDeleted` |

The fifth integration scenario, `BackwardsCompatibleWithProductionData`, is not copied. It edits MongoDB directly, which rule 3 rules out. It stays in the integration tests.

### Making the tests difficult to change

- Add `.github/CODEOWNERS` with a rule for `/tests/Api.V1.Tests/` and one for the `CODEOWNERS` file itself, so that the rule cannot be removed quietly.
- Turn on "Require review from Code Owners" for `main`. A change to a v1 test or its snapshots then cannot merge without an owner's approval.

A git submodule was also considered. It is harder still to change, but it adds work to every clone, every workflow and the Docker build, and it breaks "runs locally with no extra effort". Rule 1 keeps that option open: the project could be moved to its own repository later without being rewritten.

### Pipeline

Add a "Run v1 tests" step straight after "Run integration tests" in the three workflows that run tests: `check-pull-request.yml`, `publish.yml` and `publish-hotfix.yml`. The Compose stack is already up at that point.

```bash
dotnet build tests/Api.V1.Tests/Api.V1.Tests.csproj
dotnet test --test-modules tests/Api.V1.Tests/bin/Debug/net10.0/Api.V1.Tests.dll --no-build
```

### Supporting changes

| File | Change |
|---|---|
| `waste-organisations.slnx` | Add the project |
| `Dockerfile` | Add the project's two `COPY` lines next to the other test projects. Without them the image build cannot restore the solution |
| `AGENTS.md` | Add the build and test commands, and a rule: never edit or delete a v1 test or snapshot to make a build pass; stop and ask |
| `README.md` | Add the project to the Tests section |

## Acceptance criteria

- [ ] `tests/Api.V1.Tests` is in the solution and builds in the Docker image build. It has no project reference to `src/Api` or `tests/Testing` and does not connect to MongoDB.
- [ ] Scenarios V1-01 to V1-04 pass against the Compose stack, and pass a second time without the stack being reset in between.
- [ ] The v1 tests run locally with nothing more than `docker compose up --build -d` and the two commands in Notes.
- [ ] The v1 tests run after the integration tests in `check-pull-request.yml`, `publish.yml` and `publish-hotfix.yml`, and a failing v1 test fails the pipeline.
- [ ] `.github/CODEOWNERS` covers `/tests/Api.V1.Tests/` and itself, "Require review from Code Owners" is on for `main`, and a trial pull request that touches a v1 test cannot merge without an owner's approval.
- [ ] No file under `src/` is changed, and nothing in `tests/Api.Tests`, `tests/Api.IntegrationTests` or `tests/Testing` is changed.
- [ ] `AGENTS.md` and `README.md` describe how to run the v1 tests, and `AGENTS.md` carries the rule against editing them.

## Decisions

| Topic | Decision |
|---|---|
| How to organise the v1 tests | A separate test project in this repository |
| How tied to the code | Fully black-box: no production types, no direct database access |
| How to make them difficult to change | `CODEOWNERS` with required owner review |
| Where they run | Against Docker Compose, locally and in the pipeline. No deployed environment |
| When this story is done | The project, protection, pipeline steps and the copied scenarios. Further scenarios are follow-up work |

## Dependencies

- A repository admin, to turn on "Require review from Code Owners" for `main`.
- A decision on who the code owners are.

## Risks

| Risk | Level | Mitigation |
|---|---|---|
| A test depends on timing and fails now and then | Low | V1-03 asserts the order of timestamps, not their values, and waits briefly between writes, as the integration test does |
| Tests interfere through the shared database | Low | Rule 4, checked by running the suite twice without resetting the stack |
| The branch setting is not turned on, so the protection only notifies | Medium | It is an acceptance criterion with a trial pull request |

## Follow-up work

Not part of this story. Each is a separate work item that adds test classes to the project. Expected results for most of these have already been observed and can be turned into a spec.

1. **Remaining behaviour of the five endpoints:** validation errors (400), not found (404), every listed parameter value, boundary years, idempotent updates, deleting the last registration.
2. **Authentication and authorisation:** 401 and 403 on every endpoint. This needs limited-scope test clients added to `compose.yml`, because the stack only has clients with both scopes.
3. **Input edge cases:** malformed JSON, wrong content type, empty and lower-case values, ids that are not GUIDs.
4. **Health, documentation and routing:** `/health` endpoints, `/redoc`, a snapshot of the OpenAPI document, and 405 for unsupported methods.
5. **Likely bugs:** behaviour that looks unintended, such as `"address": null` returning 500. Whether to pin these as characterisation tests or raise tickets is undecided.

## Out of scope

- Any production code change.
- Changing or removing existing tests.
- Running the v1 tests against a CDP environment.
- 500 responses caused by a data-layer failure. A black-box test cannot produce them.
- Tests for v2.
- Creating or updating Jira tickets and Confluence pages.

## Notes

- Start the stack with `docker compose up --build -d` and stop it with `docker compose down -v --remove-orphans`, as `AGENTS.md` describes for the integration tests.
- Local commands, in the form `AGENTS.md` uses:

```bash
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build tests/Api.V1.Tests/Api.V1.Tests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test --test-modules tests/Api.V1.Tests/bin/Debug/net10.0/Api.V1.Tests.dll --no-build -v:minimal
```

- The Compose stack runs the API in the `Development` environment. Its clients are in `src/Api/appsettings.Development.json`: `Developer` (API key) and `IntegrationTest` (OAuth), both with read and write scope. The copied scenarios use the OAuth client, as the integration tests do.
- The API accepts a Bearer token signed with any key and reads the client from its `client_id` claim. The project needs its own small helper to build one. `tests/Testing/Jwt.cs` shows how, but must not be referenced.
- Give each test a `[Trait("Scenario", "<ID>")]` with its id from the table.
- In the project's Verify settings, call `UseStrictJson()` and `DontIgnoreEmptyCollections()`, and scrub `traceId`. Without the second, a snapshot drops `"registrations": []`.
- For new snapshots, run with `DiffEngine_Disabled=true`, read each `.received.json` against this spec, then accept it as `.verified.json`.
- The packages the project needs are already used in this repository, so `.github/dependabot.yml` should not need a change. Check it against the rules in `AGENTS.md`.
- `dotnet csharpier check .` and a warnings-as-errors build run in the Docker image build, so the new code must pass both.

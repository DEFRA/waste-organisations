# Waste Organisations

An API for managing organisation data and their ongoing registrations in relation to EPR.

## Prerequisites

- .NET 10
- Docker

## Setup process

- Ensure .NET 10 SDK is installed
- Ensure a container runtime is installed

### Running locally via Docker

```bash
docker compose up -d
```

This starts Mongo and Postgres, runs Liquibase once to apply the changelog in `changelog/` to Postgres, and then starts the API.

### Running via .NET

Mongo and Postgres will be needed, with the Liquibase changelog applied to Postgres. They can be started as follows:

```bash
docker compose up mongodb postgres liquibase -d
```

Start the API as follows:

```bash
dotnet run --project ./src/Api --launch-profile Api
```

The same port (8080) is used for launch profile and Docker compose configuration, therefore only one can run at any one time. 

### Documentation

API documentation can be viewed at http://localhost:8080/redoc/index.html once the service is running.

### Postgres schema changes

The Postgres schema is managed by the [Liquibase](https://docs.liquibase.com) changelog in `changelog/`, not by EF Core migrations. `changelog/db.changelog.xml` is the master file and includes the versioned files in order.

`db.changelog-1.1.xml` adds the organisation tables: `organisation`, `organisation_type`, `address_usagetype`, `address` and `organisation_address`. The EF Core entities that map them live in `src/Api/Data/Postgres/Entities`.

To change the schema:

- Add a new `changelog/db.changelog-1.N.xml` file (the next number after the latest one) containing the new changesets.
- Include it at the end of `changelog/db.changelog.xml`, in the same way `db.changelog-1.0.xml` is included.
- Never edit a changeset once it is published. Liquibase records a checksum for every changeset it has applied, so a change is always a new changeset.

Apply the changelog to the local database again as follows:

```bash
docker compose up liquibase
```

To inspect what has been applied to the local database:

```bash
docker compose exec postgres psql -U postgres -d waste_organisations -c 'select tag from databasechangelog'
```

### Stopping and clearing local resources

```bash
docker compose down
```

To remove local data:

```bash
docker compose down -v --remove-orphans
```

## API versioning

Clients request a version with the `Accept` header, not the URL path: `Accept: application/vnd.defra.waste-organisations+json; version=<n>`. Any other `Accept` value - absent, `application/json`, `*/*`, or our media type without a `version` parameter - is served v1. There is one version for the whole API, and v1 is the permanent default: it stays the default even after a v2 exists.

A version an endpoint does not support, or a malformed or ambiguous `version`, gets `406 Not Acceptable` with a problem details body (`detail: "Unsupported API version"`). Authentication and authorisation still run first, so an unauthenticated or unauthorised request gets `401`/`403` whatever version it asks for.

An endpoint supports v1 only unless it declares otherwise. To support additional versions, add `.WithApiVersions(1, 2)` next to the endpoint's other `.With...`/`.Produces...` calls. Two endpoints may share a path and HTTP method when they declare different versions: `Defra.WasteOrganisations.Api.Versioning.ApiVersionMatcherPolicy` picks the one for the requested version during routing, and a v1 request only ever sees v1 endpoints. The check itself lives in one middleware (`Defra.WasteOrganisations.Api.Versioning.ApiVersioningMiddleware`), registered straight after `app.UseAuthorization()`, and applies to every endpoint except `/health`, `/health/*`, `/openapi/*`, `/redoc` and `/redoc/*`. Routing's own answers are not versioned: an unknown path is `404` and a method no endpoint answers is `405`, whatever version is asked for. See [specs/0004-REGEN-167-implement-header-based-api-versioning.md](specs/0004-REGEN-167-implement-header-based-api-versioning.md) for the full design.

## Tests

Tests with the `IntegrationTests` trait require additional local dependencies - either the API running in Docker, or the API running via .NET with Mongo, Postgres and Liquibase started in Docker.

Running unit tests:

```bash
dotnet build tests/Api.Tests/Api.Tests.csproj
dotnet test --test-modules tests/Api.Tests/bin/Debug/net10.0/Api.Tests.dll --no-build
```

Running integration tests with dependencies:

```bash
dotnet build tests/Api.IntegrationTests/Api.IntegrationTests.csproj
dotnet test --test-modules tests/Api.IntegrationTests/bin/Debug/net10.0/Api.IntegrationTests.dll --no-build
```

Running v1 API tests (with the API running in Docker):

```bash
dotnet build tests/Api.V1.Tests/Api.V1.Tests.csproj
dotnet test --test-modules tests/Api.V1.Tests/bin/Debug/net10.0/Api.V1.Tests.dll --no-build
```

The v1 API tests are black-box tests that pin the current behaviour of the v1 API. Changes to them or their snapshots need approval from the code owners. Run them after the integration tests, not at the same time: the integration tests clear the organisation data the v1 tests create.

Running all tests (with the integration-test dependencies available):

```bash
dotnet build
dotnet test --test-modules tests/Api.Tests/bin/Debug/net10.0/Api.Tests.dll --no-build
dotnet test --test-modules tests/Api.IntegrationTests/bin/Debug/net10.0/Api.IntegrationTests.dll --no-build
dotnet test --test-modules tests/Api.V1.Tests/bin/Debug/net10.0/Api.V1.Tests.dll --no-build
```

## Code quality

SonarQube cloud is configured and all Defra rules are mandated. 

See https://sonarcloud.io/project/overview?id=DEFRA_waste-organisations for project information.

## Dependency management

Dependabot is configured for ongoing dependency management.

See [dependabot.yml](.github/dependabot.yml) for group configuration.

## Build pipeline

- [Pull requests](.github/workflows/check-pull-request.yml)
  - Run all tests
  - Build Docker image
  - Check image with Trivy
  - Sonar
- [Publish](.github/workflows/publish.yml)
  - Merge PR to main
  - Build Docker image and publish to CDP
  - Publish the Liquibase changelog (`changelog/`) to CDP as a database migrations artefact, versioned with the image
  - Sonar

## CDP

Review CDP documentation and process for relevant portal operations.

## Licence Information

THIS INFORMATION IS LICENSED UNDER THE CONDITIONS OF THE OPEN GOVERNMENT LICENCE found at:

http://www.nationalarchives.gov.uk/doc/open-government-licence/version/3

### About the licence

The Open Government Licence (OGL) was developed by the Controller of Her Majesty's Stationery Office (HMSO) to enable information providers in the public sector to license the use and re-use of their information under a common open licence.

It is designed to encourage use and re-use of information freely and flexibly, with only a few conditions.

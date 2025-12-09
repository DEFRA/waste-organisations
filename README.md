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

### Running via .NET

Mongo will be needed and can be started as follows:

```bash
docker compose up mongodb -d
```

Start the API as follows:

```bash
dotnet run --project ./src/Api --launch-profile Api
```

The same port (8080) is used for launch profile and Docker compose configuration, therefore only one can run at any one time. 

### Documentation

API documentation can be viewed at http://localhost:8080/redoc/index.html once the service is running.

### Stopping and clearing local resources

```bash
docker compose down
```

To remove local data:

```bash
docker compose down -v --remove-orphans
```

## Tests

Tests with the `IntegrationTests` trait require additional local dependencies - either the API running in Docker or Mongo.

Running tests without dependencies:

```bash
dotnet test --filter "Category!=IntegrationTests"
```

Running tests with dependencies:

```bash
dotnet test --filter "Category=IntegrationTests"
```

Running all:

```bash
dotnet test
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
  - Sonar

## CDP

Review CDP documentation and process for relevant portal operations.

## Licence Information

THIS INFORMATION IS LICENSED UNDER THE CONDITIONS OF THE OPEN GOVERNMENT LICENCE found at:

http://www.nationalarchives.gov.uk/doc/open-government-licence/version/3

### About the licence

The Open Government Licence (OGL) was developed by the Controller of Her Majesty's Stationery Office (HMSO) to enable information providers in the public sector to license the use and re-use of their information under a common open licence.

It is designed to encourage use and re-use of information freely and flexibly, with only a few conditions.
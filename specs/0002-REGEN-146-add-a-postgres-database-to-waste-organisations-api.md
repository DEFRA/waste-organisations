# REGEN-146: Add a Postgres Database to waste-organisations api

| | |
|---|---|
| Type | Task: infrastructure and wiring. No change to what the API does for its current clients |
| Priority | P1 (High): it blocks the v2 data stories under REGEN-125, but nothing is broken today |
| Parent | REGEN-125 Access to org data via Org API |
| Risk | Medium (see Risks) |
| Source | Jira story REGEN-146 (reporter darren.walker, assignee Tom Harrison), the 3-amigos outcomes comment added to it on 13 October 2026, the refinement answers of 13 October 2026, and the follow-ups of 13 October 2026 that a flag sets, per environment, whether a failing Postgres check reports `Degraded` or `Unhealthy` |

## Problem/Opportunity

The v2 functionality under REGEN-125 will store its data in Postgres. The API today talks only to MongoDB: there is no Postgres library, no configuration, no local database, no way to get a schema into a CDP (Core Delivery Platform) environment, and nothing that shows whether Postgres is reachable.

Without this story every v2 data story would have to do that setup itself. With it, those stories start from a working connection, an EF Core context, a migration mechanism that CDP applies, a local database and a health check.

Who benefits:

- **Engineers building v2:** they add entities and changelog files; the plumbing exists.
- **Operators and the team:** the health endpoints show whether the API can reach Postgres in each environment.

All MongoDB behaviour stays exactly as it is.

### What the 3-amigos agreed (13 October 2026)

- Add another health check for the Postgres database, showing that it is up and running.
- Add Postgres to Docker Compose for local development.
- Follow the pattern used for MongoDB for the Postgres configurables.
- The database name can be `waste-organisations`.
- How a Postgres database is created for dev and test on CDP is still open: Ogonna Iwunze is looking into it.

Attendees: Ogonna Iwunze, Tom Harrison, Deyan Stanchev, Ibrahim Uylas.

### The CDP standard for Postgres

There is no CDP .NET template with Postgres: `DEFRA/cdp-dotnet-backend-template` and `DEFRA/cdp-node-backend-template` ship with MongoDB only. The standard is visible in CDP's own tooling and in the DEFRA .NET services already running Postgres on CDP (`identity-service-helper`, `lis-api-cattle`) and the platform team's own `cdp-sbom-explorer-backend`:

| Concern | CDP standard | Evidence |
|---|---|---|
| Database | Aurora PostgreSQL, one database per service, provisioned by the platform | CDP documentation (internal), the services above |
| Authentication | AWS IAM authentication in CDP environments: a short-lived RDS auth token generated with the task role is used as the password. A plain password only locally | `identity-service-helper` (`RDSAuthTokenGenerator`, `UsePeriodicPasswordProvider`), `cdp-sbom-explorer-backend` (`@aws-sdk/rds-signer`) |
| Driver and mapping | Npgsql with `Npgsql.EntityFrameworkCore.PostgreSQL` | both .NET services |
| Schema changes | A Liquibase changelog in the repository, published by `DEFRA/cdp-build-action/publish-db-migrations` after the image build and applied by CDP to the environment database. Not EF Core migrations | `cdp-build-action/publish-db-migrations/README.md`, `cdp-sbom-explorer-backend/.github/workflows/publish.yml` |
| Local development | A `postgres` container plus a `liquibase` container that applies the changelog before the API starts | all three services |
| Health | A database health check on the health endpoint | both .NET services |

## Proposed Solution

Add Postgres beside MongoDB, following the Mongo pattern in the code and the CDP standard above: an options class and settings, a shared Npgsql data source with IAM authentication in CDP, an empty EF Core context on top of it, a Postgres health check on the readiness endpoints whose failure status a configuration flag sets per environment, a Liquibase changelog with a baseline tag, Postgres and Liquibase in Docker Compose, the changelog published from the publish workflows, and the database enabled for the service in CDP dev and test.

### Configuration

A `Postgres` section beside `Mongo`, bound to `PostgresOptions` with data annotations, the way `MongoDbOptions` is bound.

| Key | `appsettings.json` (CDP) | `appsettings.Development.json` and `appsettings.IntegrationTests.json` | `compose.yml` (`api` service) |
|---|---|---|---|
| `Postgres:Host` | `set-automatically-when-deployed` | `127.0.0.1` | `postgres` |
| `Postgres:Port` | `5432` | `5432` | |
| `Postgres:DatabaseName` | `waste-organisations` | `waste-organisations` | |
| `Postgres:User` | `set-automatically-when-deployed` | `postgres` | |
| `Postgres:Password` | not set | `postgres` | |
| `Postgres:UseIamAuthentication` | `true` | `false` | |
| `Postgres:HealthCheckFailureStatus` | `Degraded` | `Unhealthy` | |

`HealthCheckFailureStatus` is the feature flag for the health check (Health check): the status the check reports when it cannot reach Postgres. It is a `HealthStatus` from `Microsoft.Extensions.Diagnostics.HealthChecks`, and only `Degraded` and `Unhealthy` are valid; `Healthy` is rejected by the validator because it would hide a failure. It is `Degraded` in `appsettings.json` and the `PostgresOptions` default, so in CDP a failure is visible but does not take the service out of service. It is `Unhealthy` locally and in the integration tests, so a broken local setup fails loudly. Per environment it is changed through cdp-app-config with `Postgres__HealthCheckFailureStatus`.

`Host`, `DatabaseName` and `User` are required. `Password` is required only when `UseIamAuthentication` is `false`; validate that with `IValidateOptions<PostgresOptions>`, since a data annotation cannot express it. In CDP the real values come through cdp-app-config as `Postgres__*` environment variables and secrets, exactly as `Mongo__DatabaseUri` does today.

### Packages (`src/Api/Api.csproj`)

| Package | Why |
|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | EF Core provider; brings `Npgsql` and `Microsoft.EntityFrameworkCore` |
| `AWSSDK.RDS` | `RDSAuthTokenGenerator`, which signs the IAM auth token |
| `AspNetCore.HealthChecks.NpgSql` | The health check, matching the `AspNetCore.HealthChecks.MongoDb` package already referenced |

### Data source and DbContext

A new `AddPostgres(IConfiguration, bool integrationTest)` in `src/Api/Data/`, called from `Program.cs` next to `AddDbContext`:

1. Bind and validate `PostgresOptions`.
2. If `integrationTest` is true, return. `Api.Tests` runs the API in-process with no databases, and the Mongo registration already skips in the same way.
3. Build one `NpgsqlDataSource` with `NpgsqlDataSourceBuilder`: host, port, database and user from the options.
   - IAM on: `SslMode = Require`, and `UsePeriodicPasswordProvider` returning `RDSAuthTokenGenerator.GenerateAuthToken(region, host, port, user)` with the default AWS credential chain (the ECS task role, as the MongoDB `MONGODB-AWS` mechanism already uses). Region `eu-west-2`. Refresh every 10 minutes, retry after 30 seconds on failure; the token itself lives 15 minutes. These are the values `identity-service-helper` uses.
   - IAM off: the password from the options, no TLS requirement.
4. Register the data source as a singleton, and `PostgresDbContext` with `AddDbContext<PostgresDbContext>(options => options.UseNpgsql(dataSource, o => o.EnableRetryOnFailure()))`. The context has no `DbSet`s yet; v2 stories add them.
5. Nothing connects at start-up. Connections open lazily, so a missing database does not stop the API from starting; the health check reports it (Decisions).

`AddCustomTrustStore()` already runs before the database registrations. It loads the certificates CDP injects as `TRUSTSTORE_*` variables, which is where the RDS certificate authority is expected to come from (Risks).

### Health check

Register `AddNpgSql(...)` named `postgres`, taking the data source from the service provider, tagged `ready`, with `failureStatus` set from `Postgres:HealthCheckFailureStatus` and a `timeout` of 3 seconds. The check is always registered (outside `integrationTest`); the flag only decides what a failure means. It is read once at start-up from `PostgresOptions`, like the other Postgres settings, and no feature-management library is added. Changing it in an environment takes a redeployment, which is how cdp-app-config changes reach the service anyway.

ASP.NET Core maps `Healthy` and `Degraded` to HTTP 200 and `Unhealthy` to 503, and the overall status of an endpoint is the worst of its checks. Result:

| Endpoint | Postgres reachable | Postgres failing, flag `Degraded` | Postgres failing, flag `Unhealthy` |
|---|---|---|---|
| `/health` and `/health/authorized` | 200 `Healthy` | 200 `Degraded`: CDP's readiness probe passes and the instance stays in service | 503 `Unhealthy`: the instance is taken out of service until Postgres is reachable |
| `/health/all` | 200, `postgres: Healthy` | 200, overall `Degraded`, `postgres: Degraded` with the exception message | 503, overall `Unhealthy`, `postgres: Unhealthy` with the exception message |

The failure is observable in both modes:

- `/health/all` carries the exception message, which tells the causes apart: for example a DNS or connection error when the database is not provisioned or not reachable, and `28P01: password authentication failed` (or the IAM equivalent) when the credentials are wrong.
- The health check service logs every non-healthy result: `Degraded` as a warning and `Unhealthy` as an error, under `Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService`. CDP probes `/health` continuously, so a failing Postgres produces a steady stream of warnings in the service's logs in OpenSearch, which a dashboard or alert can watch.

The 3-second `timeout` matters for `Degraded` mode: a connection that hangs (for example a security group dropping packets) would otherwise wait for the Npgsql connect timeout (15 seconds by default), likely longer than CDP's probe waits (to confirm with the platform), and the probe would fail the instance regardless of the flag. On timeout the check reports the configured failure status.

Nothing connects at start-up, so an unprovisioned database or wrong credentials cause no error outside the health check until code uses Postgres, and no code does in this story.

The registration is skipped when `integrationTest` is true, with the data source, whatever the flag says.

Consequence: the code can be deployed to a CDP environment before Postgres is provisioned there, or with credentials that turn out to be wrong, and the service stays in service while `/health/all` and the logs show what is wrong. Once `postgres` reports `Healthy` in an environment, the flag can be switched to `Unhealthy` there so Postgres becomes a hard readiness dependency (CDP environments, Work breakdown).

### Schema: Liquibase changelog

| Path | Content |
|---|---|
| `changelog/db.changelog.xml` | The master file, including the versioned files in order |
| `changelog/db.changelog-1.0.xml` | One changeset that tags the database `v1.0.0`. That is the "empty schema": it proves the migration ran (a row in `databasechangelog`) and gives a baseline to roll back to |

Later stories add `db.changelog-1.N.xml` files and include them from the master. A changeset is never edited once published. EF Core never runs migrations; `dotnet ef migrations` is not used.

### Docker Compose (`compose.yml`)

| Service | Change |
|---|---|
| `postgres` | `postgres:16-alpine` pinned by digest, as `mongodb` is. `POSTGRES_USER`, `POSTGRES_PASSWORD` and `POSTGRES_DB` matching the Development settings. Port `5432:5432`. Volume `postgres-data:/var/lib/postgresql/data`. Health check `pg_isready -U postgres -d waste-organisations` |
| `liquibase` | `liquibase/liquibase` 5.x pinned by digest, with the PostgreSQL driver added (`lpm add postgresql --global` in a `dockerfile_inline`, as `cdp-sbom-explorer-backend` does). Mounts `./changelog` to `/liquibase/changelog`, is configured with `LIQUIBASE_COMMAND_URL`, `_USERNAME`, `_PASSWORD` and `_CHANGELOG_FILE=db.changelog.xml`, runs `update` once, `restart: no`, depends on `postgres` being healthy |
| `api` | Adds `Postgres__Host: postgres`, and `depends_on` `liquibase` with `condition: service_completed_successfully`. The existing container health check on `/health/all` then also covers Postgres |
| `volumes` | Adds `postgres-data` |

`docker compose up --build -d` brings up both databases, applies the changelog and starts the API, with no extra step. `docker compose down -v --remove-orphans` removes the data, as today.

### Pipeline

| File | Change |
|---|---|
| `.github/workflows/publish.yml` | After `Build and Publish`, add `Publish database migrations` using `DEFRA/cdp-build-action/publish-db-migrations` with `path: ./changelog` and the GitHub token, pinned to the same commit as the `build` action. The action versions the changelog with the release tag of the image, so it must run after the build action, and it fails if the folder is empty |
| `.github/workflows/publish-hotfix.yml` | The same step after `Publish Hot Fix` |
| `.github/workflows/check-pull-request.yml` | No change. `docker compose up -d` already runs there, so the Postgres container, the Liquibase run and the new integration tests run on every pull request |

### CDP environments (dev and test)

1. Enable Postgres for `waste-organisations` in dev and test through the platform. This is the open question Ogonna is looking into: a portal setting, a platform tenant-configuration change, or a request to the platform team in `cdp-support`.
2. Record the names of the environment variables the platform supplies (host, port, database, user) and map them to `Postgres__Host`, `Postgres__Port`, `Postgres__DatabaseName` and `Postgres__User` in cdp-app-config for both environments. `Postgres__UseIamAuthentication` stays `true` from `appsettings.json`.
3. Confirm how CDP applies the published changelog (on deployment, or triggered from the portal) and apply it to dev and test.
4. Deploy with the flag at its default, `Degraded`, and check `/health/all` in both environments. If `postgres` is `Degraded`, fix the cause the exception message names (mapping, credentials, IAM grant, TLS) and redeploy; the service stays in service meanwhile.
5. Once `postgres` is `Healthy`, set `Postgres__HealthCheckFailureStatus` to `Unhealthy` in cdp-app-config for the environment, redeploy, and check `/health` returns 200 `Healthy`.

The service can be deployed to an environment with the rest of this story's code before steps 1 to 3 are done there; `/health/all` then shows `postgres: Degraded`.

### Tests

| Project | Test |
|---|---|
| `Api.Tests` | `PostgresOptions` validation: missing `Host` fails; IAM off without `Password` fails; IAM on without `Password` passes; `HealthCheckFailureStatus` defaults to `Degraded` when not set; `Healthy` is rejected. The existing `/health` test keeps passing because nothing Postgres-related is registered in-process |
| `Api.IntegrationTests` | `GET /health/all` against the Compose stack returns 200 with `results.postgres.status` = `Healthy` (Verify snapshot, as the other scenarios use). A second test opens a connection to `127.0.0.1:5432` and checks that `databasechangelog` holds the `v1.0.0` tag, which proves the Liquibase step in Compose ran |
| `tests/Testing/Endpoints.cs` | Add `Health.All()` returning `health/all` |

### Supporting changes

| File | Change |
|---|---|
| `.github/dependabot.yml` | Add `Npgsql*`, `Microsoft.EntityFrameworkCore*` and `AspNetCore.HealthChecks.NpgSql` to `nuget-data-access`. `AWSSDK.RDS` is already matched by `nuget-aws-sdk`. In the `docker-compose` ecosystem, ignore major updates for `postgres` as is done for `mongo`, so the local database stays on the major version CDP runs |
| `README.md` | Postgres and Liquibase in the local setup section: `docker compose up postgres liquibase -d` for running the API with `dotnet run`, and how to add a changelog file |
| `AGENTS.md` | Note the new `nuget-data-access` members and that the Compose stack now includes Postgres and Liquibase |
| `Dockerfile` | No change. The changelog is not part of the image |

### Work breakdown

Slices 1 to 3 can be built and verified locally and in CI without CDP, and deployed to dev and test with the flag at `Degraded`. Slice 4 depends on the platform answer and ends with the flag at `Unhealthy`.

| Slice | Work | Verified by |
|---|---|---|
| 1 | `PostgresOptions` with `HealthCheckFailureStatus`, `AddPostgres`, the data source, `PostgresDbContext`, the health check with its configurable failure status and timeout, the three settings files, package references, Dependabot groups, unit tests | `Api.Tests` passes; `/health/all` with the Development profile and a local Postgres shows `postgres: Healthy`; with a wrong `Postgres__Password` and `Postgres__HealthCheckFailureStatus=Degraded`, `/health` returns 200 `Degraded`, `/health/all` shows the authentication error and a warning is logged; with `Unhealthy`, `/health` returns 503 |
| 2 | `changelog/`, the `postgres` and `liquibase` Compose services, `api` dependencies and environment, `Health.All()`, the two integration tests, README and AGENTS updates | `docker compose up --build -d` then `Api.IntegrationTests` passes |
| 3 | `publish-db-migrations` step in both publish workflows | A merge to `main` publishes a migrations artefact alongside the image |
| 4 | Postgres enabled for dev and test on CDP, cdp-app-config mapping, changelog applied, deployment checked at `Degraded`, then `Postgres__HealthCheckFailureStatus=Unhealthy` set | `/health` returns 200 `Healthy` and `/health/all` shows `postgres: Healthy` in dev and test, with the flag at `Unhealthy` |

## Acceptance Criteria

- [ ] Postgres is available to the API in CDP dev and test: `/health` returns 200, `/health/all` reports `postgres` as `Healthy`, and the `v1.0.0` tag is present in `databasechangelog` in both environments.
- [ ] The Postgres health check is part of `/health`, `/health/authorized` and `/health/all`, and the API starts whether or not Postgres is reachable.
- [ ] With `Postgres:HealthCheckFailureStatus` at `Degraded` (or not set), a failing Postgres check, including wrong credentials, makes `/health` return 200 `Degraded`, so the instance stays in service, while `/health/all` reports `postgres: Degraded` with the exception message and a warning is logged.
- [ ] With `Postgres:HealthCheckFailureStatus` at `Unhealthy`, a failing Postgres check makes `/health` return 503 and logs an error.
- [ ] A Postgres check that does not answer within 3 seconds reports the configured failure status rather than holding the probe open.
- [ ] The flag is `Degraded` in `appsettings.json`, `Unhealthy` in the Development and IntegrationTests settings, overridable per environment through `Postgres__HealthCheckFailureStatus`, and `Healthy` is rejected at start-up.
- [ ] `docker compose up --build -d` starts Postgres, applies the changelog and starts the API with Postgres healthy, with no manual step. `docker compose down -v --remove-orphans` removes the data.
- [ ] `Postgres` configuration follows the `Mongo` pattern: a bound and validated options class, values in the three settings files, overridable through `Postgres__*` environment variables.
- [ ] `PostgresDbContext` is registered on the shared data source and has no entities. `Npgsql.EntityFrameworkCore.PostgreSQL`, `AWSSDK.RDS` and `AspNetCore.HealthChecks.NpgSql` are the only packages added.
- [ ] In CDP the API authenticates to Postgres with IAM; no database password is stored anywhere in the repository except the local Compose and Development values.
- [ ] The schema is managed by the Liquibase changelog only, published by `publish.yml` and `publish-hotfix.yml` through `cdp-build-action/publish-db-migrations`. No EF Core migration exists.
- [ ] `Api.Tests` and `Api.IntegrationTests` pass locally and in `check-pull-request.yml`, including the new tests.
- [ ] All MongoDB code, configuration and tests are unchanged, and the v1 behaviour tests from REGEN-145 (once merged) still pass.
- [ ] `dotnet csharpier check .` and a warnings-as-errors build pass, and `.github/dependabot.yml` parses and follows the rules in `AGENTS.md`.
- [ ] README and AGENTS.md describe the local Postgres setup.

## Out of Scope

- Any table, entity, `DbSet` or repository for v2 data.
- EF Core migrations, in any form.
- Any change to MongoDB code, configuration, indexes or tests.
- A read-only data source against the Aurora reader endpoint. `identity-service-helper` wires one; add it when a read-heavy feature needs it.
- Postgres in production or any environment other than dev and test.
- Adding a MongoDB health check. There is none today, although `AspNetCore.HealthChecks.MongoDb` is referenced; worth its own small ticket.
- Working out how CDP provisions Postgres. That is Ogonna's action and a dependency of this story, not part of it.
- Creating or updating Jira tickets and Confluence pages.

## Decisions

| Topic | Decision |
|---|---|
| Work type | Task, with the CDP provisioning for dev and test as a dependency. The story is done when Postgres is healthy in both environments |
| Driver and mapping | Npgsql with one shared `NpgsqlDataSource`, and EF Core on top of it now with an empty `PostgresDbContext`, as the DEFRA .NET services on CDP do. v2 stories add entities rather than choosing a mapper |
| Schema management | Liquibase, because that is what CDP's build action publishes and the platform applies. EF Core migrations would not reach the environment database |
| Initial schema | A tag-only changeset. No tables until a story needs them |
| Authentication | IAM in CDP, password locally, switched by `Postgres:UseIamAuthentication`. The same split both DEFRA .NET services use |
| Health check tag | `ready`, so once the check is on, Postgres is part of readiness |
| Health check flag | `Postgres:HealthCheckFailureStatus` sets what a failing check reports: `Degraded` by default and in CDP until Postgres is confirmed healthy there, `Unhealthy` locally, in tests, and in dev and test once confirmed. Rather than an on/off switch, because a switched-off check would hide wrong credentials or a missing IAM grant until the moment it was switched on, and then cause an outage; `Degraded` keeps the failure visible in `/health/all` and the logs without failing readiness. `Healthy` is not allowed, as it would be the same as having no check. A plain options value rather than `Microsoft.FeatureManagement`: it is one setting read at start-up, and per-environment values already come from cdp-app-config |
| Health check timeout | 3 seconds, so a hanging connection reports the failure status before CDP's probe gives up; otherwise `Degraded` would not protect against a network-level failure |
| Start-up behaviour | The API starts whether or not Postgres is reachable. Readiness reports the state, so a Postgres outage does not crash-loop the container or stop MongoDB traffic once it recovers |
| Database name | `waste-organisations`, as agreed. If CDP names the database itself, the platform's name wins and the settings files change |

## Dependencies

- **Ogonna's finding** on how Postgres is provisioned for a CDP service in dev and test, and the environment variables the platform then provides. Slice 4 cannot start without it. Slices 1 to 3 can be deployed before it, with the health check flag at `Degraded`.
- **cdp-app-config access** for `waste-organisations` in dev and test, to map those variables.
- **Confirmation of the Aurora PostgreSQL major version** CDP runs, so the Compose image matches it.

## Risks

| Risk | Level | Mitigation |
|---|---|---|
| The code reaches dev or test before Postgres is provisioned there, or with wrong credentials, and readiness fails | Low | `Postgres:HealthCheckFailureStatus` is `Degraded` in `appsettings.json`, so readiness still passes; the failure shows in `/health/all` and as warnings in the logs |
| A `Degraded` Postgres goes unnoticed, because nothing fails | Medium | Check `/health/all` after every deployment in slice 4. Switch the flag to `Unhealthy` once `postgres` is `Healthy`, which the acceptance criteria require for dev and test. Consider an OpenSearch alert on the health check warnings |
| The flag is switched to `Unhealthy` while Postgres is still failing, and readiness fails | Low | Switch only after `/health/all` shows `postgres: Healthy` at `Degraded`. Setting it back to `Degraded` and redeploying restores readiness |
| A failing Postgres check logs a warning on every probe, which is noisy in an environment where Postgres is not provisioned yet | Low | Accept it for dev and test until slice 4 is done; it is the signal that something is still missing |
| CDP's Postgres offering differs from what the reference services assume (different variable names, no IAM, a fixed database name) | Medium | Slices 1 to 3 are unaffected; the options class takes whatever values cdp-app-config maps. Adjust the mapping, not the code |
| TLS handshake to Aurora fails because the RDS certificate authority is not trusted by the container | Medium | The CDP trust store loaded by `AddCustomTrustStore()` is expected to include it, as it does for DocumentDB. If not, set `RootCertificate` on the data source builder to the RDS CA bundle, or `SslMode = VerifyFull` with it. Confirm in dev before test |
| A hyphen in the database name `waste-organisations` needs quoting in SQL and some tools | Low | Nothing in this story writes SQL against it. Liquibase, Npgsql and the Postgres image accept it. If it causes friction, switch to `waste_organisations` before any data is stored |
| The Liquibase container cannot download the PostgreSQL driver in CI | Low | Vendor the JDBC jar under `changelog/` instead, as `identity-service-helper` does |
| `publish-db-migrations` behaves unexpectedly on the first run | Low | The action only publishes when the folder changed and does not fail for an unchanged folder. Watch the first run on `main` |
| The Compose stack gets slower to start, so the integration test step in CI times out | Low | Liquibase on an empty changelog takes seconds. The `api` health check already waits with retries |
| EF Core adds packages that Dependabot bumps often | Low | They sit in `nuget-data-access`, so they arrive as one grouped pull request |
| Sonar flags new, uncovered wiring code | Low | Mark the registration code `ExcludeFromCodeCoverage` with the same "See integration tests" justification as the Mongo registration. The options validation is unit-tested |

## Notes

- Branch name: `REGEN-146-add-a-postgres-database-to-waste-organisations-api`.
- `Program.cs` reads `--integrationTest=true` into a flag and `TestWebApplicationFactory` sets it; `AddPostgres` must honour it exactly as `AddDbContext` does, or `Api.Tests` will try to reach a database.
- `AspNetCore.HealthChecks.NpgSql` can take a `Func<IServiceProvider, NpgsqlDataSource>`; use that so the check shares the application's data source and its IAM password provider rather than opening its own connection string.
- `RDSAuthTokenGenerator.GenerateAuthToken` is synchronous and signs locally; it does not call AWS. The credential chain resolves the ECS task role in CDP and the `test` keys from `compose/aws.env` locally, which is harmless because IAM is off locally.
- With `UseNpgsql(dataSource)` EF Core reuses the data source and its password provider, so IAM tokens refresh without any EF Core configuration.
- Pin the `postgres` and `liquibase/liquibase` images by digest, as `mongo` is pinned, and expect Dependabot `docker-compose` pull requests for them.
- Reference examples, all public on GitHub under `DEFRA`: `identity-service-helper` (`src/Database/Postgres.Database/`, `changelog/`, `compose.yml`), `lis-api-cattle` (`development-compose.yml`, `changelog/`), `cdp-sbom-explorer-backend` (`compose.db.yml`, `changelog/`, `.github/workflows/publish.yml`), and `cdp-build-action/publish-db-migrations/README.md`.
- Local commands, in the form `AGENTS.md` uses, after `docker compose up --build -d`:

```bash
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build tests/Api.IntegrationTests/Api.IntegrationTests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal
DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test --test-modules tests/Api.IntegrationTests/bin/Debug/net10.0/Api.IntegrationTests.dll --no-build -v:minimal
```

- To inspect the local database:

```bash
docker compose exec postgres psql -U postgres -d waste-organisations -c 'select tag from databasechangelog'
```

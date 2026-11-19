# waste-organisations

.NET 10 backend API (ASP.NET Core minimal APIs) for organisation data and their Extended Producer Responsibility (EPR) registrations, hosted on Defra's Core Delivery Platform (CDP). Data lives in Postgres (EF Core over a Liquibase-managed schema) and MongoDB. All work in this repo must follow the [DEFRA software development standards](https://github.com/DEFRA/software-development-standards).

## Repo profile

| Entry | Value |
|---|---|
| Stack rule | `csharp-dotnet` |
| Gate commands | `dotnet csharpier check .`<br>`DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build waste-organisations.slnx --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal`<br>`DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build tests/Api.Tests/Api.Tests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal`<br>`DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test --test-modules tests/Api.Tests/bin/Debug/net10.0/Api.Tests.dll --no-build -v:minimal`<br>`docker compose up --build -d`<br>`DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build tests/Api.IntegrationTests/Api.IntegrationTests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal`<br>`DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test --test-modules tests/Api.IntegrationTests/bin/Debug/net10.0/Api.IntegrationTests.dll --no-build -v:minimal`<br>`DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet build tests/Api.V1.Tests/Api.V1.Tests.csproj --no-restore -p:OpenApiGenerateDocuments=false -m:1 -nodeReuse:false --disable-build-servers -v:minimal`<br>`DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1 dotnet test --test-modules tests/Api.V1.Tests/bin/Debug/net10.0/Api.V1.Tests.dll --no-build -v:minimal`<br>`docker compose down -v --remove-orphans` |
| Gate prerequisites | Docker running; host ports 5432, 27017 and 8080 free; NuGet packages and .NET tools restored (`dotnet restore waste-organisations.slnx`, `dotnet tool restore`); Google Chrome installed, for `ReDocPageTests` |
| Test projects | `tests/Api.Tests`: units and in-process endpoint behaviour through `WebApplicationFactory`, the only coverage Sonar counts; `tests/Api.IntegrationTests`: behaviour against real Postgres and Mongo, run against the Docker Compose stack; `tests/Api.V1.Tests`: the frozen v1 contract, black-box over HTTP, run after the integration tests and never alongside them; `tests/Testing`: shared fixtures, endpoint helpers and JWT generation, referenced and not run |
| Specs folder | `specs/` |
| Spec file naming | `NNNN-<ticket-id>-slug.md` |
| Frozen artefacts | `tests/Api.V1.Tests/`; `*.verified.*` |
| Pull request template | `.github/PULL_REQUEST_TEMPLATE.md` |
| Review lenses | none |

The gate runs in the order given and stops at the first failure. The sections "Local Build And Test Checks", "Postgres And Liquibase" and "V1 API Tests" of [doc/constraints.md](doc/constraints.md) say what each step needs.

## Constraints

Read [doc/constraints.md](doc/constraints.md) before changing code, tests, a workflow, the Dockerfile or the local stack. It holds the rules that exist only in this repo and its accepted deviations from a shared or stack rule. The ones that matter most:

- v1 behaviour is frozen by `tests/Api.V1.Tests` and every `*.verified.*` snapshot. Never edit or delete one to make a build pass: a failing v1 test means the API changed, so stop and ask. New or changed behaviour goes in an endpoint that declares `.WithApiVersions(...)`.
- The Postgres schema is the Liquibase changelog in `changelog/`: a new changeset file, never an EF Core migration, never an edit to a published changeset.
- Run `Api.V1.Tests` after `Api.IntegrationTests`, never in the same `dotnet test` call or at the same time: the integration tests delete the data a v1 test creates.
- Host port 5432 must be free for the Compose Postgres. Do not change a host port in `compose.yml` or in the tests to get round a local conflict: stop and ask.
- Never log PII or secrets, and never put either into a spec, a ticket or test data.

## AI toolkit

This repo follows the Manage Account AI toolkit, `DEFRA/waste-manage-account-ai-toolkit`. The skills and the shared rules live there, not in this repo. Its `shared-instructions.md` applies to every task:

- **Claude Code:** the plugin loads it at the start of the session.
- **Codex:** read `~/.wma-ai-toolkit/shared-instructions.md` before any task.

If it is neither in your context nor at that path, or a toolkit skill is not available, stop and ask the developer to install the toolkit:

- **Claude Code:** run `claude plugin install wma-ai-toolkit --marketplace DEFRA/waste-manage-account-ai-toolkit` in a terminal, then start a new session.
- **Codex:** run `install.sh` from the [toolkit repo](https://github.com/DEFRA/waste-manage-account-ai-toolkit); run it again to update.

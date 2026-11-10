# Container standards

Adapted for this repo from the DEFRA [container standards](https://github.com/DEFRA/software-development-standards/blob/main/docs/standards/container_standards.md), [deployment standards](https://github.com/DEFRA/software-development-standards/blob/main/docs/standards/deployment_standards.md) and [Docker guidance](https://github.com/DEFRA/software-development-standards/blob/main/docs/guides/docker_guidance.md). Last synced 9 November 2026.

## Standards

- Bespoke software is delivered as Docker containers; images are built with Docker and the local stack is defined with Docker Compose (`compose.yml`).
- Linux containers only.
- Multi-stage builds: the `Dockerfile` has `base`, `build`, `publish` and `final` stages. The final image carries only the published output and curl; tests and the SDK stay in the build stage. Preserve that structure.
- Production images are **immutable**: once built they do not change, but they are configured. All per-environment behaviour comes from environment variables set by CDP (`Mongo__*`, `Postgres__*`, `Acl__*`, `Serilog__*`), never from values baked into the image.
- Images are **self-contained** and carry all runtime dependencies. The only addition to the runtime image is curl, which the CDP platform health check requires.
- Every `FROM` line is pinned by digest (`mcr.microsoft.com/dotnet/aspnet@sha256:...`), as SonarCloud rule `docker:S8431` requires; Dependabot's `docker` ecosystem bumps the digests. Keep the SDK and ASP.NET digests on the same .NET feature version.
- Images are tagged and versioned by the CDP build action on merge to `main` (semantic version tags), and the application reads its version from `SERVICE_VERSION`.

## Accepted deviations (recorded 9 November 2026)

The DEFRA standard asks for the Defra parent images (`defradigital/dotnetcore`, `defradigital/dotnetcore-development`) and for containers that do not run as root. This repo follows the [CDP .NET backend template](https://github.com/DEFRA/cdp-dotnet-backend-template) instead:

- **Base images are Microsoft's** `mcr.microsoft.com/dotnet/sdk` and `mcr.microsoft.com/dotnet/aspnet`, pinned by digest. The CDP template uses them and the platform health check expects the resulting layout. The Defra parent images do publish .NET 10 tags (`defradigital/dotnetcore:2.1.8-dotnet10.0` on 9 November 2026), so moving to them is possible; it is a decision to take with CDP, not a change to make in passing.
- **The final stage has no `USER` instruction**, so the process runs as root inside the container. CDP runs the container on ECS with the platform's task-level controls. Adding `USER $APP_UID` (supported by the Microsoft images) is a candidate change to raise with CDP; do not make it in passing, because the platform health check and log shipping would need verifying.

Record any further deviation here with the date and the reason, and revisit these when the CDP template or the Defra parent images change.

## Local stack (`compose.yml`)

- `compose.yml` is the source of truth for the services of the local stack; read it rather than rely on a list here. A one-shot service that does its work and exits is expected to show as exited, and the services that depend on it wait for it to complete successfully (see "Postgres And Liquibase" in `AGENTS.md`).
- Images in `compose.yml` are pinned by tag and digest. The Liquibase image sits inside a `dockerfile_inline`, so Dependabot cannot see it: bump its tag and digest by hand, resolving the digest rather than typing it from memory.
- Host ports configured in `compose.yml` are fixed because the tests depend on them. Do not change them to work around a local conflict; stop the conflicting service instead.
- Development-only credentials in `compose.yml` and `compose/aws.env` are test values. Never put a real credential in a Compose file.

# Security and logging standards

Adapted for this repo from the DEFRA [security standards](https://github.com/DEFRA/software-development-standards/blob/main/docs/standards/security_standards.md), [logging standards](https://github.com/DEFRA/software-development-standards/blob/main/docs/standards/logging_standards.md), [security principles](https://github.com/DEFRA/software-development-standards/blob/main/docs/principles/security_principles.md) and [application credentials guide](https://github.com/DEFRA/software-development-standards/blob/main/docs/guides/application_credentials.md). Last synced 9 November 2026.

## Secure coding

- Follow the [OWASP Secure Coding Practices quick reference guide](https://owasp.org/www-project-secure-coding-practices-quick-reference-guide/) (version 2) and Microsoft's [secure coding guidelines for .NET](https://learn.microsoft.com/en-us/dotnet/standard/security/secure-coding-guidelines).
- Secure development is everyone's concern; security is woven into everyday practice, not bolted on.
- Validate all input at the boundary (validators and endpoint filters, see [csharp-dotnet.md](csharp-dotnet.md)). Return problem details; never leak stack traces, exception text, SQL or connection strings. `DetailedErrors` is on only in `appsettings.Development.json`.
- Authentication is the Basic API-key and JWT handlers in `src/Api/Authentication`; authorisation is the `read` and `write` scope policies declared on each endpoint. Deny by default: an endpoint without `RequireAuthorization` must be a deliberate, documented exception (today only health, OpenAPI and ReDoc).
- Never log, echo or include in a response the `Authorization` header, an API key secret or a token.
- HSTS is applied unconditionally (`UseHstsUnconditionally`). Keep it; CDP terminates TLS in front of the service.

## Secrets and credentials

- Credentials come from the CDP secrets mechanism as environment variables (`Mongo__DatabaseUri`, `Postgres__*`, `Acl__Clients__*`). Postgres in deployed environments uses IAM authentication (`UseIamAuthentication: true`), not a password.
- Never commit credentials, connection strings or `.env` values. `appsettings.json` holds `set-automatically-when-deployed` placeholders only. Development-only values (`developer-pwd`, `postgres`/`postgres`, the LocalStack `test` keys in `compose/aws.env`) live in `appsettings.Development.json`, `compose.yml` and `compose/`, and must never be reused anywhere else.
- If a secret ever reaches git history, treat it as compromised and follow the DEFRA [credential exposure process](https://github.com/DEFRA/software-development-standards/blob/main/docs/processes/credential_exposure.md): rotate first, then clean the history.
- Never bypass GitHub push protection or dismiss a security alert without the approval described in [resolving GitHub security alerts](https://github.com/DEFRA/software-development-standards/blob/main/docs/processes/github_security_alerts.md) (post in `#github-support`).

## GitHub Advanced Security

Defra has GitHub Advanced Security enabled organisation-wide. Maximise these built-in features rather than third-party tools:

- **Dependency graph** enabled (foundation for the features below).
- **Dependabot** raises PRs for vulnerable or outdated dependencies, with grouped updates to reduce noise (`.github/dependabot.yml`, see "Updating Dependabot" in `AGENTS.md`).
- **NuGet audit** runs on every restore in `all` mode (direct and transitive packages, every severity) with warnings as errors, so the build, and with it the PR check, fails on a package with a known vulnerability. This is how the repo meets the DEFRA expectation that a PR cannot introduce a vulnerable package, which the standard names the dependency review action for. Never switch the audit off or lower its level; a single advisory is suppressed only as `csharp-dotnet.md` describes.
- **Security tab** reviewed regularly: Dependabot alerts, code scanning alerts, secret scanning alerts. Open alerts are resolved, not left to age.
- Trivy scans the built image in the PR workflow for OS and runtime vulnerabilities (`--ignore-unfixed`).

## Logging

This repo logs through Serilog with the Elastic Common Schema formatter (`Elastic.CommonSchema.Serilog`), configured in `appsettings*.json` and `src/Api/Utils/Logging`. Always inject `ILogger<T>` and use message templates (`logger.LogError(ex, "Failed to create index {Name}", name)`); never `Console.WriteLine`, string interpolation in the message, or a second logging library.

### Only log useful information

Errors, warnings, informational messages, debug messages and audit events. Health-check request noise is already filtered out in `ConfigureLogging`. Be aware of what third-party libraries log (EF Core command logging, the Mongo driver) and configure them rather than letting them log payloads.

### Log levels per environment

Levels stay configurable through `Serilog:MinimumLevel` in `appsettings*.json` and the `Serilog__MinimumLevel__Default` environment variable; never hard-code a level. Baseline, following the DEFRA standard: **development** debug or information; **test and staging** warnings and errors; **production** errors and critical warnings. **Project note:** `appsettings.json` currently sets `Information`, which CDP environments inherit. Aligning deployed environments with the baseline is a configuration change on CDP, tracked as its own story, not something a code change does in passing.

### Use structured logs

Follow the [Elastic Common Schema (ECS)](https://www.elastic.co/docs/reference/ecs), already applied by the formatter, enriched with the ECS HTTP context, `service.version` and the correlation id from the `x-cdp-request-id` trace header (`TraceHeader` options). Do not invent custom log formats or fields; add structured properties through the message template.

### Never log PII or sensitive data

Sanitise before logging. This includes, but is not limited to: names, addresses, email addresses, phone numbers, National Insurance numbers, company registration details tied to a person, bank details, usernames, passwords, API keys and tokens. Log identifiers (organisation id, request id) instead of values. Never log a request or response body.

### Centralised logging and protective monitoring

Logs go to stdout and are aggregated by CDP's centralised logging. Protective monitoring events (authentication failures, access control changes, configuration changes, significant decisions) should be sent to the Security Operations Centre; engage the SOC team for guidance before adding such events.

## Metrics

Request metrics go through the existing `Amazon.CloudWatch.EMF` setup in `src/Api/Utils/Metrics`. Metric dimensions must never contain PII or free-text input.

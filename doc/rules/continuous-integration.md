# Continuous integration standards

Adapted for this repo from the DEFRA [continuous integration standards](https://github.com/DEFRA/software-development-standards/blob/main/docs/standards/continuous_integration.md). Last synced 9 November 2026.

## What CI must check

Every branch build verifies: the solution builds, tests pass, code style passes (CSharpier), code quality passes (SonarQube Cloud) and security checks pass (Trivy image scan, Dependabot, dependency review).

In this repo that is `.github/workflows/check-pull-request.yml`:

1. Unit tests (`Api.Tests`) on the runner.
2. `docker compose up -d`, then `Api.IntegrationTests` and `Api.V1.Tests` against the stack, in that order.
3. The Docker image build, which runs `dotnet csharpier check .`, `dotnet build --warnaserror` and `Api.Tests` again inside the build stage.
4. Trivy against the built image, ignoring unfixed findings.
5. The reusable `sonarcloud.yml` workflow, which reports `Api.Tests` coverage to SonarQube Cloud.

The same gate runs locally before a PR is proposed (commands in "Local Build And Test Checks" and "V1 API Tests" in `AGENTS.md`). `publish.yml` and `publish-hotfix.yml` run the unit and integration tests; the v1 tests run on pull requests only. A test step changed in one workflow is considered for the other two, and a deliberate difference is recorded here.

## Branch protection

`main` is protected by repository rulesets requiring `Run Pull Request Checks` and `CDP SonarCloud Scan / CDP SonarCloud coverage scan`, two approving reviews, code owner review, resolved threads and signed commits. Renaming a job renames a required check: update the ruleset in the same change, or the PR cannot merge.

## GitHub Actions

Use GitHub Actions for CI builds. The publish workflows call Defra's `cdp-build-action`; do not replace them with a bespoke deployment.

### Commit SHA pinning (required)

Pin every action to a full-length commit SHA, never a tag or branch. Tags and branches can be moved by an attacker who compromises the action's repository, an increasingly common supply-chain attack. Reference the version as a trailing comment for clarity:

```yaml
# Use:
uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7

# Not:
uses: actions/checkout@v7
# Nor:
uses: actions/checkout@main
```

To update an action, test the new version, then update the SHA. Dependabot's `github-actions` group does this weekly and keeps the comment current. The same applies to the Trivy image (`aquasec/trivy:<tag>@sha256:...`) and every `FROM` line in the `Dockerfile`.

### Permissions

Workflows declare the least `permissions` they need at the top level and widen per job (`contents: write` and `id-token: write` only on the publish job). Secrets are passed with `secrets: inherit` to the reusable Sonar workflow only.

## Security in CI

- **Dependabot** is enabled with grouped updates for NuGet (daily), GitHub Actions, Docker and Docker Compose (weekly).
- **Dependency review action** belongs in the PR workflow, to block newly introduced vulnerable packages.
- **Trivy** scans the built image on every PR.
- **GitHub Security tab** is reviewed regularly; open alerts are resolved. This includes vulnerabilities in GitHub Actions themselves.

## Maintainability and test coverage

- SonarQube Cloud (project `DEFRA_waste-organisations`, Defra organisation) runs on every PR and on `main` through `sonarcloud.yml`. The build is analysed with `dotnet-sonarscanner` and the Defra quality gate must pass: new-code coverage of at least 90%, no new blocker, critical or major issues, no new duplication above 3%, every new security hotspot reviewed.
- Coverage comes from `Api.Tests` only (`--coverage --coverage-output-format xml`). Integration and v1 tests do not count towards the gate, so business logic needs unit tests even when an integration test also covers it.
- Sonar does not run for Dependabot PRs (`github.actor != 'dependabot[bot]'`); everything else does.

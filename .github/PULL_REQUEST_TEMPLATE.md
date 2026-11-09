<!-- Title: REGEN-NNN: Short summary (imperative, 50 characters). See doc/rules/git-workflow.md. -->

## Why

<!-- Why this change is needed and briefly what it does. Not a copy of the story. -->

Jira: REGEN-NNN
Spec: specs/NNNN-REGEN-NNN-slug.md

## What changed

- <!-- One bullet per behaviour change, not per file -->

## How to test

<!-- Everything QA or another developer needs to verify this change without reading the code.
     Required for any behaviour, schema or configuration change. For a docs-only or tooling PR,
     replace this section with "No behaviour change: <reason>". -->

**Environment:** <!-- Local: `docker compose up --build -d`; and/or the CDP environment -->
**Preconditions:** <!-- Changelog applied: `docker compose exec postgres psql -U postgres -d waste_organisations -c 'select tag from databasechangelog'`; data needed; new configuration or secrets -->
**Credentials:** <!-- Client from compose.yml or appsettings.Development.json: Developer (read and write), ReadOnlyApiKey, WriteOnlyApiKey, ReadOnlyOAuth, WriteOnlyOAuth -->

### Steps

1. <!-- Request: method, path, `Accept: application/vnd.defra.waste-organisations+json; version=N`, body. Expected: status, headers, body shape -->
2. <!-- Negative case: wrong scope gives 403; invalid body gives 400 with the error key; unsupported version gives 406 -->
3. <!-- Confirm the data: psql or Mongo query, or the GET that reads it back -->

### Automated tests

- <!-- Project and test class or spec scenario IDs covering this change, for example Api.IntegrationTests CreateOrganisationTests; spec 0005 scenarios ORG-POST-01 to 05 -->
- <!-- Snapshots added or changed, and why -->

### Not covered

- <!-- What needs a human: exploratory areas, performance, ReDoc rendering, CDP-only configuration -->

## Checklist

- [ ] Full gate run locally: CSharpier, build, Api.Tests, then Api.IntegrationTests and Api.V1.Tests against Docker Compose
- [ ] Tests added or updated; coverage not decreased
- [ ] No PII or secrets in code, logs or test data
- [ ] README or spec updated where behaviour or setup changed
- [ ] Dependabot groups updated for any new package

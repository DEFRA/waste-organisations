# REGEN-162: Write the OpenAPI contract for Create Organisation (v2)

| | |
|---|---|
| Type | Task: a hand-written OpenAPI document and its supporting documentation. No change to what the API does |
| Priority | P1 (High): consumers need the contract to review, mock and build against before the v2 create endpoint is built under REGEN-141 |
| Parent | REGEN-141 Create an Organisation, under REGEN-125 Access to org data via Org API |
| Risk | Medium (see Risks) |
| Approach | Design-first ([OpenAPI-driven API design](https://swagger.io/blog/openapi-driven-api-design/)): agree the contract, then build to it |
| Source | The decisions of 21 October 2026 (D1–D6), and the Postgres schema in PR [#203](https://github.com/DEFRA/waste-organisations/pull/203) (`changelog/db.changelog-1.1.xml`, `src/Api/Data/Postgres/Entities/`, `specs/0003-REGEN-141-organisation-and-address-db-schema.md`) |

## Problem/Opportunity

The v2 API under REGEN-125 will let consumers create organisations in Postgres. Consumers (RPD, RE/EX and the front ends) need to know what to send and what they get back before the endpoint exists, so they can plan, mock and generate clients in parallel with the build.

Today there is nothing to give them. The only OpenAPI document is the v1 one at `/openapi/v1.json`, which is generated from code, and v1 has no `POST /organisations`.

Who benefits:

- **Consumers:** they can review the contract, mock it and generate clients before the endpoint is built.
- **Engineers building the endpoint:** they build to an agreed contract, not to assumptions.

## Proposed Solution

Write `openapi/organisations-v2.yaml`, an OpenAPI 3.1 contract for `POST /organisations` (v2, selected by the `Api-Version` header), based on the Postgres schema in PR #203. The contract:

- describes the request and response bodies for creating an organisation;
- publishes the allowed values (`homeNation`, `organisationType`, address usage types) with their meanings;
- documents authentication, every response status, the error format and worked examples;
- passes an OpenAPI linter.

Building the endpoint itself is out of scope.

### Where it lives

| Item | Decision |
|---|---|
| File | `openapi/organisations-v2.yaml` at the repository root (a new `openapi/` folder, so later v2 operations (get, update) are added to the same document) |
| Format | OpenAPI **3.1.0**, YAML. 3.1 matches JSON Schema 2020-12 and is what `Microsoft.OpenApi` 2.x / .NET 10 emits, so the generated document can be compared with this one later |
| Version | `info.version: 2.0.0-draft`, moving to `2.0.0` when consumers sign it off |
| Relation to the existing doc | The v1 document at `/openapi/v1.json` is generated from code and is not changed. The v2 contract is hand-written and is the source of truth until the endpoint exists |

### Existing API conventions (kept in v2)

- JSON property names are camelCase; identifiers are `id` (UUID).
- Errors are RFC 9457 `application/problem+json` (`AddProblemDetails`, `ProducesProblem` in `src/Api/Endpoints/Organisations/Put.cs`).
- Authentication (`src/Api/Authentication/ServiceCollectionExtensions.cs`): HTTP Basic **or** JWT Bearer. Writes need the `write` scope claim (`PolicyNames.Write`).
- Tag `Organisations`; operation names such as `CreateOrUpdateOrganisation`.

### Organisation fields (table `organisation`)

| Contract field | Column | Type in contract | Required on create | Notes |
|---|---|---|---|---|
| `id` | `id` | `string`, `format: uuid`, `readOnly` | No (server sets UUIDv7) | Returned in the body and in the `Location` header |
| `name` | `name` | `string`, `minLength: 1` | **Yes** | `NOT NULL` |
| `tradingName` | `trading_name` | `string` | No | |
| `companiesHouseNumber` | `companies_house_number` | `string`, `pattern: ^[A-Z0-9]{2}[0-9]{6}$` | **When `organisationType` is a Companies House type** (D6) | Format checked (D3): 8 characters, a 2-character prefix (`00`–`99`, or letters such as `SC`, `NI`, `OC`, `SO`, `R0`) followed by 6 digits, upper case. The endpoint checks it; the database does not |
| `homeNation` | `home_nation` | `string`, `enum: [GB-ENG, GB-SCT, GB-WLS, GB-NIR]` | **Yes** | Matches `ck_organisation_home_nation` |
| `organisationType` | `organisation_type_id` → `organisation_type.code` | `string`, enum of the 40 codes | No | Codes and descriptions copied from `OrganisationTypeCodes.All` |
| `validFrom` | `valid_from` | `string`, `format: date-time` | No | Defaults to the time of the request |
| `validTo` | `valid_to` | `string`, `format: date-time`, `readOnly` | No | Not settable on create (Q6) |
| `createdAt` / `updatedAt` | `created_at` / `updated_at` | `string`, `format: date-time`, `readOnly` | No | Set by `TimestampInterceptor` |
| `addresses` | `organisation_address` + `address` | object (see below) | **Yes, with `registeredAddress`** (D5) | The endpoint enforces this; the database cannot |

**Conditional rule (D6):** `companiesHouseNumber` is required when `organisationType` is one of the 36 Companies House `company_type` codes in `OrganisationTypeCodes.All`. It is optional for our own four codes (`sole-trader`, `partnership`, `unincorporated-association`, `public-body`) and when `organisationType` is omitted. The contract expresses this with JSON Schema 2020-12 `if`/`then` on `CreateOrganisationRequest`:

```yaml
if:
  required: [organisationType]
  properties:
    organisationType: { $ref: '#/components/schemas/CompaniesHouseOrganisationType' }
then:
  required: [companiesHouseNumber]
```

`CompaniesHouseOrganisationType` is an enum of the 36 Companies House codes. `OrganisationType` is then built as `anyOf` that enum and `OtherOrganisationType` (the four of ours), so each code is listed once. Some code generators and UI renderers ignore `if`/`then`, so the rule is also stated in plain words in the `companiesHouseNumber` and `organisationType` descriptions. The endpoint enforces it either way.

### Addresses

`addresses` is an object with one optional property per seeded usage type, as on the Confluence API page:

| Property | `address_usagetype.name` |
|---|---|
| `registeredAddress` | `Registered` |
| `auditAddress` | `Audit` |
| `serviceOfNoticeAddress` | `ServiceOfNotice` |
| `principalAddress` | `Principal` |
| `correspondenceAddress` | `Correspondence` |

`required: [registeredAddress]` (D5) and `additionalProperties: false` express "a registered address, plus any of the other four". A keyed object maps directly to the "one current address per usage type" rule (`ux_organisation_address_current`), so the contract cannot express a duplicate. An array of `{usageType, address}` would need a separate uniqueness rule.

`Address` follows the Defra standard address model (table `address`):

| Contract field | Column | Type | Required on create |
|---|---|---|---|
| `addressLine` | `address_line` | `string`, `minLength: 1` | **Yes** (D2) |
| `subBuildingName` | `sub_building_name` | `string` | No |
| `buildingName` | `building_name` | `string` | No |
| `buildingNumber` | `building_number` | `string` (not a number: "12A", "3-5") | No |
| `street` | `street` | `string` | No |
| `locality` | `locality` | `string`, `minLength: 1` (town or city) | **Yes** (D2) |
| `region` | `region` | `string` | No |
| `postalCode` | `postal_code` | `string`, `minLength: 1` | **Yes** (D2) |
| `poBox` | `po_box` | `string` | No |
| `country.iso` | `country_iso` | `string`, `pattern: ^[A-Z]{2}$` (ISO 3166-1 alpha-2) | No |
| `country.name` | `country_name` | `string` | No |
| `isUkAddress` | `is_uk_address` (generated) | `boolean`, `readOnly`; `null` when the country is unknown | n/a |

The three required address fields apply to every address kind. The `address` columns stay nullable in the database, so the endpoint enforces them, not Postgres. `postalCode` is a non-empty string with no format check, because it also holds non-UK postal codes.

The address's internal `id`, `valid_from`/`valid_to` on `organisation_address`, and `created_at`/`updated_at` are not in the contract (Q7).

Not in the contract, because they are not in the schema: SIC code, organisation size, turnover, phone number, external RPD ids (Q8), sub-type, `isAPartnership`.

### Operation

```
POST /organisations
Api-Version: 2                      (header, required)
operationId: createOrganisation
tags: [Organisations]
security: basicAuth (scope write) OR bearerAuth (JWT, scope claim write)
requestBody: application/json → CreateOrganisationRequest (required)
```

Versioning uses a request header (D1), named `Api-Version` with the value `2`:

- It is a reusable `components/parameters/ApiVersion` (`in: header`, `required: true`, `schema: {type: string, enum: ["2"]}`), so the later v2 get, update and search operations reference the same parameter.
- Responses echo it as an `Api-Version` response header, so a client can confirm which version answered.
- v1 clients send no header and are unaffected: a request without `Api-Version` is v1. v1 has no `POST /organisations`, so a `POST` without the header, or with an unsupported value, returns `400` with a problem detail that names the header.
- The document's `info.description` explains the header, because a header version is less visible than a path.

### Responses

| Status | When | Body | Headers |
|---|---|---|---|
| `201 Created` | Organisation stored | `Organisation` | `Location: /organisations/{id}`, `Api-Version` |
| `400 Bad Request` | Malformed JSON, missing or unsupported `Api-Version`, missing required field (including `addressLine`, `locality`, `postalCode` on any address), unknown `organisationType`, `companiesHouseNumber` missing for a Companies House type or in the wrong format, any other enum/pattern failure, no `registeredAddress` | `ValidationProblemDetails` (problem+json with `errors: {field: [messages]}`), naming the field, for example `addresses.registeredAddress.postalCode` (REGEN-141 scenario 3) | `Api-Version` |
| `401 Unauthorized` | No or invalid credentials | none | `WWW-Authenticate` |
| `403 Forbidden` | Authenticated without the `write` scope | none | |
| `409 Conflict` | The organisation duplicates an existing one (D4) | `DuplicateOrganisationProblem`: problem+json with the extension `existingOrganisationId` (uuid) | `Location: /organisations/{existingId}`, `Api-Version` |
| `415 Unsupported Media Type` | Not `application/json` | `ProblemDetails` | |
| `500 Internal Server Error` | Unexpected | `ProblemDetails` | |

The `409` is in the first release (D4). Its body identifies the existing organisation, so a client can fetch it instead of retrying. The contract describes the response without defining what counts as a duplicate. The `409` description will state that rule once it is agreed (Q2).

### Components

- `schemas`: `CreateOrganisationRequest`, `Organisation` (response), `Addresses`, `Address`, `Country`, `HomeNation`, `OrganisationType`, `CompaniesHouseOrganisationType`, `OtherOrganisationType`, `CompaniesHouseNumber`, `ProblemDetails`, `ValidationProblemDetails`, `DuplicateOrganisationProblem`.
- `parameters`: `ApiVersion` (header). `headers`: `ApiVersion`, `Location`.
- The request and response are separate schemas rather than one schema with `readOnly` flags. Code generators handle `readOnly` inconsistently, so separate schemas give consumers a cleaner client.
- `OrganisationType`: `anyOf` `CompaniesHouseOrganisationType` (36 codes) and `OtherOrganisationType` (4 codes), each a `type: string` `enum`, with each meaning carried twice:
  - a markdown table in `description` (all renderers show it);
  - `x-enumDescriptions` (a map), which Redoc/Scalar display and code generators can use.

  Consumers own their labels (English and Welsh). The description says so and asks them to fall back gracefully on a code they don't recognise, because new codes are added without a version bump.
- `securitySchemes`: `basicAuth` (`http`/`basic`), `bearerAuth` (`http`/`bearer`, `bearerFormat: JWT`). The description says the token must carry `scope: write`.
- `examples`: request examples `minimal` (name, homeNation, registered address), `fullLimitedCompany` (`ltd` with a Companies House number and all five addresses), `soleTraderNoCompaniesHouse` and `nonUkCorrespondenceAddress`. Response examples: `201`, `409` duplicate, `400` unknown organisation type, `400` missing `registeredAddress`, `400` `ltd` without `companiesHouseNumber`, `400` address without `postalCode`, `400` invalid `companiesHouseNumber`, `400` missing `Api-Version`, `415` and `500`. `401` and `403` carry no body, as in "Responses", so neither carries an example.
- `info`: title, description (purpose, versioning, how enum values evolve, timestamps are UTC ISO 8601), contact, licence (OGL / repo licence).
- `servers`: local `http://localhost:8080`, plus CDP environment URLs as placeholders to confirm with the platform team.

### Standards (from the Swagger design-first guidance)

- Every operation, schema and property has a `description`; every operation has an `operationId`, `summary` and tag.
- Required fields are listed explicitly; `additionalProperties: false` on request objects, so typos in field names are rejected rather than ignored.
- Formats (`uuid`, `date-time`) and patterns are given, and every enum is documented.
- Every documented response has at least one example.
- No breaking change after `2.0.0` without a new version; adding enum values and optional fields counts as non-breaking. The `info.description` says so.

### Validation

Run locally with Node (no repo dependency added):

```bash
npx @redocly/cli@latest lint openapi/organisations-v2.yaml           # OpenAPI 3.1 structural + recommended rules
npx @stoplight/spectral-cli@latest lint openapi/organisations-v2.yaml # optional second opinion (spectral:oas ruleset)
npx @redocly/cli@latest preview-docs openapi/organisations-v2.yaml   # human review
```

Also check:

- each example validates against its schema (Redocly's `no-invalid-media-type-examples` rule);
- the `organisationType` enums together equal `OrganisationTypeCodes.All` (40 codes, same spelling), split 36 Companies House codes and 4 of ours, matching the comments in that file. For now this is a manual diff; "Follow-on work" describes an automated check;
- the `if`/`then` rule works: validate two throwaway payloads against `CreateOrganisationRequest` (for example with `npx ajv-cli`), checking that `ltd` without a number fails and `sole-trader` without one passes. The payloads are not added to the document, because an invalid request example would fail the lint;
- `homeNation` and the address usage types match `ck_organisation_home_nation` and the `address_usagetype` seed in `db.changelog-1.1.xml`.

Adding a lint step to `check-pull-request.yml` is optional and needs team agreement (it adds a Node step to a .NET pipeline).

### Supporting documentation

The contract says what the API accepts and returns. Consumers also need to know how to get access, how to use the API correctly and what will change. Wherever possible, that material goes inside the YAML (`info.description`, field `description`s, `examples`, `x-` extensions), so ReDoc renders it and there is one source that cannot drift. Material that is not about the HTTP interface, or that is not for every consumer, goes on the Confluence page "Access to org data via Org API" and is linked from `info.description`.

Required for the first release:

| # | Document | Content | Where |
|---|---|---|---|
| S1 | Getting started | How to get credentials, and who issues them (Basic, or a JWT with the `write` scope). The base URL for each CDP environment. The `Api-Version: 2` header, and the `400` returned without it. A first `curl` call for each auth scheme, returning `201` | `info.description` (Markdown section "Getting started") |
| S2 | Business rules and reference data | Mandatory fields (D2, D5). The Companies House number format, and when the number is required (D3, D6). The 40 `organisationType` codes with their meanings. Consumers own their English and Welsh labels and need a fallback for unknown codes. What each of the five address kinds is for. `isUkAddress` is derived from `country.iso`. Timestamps are UTC ISO 8601 | Schema and property `description`s; `x-enumDescriptions`; a "Rules" section in `info.description` |
| S3 | Error catalogue | Every `400` reason with the field path it names (for example `addresses.registeredAddress.postalCode`, `companiesHouseNumber`, `Api-Version`). What a `409` means, and how to use `existingOrganisationId` and `Location`. When to expect `401`, `403`, `415` and `500` | Response `description`s and examples; an "Errors" table in `info.description` |
| S4 | Versioning and change policy | Versions are selected by `Api-Version` (D1). Breaking versus non-breaking changes: new enum values and new optional response fields are non-breaking, so clients must tolerate unknown values. How long v1 and v2 run side by side. How deprecations are announced (OpenAPI `deprecated: true`, plus notice on Confluence and to known consumers) | `info.description` ("Versioning") |
| S5 | Contract changelog | One entry per contract version from `2.0.0-draft`: what changed and whether it is breaking | `openapi/CHANGELOG.md`, linked from `info.description` |

Expected for a Defra service:

| # | Document | Content | Where |
|---|---|---|---|
| S6 | v1 → v2 mapping | For consumers moving off v1: field-by-field mapping (for example `town` → `locality`, the Mongo address to the Defra address model), what v2 does not carry (registrations), and how RPD data maps to v2 (external ids pending Q8) | Confluence, linked |
| S7 | Data protection | Which personal data is held (sole traders' names and addresses are personal data), lawful basis, retention, and who may call the API. Input to the DPIA; check with the information assurance / DPO contact | Confluence (DPIA), not the public contract |
| S8 | Non-functional expectations | Availability and support hours, timeouts, rate limits (if any). Retry guidance: `POST` is not idempotent, so a retry after a timeout may return `409`; use `existingOrganisationId` rather than treating that as a failure (Q3) | `info.description` ("Using the API safely") + Confluence |
| S9 | Ownership and support | Owning team, how to raise an issue or request a change, where to see environment status | `info.contact` + Confluence |

Optional:

| # | Item | Content | Where |
|---|---|---|---|
| S10 | Mock server | Consumers build against the contract before the endpoint exists: `npx @stoplight/prism-cli@latest mock openapi/organisations-v2.yaml`. It returns the documented examples and validates requests against the schema | `README.md` and S1 |
| S11 | Sequence diagrams | Create → `201`; create → `409` → `GET` the existing organisation (when the v2 get exists); validation failure → `400` | Mermaid in Confluence or `info.description` |

### Work breakdown

| # | Task | Output | Est. |
|---|---|---|---|
| 1 | Record decisions D1–D6 on REGEN-141; agree the duplicate rule (Q2) | Answers recorded on REGEN-141 | 0.25 d |
| 2 | Create `openapi/organisations-v2.yaml`: `info`, `servers`, `security`, `tags`, the `Api-Version` parameter and header, the `POST /organisations` path | Skeleton | 0.25 d |
| 3 | Write the component schemas from "Organisation fields" and "Addresses", including the 40 `organisationType` codes copied from `OrganisationTypeCodes.All` | Schemas | 0.5 d |
| 4 | Add the responses and problem details schemas from "Responses" | Responses | 0.25 d |
| 5 | Add the request and response examples from "Components" | Examples | 0.25 d |
| 6 | Lint and validate (see "Validation"); fix findings | Clean lint | 0.25 d |
| 7 | Write the in-contract documentation S1–S4 in `info.description` and the descriptions, and create `openapi/CHANGELOG.md` (S5) | Documented contract | 0.5 d |
| 8 | Draft the Confluence material S6–S9; raise S7 with the DPO contact | Confluence sections | 0.5 d + review |
| 9 | Render with Redoc locally and review it with the team | Review notes | 0.25 d |
| 10 | Share with consumers (link in Jira / Confluence "Access to org data via Org API"), collect feedback, iterate, then bump to `2.0.0` | Signed-off contract | elapsed time |
| 11 | Short `README.md` section: where the contract lives, how to lint, preview and mock it (S10), and that it leads the implementation | Docs | 0.1 d |

## Acceptance Criteria

- [ ] `openapi/organisations-v2.yaml` exists, is OpenAPI 3.1.0, and describes `POST /organisations` with the required `Api-Version: 2` header.
- [ ] `registeredAddress` is required; `addressLine`, `locality` and `postalCode` are required on every address.
- [ ] `companiesHouseNumber` has a format pattern and is required (`if`/`then`, plus a description) when `organisationType` is one of the 36 Companies House codes.
- [ ] A duplicate is documented as `409 Conflict` with the existing organisation's id.
- [ ] Request and response fields match the PR #203 schema as mapped under "Organisation fields" and "Addresses"; no field exists that the schema cannot store.
- [ ] `homeNation`, `organisationType` (40 codes with descriptions) and the five address kinds are enumerated and documented.
- [ ] Security schemes, all response statuses under "Responses" and problem-details error bodies are documented.
- [ ] Every response has a valid example; `redocly lint` passes with no errors.
- [ ] Getting started, business rules, error catalogue and versioning policy (S1–S4) are in the contract, and `openapi/CHANGELOG.md` exists (S5).
- [ ] The v1 → v2 mapping, data protection, non-functional and support material (S6–S9) is on Confluence and linked from `info.description`.
- [ ] Decisions D1–D6 are recorded on REGEN-141, and the duplicate rule (Q2) is agreed before the endpoint is built.
- [ ] The contract has been reviewed by the team and shared with consumers.

## Out of Scope

- Implementing the endpoint, its persistence and validation.
- Get, update, search and delete operations for v2 (added to the same file later).
- Changes to the v1 API or its generated OpenAPI document (the `tests/Api.V1.Tests` snapshots must not change).
- Schema changes; any new column needs a new changeset (`db.changelog-1.2.xml`), not a contract-only field.

## Decisions

Agreed 21 October 2026:

| # | Decision | Where in the contract |
|---|---|---|
| D1 | The version is selected by a request header named `Api-Version`, not the path | `POST /organisations` with a required `Api-Version: 2` header ("Operation") |
| D2 | `addressLine`, `locality` and `postalCode` are mandatory on every address | `required` on `Address` ("Addresses") |
| D3 | `companiesHouseNumber` is format-checked | `pattern: ^[A-Z0-9]{2}[0-9]{6}$`, `400` on failure ("Organisation fields") |
| D4 | A duplicate returns `409 Conflict`, in the first release | `409` with `existingOrganisationId` ("Responses") |
| D5 | A registered address is required; the other four kinds are optional | `required: [registeredAddress]` on `Addresses` ("Addresses") |
| D6 | `companiesHouseNumber` is required when `organisationType` is a Companies House type | `if`/`then` on `CreateOrganisationRequest`, `400` on failure ("Organisation fields") |

## Open questions

| # | Question | Effect on the contract | Blocks? |
|---|---|---|---|
| Q2 | What counts as a duplicate (Companies House number? name + home nation + registered postal code?) | `409` description; endpoint logic; perhaps a unique index in a later changeset | Yes for the endpoint, not for drafting the contract |
| Q3 | Should a client be able to supply the `id` (idempotent `PUT /organisations/{id}` like v1) instead of the server generating it? | Operation shape | No: this spec assumes `POST` + server id |
| Q4 | Should `organisationType` itself be mandatory? While it is optional, an organisation sent without a type skips the Companies House number rule (D6) | `required` | No: this spec keeps it optional, as in the schema |
| Q5 | Field length limits: the database uses unbounded `text`. Should the contract set `maxLength` to protect the service? | `maxLength` | No |
| Q6 | What does `validFrom`/`validTo` on an organisation mean, and should a client set them on create? (schema spec open question 7) | `validFrom` writable? | No |
| Q7 | Should the response expose each address's history (`validFrom`/`validTo`) or only current addresses? | Response schema | No: current only |
| Q8 | Where do the RPD external ids (`externalOrganisationId`, `externalSubsidiaryId`) go? (schema spec open question 8) | New optional fields later (non-breaking) | No |

## Dependencies

- PR [#203](https://github.com/DEFRA/waste-organisations/pull/203) (REGEN-141 organisation and address schema): the contract fields are mapped from it.
- The duplicate rule (Q2) must be agreed before the endpoint is built, though not before the contract is drafted.
- The platform team, to confirm the CDP environment URLs for `servers`.
- The information assurance / DPO contact, for the data protection material (S7).

## Risks

| Risk | Mitigation |
|---|---|
| PR #203 changes before merge (it is still open) | Re-check "Organisation fields" and "Addresses" against the merged changelog before sign-off |
| The contract and the implementation drift | Follow-on contract test (see "Follow-on work") |
| Consumers hard-code labels or break on a new `organisationType` code | Contract states codes are open-ended and asks for a fallback label |
| `409` is promised in the first release before the duplicate rule is agreed | Q2 is a blocker for the endpoint; the contract states the rule once it is agreed |
| Header versioning is easy to miss, so clients call without the header | Required parameter, a clear `400` naming the header, and an explanation in `info.description` |
| The Companies House number pattern rejects a valid number | The pattern covers every 2-character prefix plus 6 digits; check it against real RPD data before sign-off |

## Notes

### Follow-on work (when the endpoint is built under REGEN-141)

- Implement `POST /organisations` (v2) to the contract, with validation messages that name the field. Route on the `Api-Version` header so v1 requests (no header) are untouched; `Api.V1.Tests` must still pass unchanged.
- Implement the agreed duplicate rule (Q2) behind the `409`.
- Add an `Api.Tests` test that loads `openapi/organisations-v2.yaml` and the app's generated document, and checks that the v2 paths, schemas and the `organisationType` enum match. Once that test exists, the hand-written contract and the code can't drift. Alternatively, retire the hand-written file and publish the generated one once they match.
- Expose the v2 document in ReDoc next to `/openapi/v1.json`.

### Assumptions recorded during planning

- The source of the 40 `organisationType` codes is `src/Api/Data/Postgres/OrganisationTypeCodes.cs` on branch `REGEN-141-organisation-and-address-db-schema` until PR #203 merges into `main`. Read the file with `git show origin/REGEN-141-organisation-and-address-db-schema:src/Api/Data/Postgres/OrganisationTypeCodes.cs`.
- The CDP environment URLs are not yet confirmed. `servers` uses one templated entry, `https://waste-organisations.{environment}.cdp-int.defra.cloud`, with an `environment` variable whose `enum` is `dev`, `test`, `perf-test` and `prod`. The server `description` states that the platform team must confirm the URLs.
- S6 to S9 are Confluence pages. An agent cannot create them. The contract carries a "More information" list in `info.description` that names each page by title and links the Confluence page "Access to org data via Org API". Writing the pages stays an acceptance criterion of this spec.
- Recording D1 to D6 on REGEN-141, agreeing Q2, the team review and the consumer sign-off are human tasks. They stay acceptance criteria of this spec and are not plan items.
- The `openapi/` folder holds no lint configuration file. The lint command uses the Redocly `recommended` ruleset through `--extends recommended`.
- The request and response schemas are separate, so the address schemas are separate too. `Address` and `Addresses` are the request schemas. `AddressResponse` and `AddressesResponse` are the response schemas. `AddressResponse` adds the read-only `isUkAddress`.

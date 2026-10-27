# REGEN-167: Implement Header-Based API Versioning

| | |
|---|---|
| Type | Feature: a new request contract (the version in the `Accept` header) and a new `406` response that API consumers can see. No change for current clients that send no version |
| Priority | P1 (High): the v2 endpoints under REGEN-125 (starting with REGEN-141 Create an Organisation) need a version to be released under without breaking v1 clients |
| Parent | REGEN-125 Access to org data via Org API |
| Risk | Low to medium (see Risks) |
| Source | Jira story REGEN-167 (reporter Ogonna Iwunze), the Companies House versioning guide it names as the reference pattern, the gap review and refinement answers of 26 and 27 October 2026, and the "Gap review outcome: agreed acceptance criteria" comment added to REGEN-167 on 27 October 2026 |

## Problem/Opportunity

The API has one shape today: the five v1 endpoints that `Api.V1.Tests` pins (REGEN-145). The v2 work under REGEN-125 will add response fields and make some request fields mandatory. Without a way for a client to say which version it wants, every such change breaks existing clients.

Path versioning (`/v2/organisations`) was considered and rejected. The client selects the version through a request header instead, following the Companies House pattern of content negotiation on the `Accept` header.

Who benefits:

- **Existing API consumers:** they keep getting v1 without changing anything.
- **New consumers and the team building v2:** there is one agreed, documented way to ask for a newer version, and one predictable error when an endpoint cannot serve it.

The strategy is not final. There are open concerns about the team deciding it on its own, so the implementation is kept minimal and easy to change or remove.

### The reference pattern (Companies House)

The Companies House guide versions resources through `Accept: application/vnd.companieshouse.<resource>+json; version=<n>`, echoes the version in the response `Content-Type`, serves the latest version by default, and returns `406` for an invalid version. This story follows the `Accept` header mechanism and the `406`, and deliberately differs in three places:

| Topic | Companies House | This API |
|---|---|---|
| Version scope | Each resource is versioned on its own | One version for the whole API |
| Default when none is sent | Latest version | v1, permanently |
| Version in the response `Content-Type` | Echoed | Not echoed: responses stay `application/json` |

## Proposed Solution

Add a small, reusable version check that runs after authentication and authorisation, before the endpoint handler. Each versioned endpoint declares the versions it supports; the check reads the requested version from the `Accept` header and returns `406` when the endpoint does not support it. Handlers, services and the data layer never see the version.

### The request contract

- Media type: `application/vnd.defra.waste-organisations+json`, with a `version` parameter. Example: `Accept: application/vnd.defra.waste-organisations+json; version=2`.
- One version for the whole API. The version decides the response shape and which requests are valid. For example, v2 may add response fields, or make fields mandatory on creation, that v1 does not have.
- `version` is a positive whole number: `1`, `2`, and so on.

### How the requested version is resolved

| `Accept` header | Requested version |
|---|---|
| Absent | 1 |
| `application/json` or `*/*` | 1 |
| Any media type other than ours, for example `text/html` | 1 |
| `application/vnd.defra.waste-organisations+json` with no `version` parameter | 1 |
| `application/vnd.defra.waste-organisations+json; version=1` | 1 |
| `application/vnd.defra.waste-organisations+json; version=2` | 2 |
| Our media type with `version=abc`, `version=0`, `version=-1`, `version=1.1` or `version=` | Malformed: `406` |
| Our media type listed more than once with different versions | Ambiguous: `406` |

Only our own media type with a `version` parameter can move a request off v1. A client that sends any other `Accept` value keeps getting v1, so no existing client can start receiving `406`.

### Responses

- A supported version: the endpoint behaves exactly as it does today. Responses keep `Content-Type: application/json` (or `application/problem+json` for errors), and the version is not echoed back.
- An unsupported, malformed or ambiguous version: `406 Not Acceptable` with a problem details body, written through the existing `IProblemDetailsService` like every other error from this API:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.7",
  "title": "Not Acceptable",
  "status": 406,
  "detail": "Unsupported API version"
}
```

The exact `type` and `title` are whatever the framework's problem details defaults give for `406`; `status` and `detail` are fixed by this story.

### Order of checks

1. Authentication and authorisation, as today. An unauthenticated or unauthorised request gets `401` or `403` whatever version it asks for.
2. The version check.
3. Route and body validation, then the handler, as today.

### Which endpoints are checked

| Endpoints | Supported versions | Version check |
|---|---|---|
| `GET /organisations`, `GET /organisations/{id}`, `PUT /organisations/{id}`, `PUT` and `DELETE /organisations/{id}/registrations/{type}-{registrationYear}` | 1 | Yes. A v2 request returns `406`; this is expected |
| `/health`, `/health/*`, `/openapi/*`, `/redoc` | n/a | No. They ignore the `Accept` version entirely |

When v2 is released, every endpoint must declare v2 support explicitly, including endpoints whose behaviour does not change. Otherwise a client sending `version=2` gets `406` from them.

### Implementation outline

- **Declaration:** an endpoint's supported versions are endpoint metadata, added by one extension method next to the existing `.WithName(...)`, `.Produces(...)` calls, for example `.WithApiVersions(1)`. Applying it once to a route group in `MapApiEndpoints` (`app.MapGroup("")`, which changes no paths) gives all five existing endpoints v1 without touching each file. Health, OpenAPI and ReDoc are mapped outside that group, so they have no metadata and are not checked.
- **Check:** one reusable endpoint filter (or middleware placed after `UseAuthorization`) that reads the metadata, resolves the requested version from `Accept` with `MediaTypeHeaderValue`, and short-circuits with the `406` problem when the version is not in the endpoint's list. Endpoint filters run after authorisation, which gives the required order without extra wiring.
- **Reuse:** use what ASP.NET Core already provides: endpoint metadata, `IEndpointFilter`, `MediaTypeHeaderValue` parsing and `IProblemDetailsService`. No new NuGet package. In particular, do not add `Asp.Versioning.Http`: it is heavier than this story needs and would add its own conventions to the OpenAPI document.
- **Lower layers:** the version is not passed to services, mapping or data access. When v2 arrives, the version decides which endpoint handler runs; anything below the handler stays version-agnostic.
- **Documentation:** the supported versions are visible in the code, at each endpoint's declaration. The filter adds no OpenAPI metadata, so the `406` response does not appear in `/openapi/v1.json` and the document is unchanged.

### Tests

| Project | Test |
|---|---|
| `Api.Tests` (new, for example `Endpoints/Versioning/`) | Unit tests for resolving the version from `Accept`, one case per row of the resolution table above |
| `Api.Tests` | In-process endpoint tests against an existing endpoint, for example `GET /organisations/{id}` with valid credentials: no `Accept` header, `application/json`, `*/*`, `text/html`, our type without `version`, and `version=1` all behave as today; `version=2` returns `406` with the problem details body (Verify snapshot); `version=abc`, `version=0` and `version=1.1` return `406`; two conflicting versions return `406` |
| `Api.Tests` | A request with no credentials and `version=2` returns `401`, not `406` |
| `Api.Tests` | `/health` and `/openapi/v1.json` with `version=2` return `200` |
| `Api.Tests` | The existing `OpenApiTests.OpenApi_VerifyAsExpected` snapshot passes unchanged |
| `Api.V1.Tests` | No change. Every test and snapshot passes as it is, including X-CONTRACT-01 (the v1 OpenAPI document) |

### Supporting changes

| File | Change |
|---|---|
| `README.md` | A short "API versioning" section: the media type, the default, the `406`, and how to declare versions on a new endpoint |
| `.github/dependabot.yml` | No change. No package is added |

### Work breakdown

| Slice | Work | Verified by |
|---|---|---|
| 1 | Version resolution from `Accept`, with unit tests | `Api.Tests` passes |
| 2 | Version metadata, the extension method, the endpoint filter, the `406` problem, the route group in `MapApiEndpoints`, endpoint tests | `Api.Tests` passes, including both OpenAPI snapshots unchanged |
| 3 | README section | Review |
| 4 | Run the full suite | `Api.Tests`, `Api.IntegrationTests` and then `Api.V1.Tests` pass against `docker compose up --build -d`, with no v1 test or snapshot edited |

## Acceptance Criteria

- [ ] The API version is sent in the `Accept` header as `application/vnd.defra.waste-organisations+json; version=<n>`, not in the URL path.
- [ ] A request with no `Accept` header, `application/json`, `*/*`, any other media type, our media type without `version`, or `version=1` is served v1, exactly as today.
- [ ] Responses keep `Content-Type: application/json`, and the version is not echoed back.
- [ ] Authentication and authorisation run before the version check: an unauthenticated request returns `401` whatever version it asks for.
- [ ] A version the endpoint does not support, a malformed version (`version=abc`, `version=0`, `version=1.1`) or conflicting versions return `406 Not Acceptable` with a problem details body whose `detail` is `Unsupported API version`.
- [ ] All five existing endpoints support v1 only, so a v2 request to any of them returns `406`.
- [ ] `/health`, `/health/*`, `/openapi/*` and `/redoc` skip the version check.
- [ ] Supported versions are declared in code at each endpoint through one reusable extension method, and the check lives in one reusable component. No endpoint handler parses the header, and no service, mapping or data class receives the version.
- [ ] The `406` response does not appear in the v1 OpenAPI document: the OpenAPI snapshots in `Api.Tests` and `Api.V1.Tests` (X-CONTRACT-01) are unchanged.
- [ ] No NuGet package is added.
- [ ] Every existing test passes, with no v1 test or snapshot edited, and the new tests listed under Tests pass locally and in `check-pull-request.yml`.
- [ ] `dotnet csharpier check .` and a warnings-as-errors build pass.

## Out of Scope

- Building any v2 endpoint or v2 behaviour.
- The Swagger/OpenAPI layout per version (for example `/openapi/v2.json`) and showing the `Accept` version in the generated document.
- Deprecation, sunset or expiry of versions (Companies House's `410 Gone` and `CH-Expiry-Date`).
- Echoing the version in the response `Content-Type`. To be decided when v2 is built.
- Governance sign-off of the versioning strategy.
- Ticket fields: estimate, assignee and team.
- Changes to whatever sits in front of the API: it is assumed to pass the `Accept` header through unchanged.

## Decisions

| Topic | Decision |
|---|---|
| Work type | Feature: consumers see a new header contract and a new error |
| Where the version goes | The `Accept` header, following the Companies House pattern. Path versioning was rejected |
| Version scope | One version for the whole API, not per resource |
| Media type | `application/vnd.defra.waste-organisations+json; version=<n>` |
| Default | v1 when no version is sent, permanently, including after a v2 exists |
| Status for an unsupported version | `406 Not Acceptable`, as Companies House returns, rather than the `400` in the original story |
| Error body | Problem details with `detail: "Unsupported API version"`, consistent with every other error from this API, rather than a bare string |
| Malformed or conflicting versions | `406`, the same as an unsupported version |
| Unrecognised `Accept` values | Served v1, so no existing client can start receiving `406` |
| Order | Authentication, then authorisation, then the version check |
| Existing endpoints | Support v1 only and are checked. A v2 request to them returns `406`, and this is expected. Every endpoint must declare v2 explicitly when v2 is released |
| Exempt routes | Health, OpenAPI and ReDoc |
| Response `Content-Type` | Unchanged; the version is not echoed |
| OpenAPI | The `406` is kept out of the v1 document, so the v1 contract snapshot does not change |
| Library | Reuse ASP.NET Core's endpoint metadata, filters and problem details. No new package |

## Open Questions

| Question | Owner |
|---|---|
| The technical notes in REGEN-167 say "may" ("version headers may be parsed by reusable middleware", "version information may not be passed into the application lower layers"). This spec reads the first as a permission and the second as "must not". To be confirmed | Ogonna Iwunze |

## Dependencies

- None. Nothing has to land before this story. REGEN-141 and the other v2 stories depend on it.

## Risks

| Risk | Level | Mitigation |
|---|---|---|
| The strategy changes after the governance concerns are raised | Medium | Everything lives in one extension method, one filter and one route group. Removing it is a small change, and handlers never see the version |
| A client already sends an unusual `Accept` header and starts getting `406` | Low | Only our own media type with a `version` parameter can trigger the check; any other `Accept` value is served v1 |
| The version check changes the OpenAPI document and breaks X-CONTRACT-01 | Low | The filter adds no OpenAPI metadata. Both OpenAPI snapshot tests must pass unchanged |
| The route group changes the endpoints' metadata order or names in the OpenAPI document | Low | `MapGroup("")` adds no prefix or tags; the snapshot tests catch any change. If they do change, declare the versions on each endpoint instead |
| When v2 is released, an unchanged endpoint is not given v2 and v2 clients get `406` from it | Medium | Recorded in the Decisions and in the README section. A test in the v2 story can assert that every API endpoint declares the highest version |
| Something in front of the API strips or rewrites `Accept` | Low | Assumed not to. A stripped header falls back to v1, so v1 clients are unaffected |

## Notes

- Branch name: `feature/REGEN-167`.
- `RequireAuthorization` is enforced by `UseAuthorization` before endpoint filters run, so a filter gets the "auth first" order for free. Middleware would have to be registered after `app.UseAuthorization()` to get the same order.
- `MediaTypeHeaderValue.TryParseList` handles several `Accept` values, `q` parameters and whitespace. Compare the media type and parameter name ignoring case.
- `MapOpenApi()` and `UseReDoc()` sit outside `MapApiEndpoints`, and `MapHealth()` maps its own endpoints, so none of them pick up the version metadata.
- Local commands, as in `AGENTS.md`: build and run `Api.Tests`; then `docker compose up --build -d`, run `Api.IntegrationTests`, then `Api.V1.Tests` on its own, then `docker compose down -v --remove-orphans`.

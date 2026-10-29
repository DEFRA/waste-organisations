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

Add a small, reusable version check that runs after authentication and authorisation, before the endpoint handler. By default it checks every endpoint, except health, OpenAPI and ReDoc. An endpoint supports v1 unless it declares other versions; the check reads the requested version from the `Accept` header and returns `406` when the endpoint does not support it. Handlers, services and the data layer never see the version.

v1 and v2 run side by side on the same URLs until v1 is retired, so a path may have one handler per version. A routing policy picks the handler for the requested version before authentication runs; the check then only confirms that the chosen endpoint supports the version. A v1 request only ever sees v1 endpoints, so the v1 API, including its `404` and `405` answers, does not change when v2 endpoints are added.

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

By default, every endpoint is checked. An endpoint does not opt in, and an endpoint added later is checked without anyone remembering to wire it up. Only these paths are exempt:

| Exempt path | Why |
|---|---|
| `/health` and `/health/*` | Platform probes, which send no version |
| `/openapi/*` | The generated OpenAPI documents |
| `/redoc` and `/redoc/*` | The ReDoc page and its assets |

| Endpoints | Supported versions | Version check |
|---|---|---|
| `GET /organisations`, `GET /organisations/{id}`, `PUT /organisations/{id}`, `PUT` and `DELETE /organisations/{id}/registrations/{type}-{registrationYear}` | 1 (the default; nothing is declared on them) | Yes. A v2 request returns `406`; this is expected |
| Any endpoint added later | 1, unless it declares other versions | Yes |
| The exempt paths above | n/a | No. They ignore the `Accept` version entirely |

A request that matches no endpoint is not checked, so an unknown path still returns `404` whatever version it asks for. Nor is routing's own `405`: a method no endpoint on the path answers returns `405` whatever version it asks for, listing the methods the requested version has.

When v2 is released, every endpoint must declare v2 support explicitly, including endpoints whose behaviour does not change. Otherwise a client sending `version=2` gets `406` from them.

### Two versions on one URL

Endpoint routing chooses the endpoint before any middleware after routing runs, so a middleware alone cannot pick between two handlers on the same path and method: without help, routing reports `AmbiguousMatchException` for every request to that path, v1 requests included. The version is therefore applied inside routing, by a `MatcherPolicy` that splits the routing tree by the version resolved from `Accept`, before the HTTP method is matched. It applies only at paths where an endpoint declares versions, so nothing changes until the first one does.

| Endpoints on the path | Request | Result |
|---|---|---|
| `GET` v1 and `GET` v2 | No header, or `version=1` | The v1 handler |
| `GET` v1 and `GET` v2 | `version=2` | The v2 handler |
| `GET` v1 and `GET` v2 | `version=3`, or malformed | The v1 handler is chosen, then `406` from the middleware |
| `PUT` declared `(1, 2)` | Any of v1, v2 | The one handler |
| `GET` v1 only and `POST` v2 only | `POST`, no header | `405` with `Allow: GET`, as today: v1 does not see the v2 `POST` |
| `GET` v1 only and `POST` v2 only | `POST`, `version=2` | The v2 handler |
| `GET` v1 only and `POST` v2 only | `GET`, `version=2` | `406`: v2 falls back to the v1 `GET`, which does not support v2 |
| `GET /x/search` v1 only beside `GET /x/{id}` v1 and v2 | `GET /x/search`, `version=2` | `406`: the literal route is v1-only and keeps its precedence over `{id}`, as in v1. The v2 `{id}` handler never sees `search` |
| `GET` v1 and `PUT` v1 and v2 | `DELETE`, any version | `405` with `Allow: GET, PUT`: a method no version answers stays `405` |
| `GET` v2 only (a new v2 path) | No header | `404`: the path does not exist for v1 |
| `GET` v2 only (a new v2 path) | `version=3` | `406` |
| Nothing declared | `version=2` | `406`, as before |

The rule behind the table: the v1 branch holds exactly the endpoints that support v1. Every other version's branch holds the endpoints that support it, plus the v1 endpoints for the HTTP methods none of them answers, so an unsupported version still reaches an endpoint and gets its `406` after authentication rather than a `404` or `405`. Unknown and malformed versions take that same route. Fallbacks are decided route by route, so a v2 `/x/{id}` never stands in for a v1-only `/x/search`: the literal keeps its precedence and a v2 request to it gets `406`.

### Implementation outline

- **Routing:** one `MatcherPolicy` implementing `INodeBuilderPolicy`, registered as a singleton by the same `AddApiVersioning()` call. It orders itself before ASP.NET Core's HTTP method policy, so the method check and its `405` happen inside the requested version's branch. Its jump table resolves the version with the same resolver the middleware uses, and sends unknown and malformed versions down the fallback branch. It builds no OpenAPI metadata.
- **Check:** one reusable middleware, registered in `Program.cs` straight after `app.UseAuthorization()` and before `app.UseReDoc(...)`. It skips the exempt paths and requests that matched no endpoint. Otherwise it resolves the requested version from `Accept` with `MediaTypeHeaderValue`, and short-circuits with the `406` problem when the version is not one the matched endpoint supports. Because it runs after `UseAuthorization`, an unauthenticated or unauthorised request is rejected before the version is looked at.
- **Default:** an endpoint with no version metadata supports v1 only. None of the five existing endpoints needs changing.
- **Declaration:** an endpoint that supports other versions says so with endpoint metadata, added by one extension method next to the existing `.WithName(...)`, `.Produces(...)` calls, for example `.WithApiVersions(1, 2)`. Nothing in this story calls it; it is there for the first v2 endpoint, and covered by the routing tests in `Api.Tests`, which map endpoints in every shape the v2 stories need.
- **Exempt paths:** kept in one list next to the middleware, compared as path prefixes ignoring case: `/health`, `/openapi`, `/redoc`.
- **Reuse:** use what ASP.NET Core already provides: endpoint metadata, middleware, `MediaTypeHeaderValue` parsing and `IProblemDetailsService`. No new NuGet package. In particular, do not add `Asp.Versioning.Http`: it is heavier than this story needs and would add its own conventions to the OpenAPI document.
- **Lower layers:** the version must not be passed to services, mapping or data access. When v2 arrives, the version decides which endpoint handler runs; anything below the handler stays version-agnostic.
- **Documentation:** the default and the exempt paths are visible in the middleware, and any other supported versions at each endpoint's declaration. The middleware adds no OpenAPI metadata, so the `406` response does not appear in `/openapi/v1.json` and the document is unchanged.

### Tests

| Project | Test |
|---|---|
| `Api.Tests` (new, for example `Endpoints/Versioning/`) | Unit tests for resolving the version from `Accept`, one case per row of the resolution table above |
| `Api.Tests` | In-process endpoint tests against an existing endpoint, for example `GET /organisations/{id}` with valid credentials: no `Accept` header, `application/json`, `*/*`, `text/html`, our type without `version`, and `version=1` all behave as today; `version=2` returns `406` with the problem details body (Verify snapshot); `version=abc`, `version=0` and `version=1.1` return `406`; two conflicting versions return `406` |
| `Api.Tests` | A request with no credentials and `version=2` returns `401`, not `406` |
| `Api.Tests` | `/health`, `/openapi/v1.json` and `/redoc` return `200` whatever version is requested, a malformed one included |
| `Api.Tests` | An unknown path with `version=2` returns `404`, not `406` |
| `Api.Tests` (`Versioning/ApiVersionMatcherPolicyRoutingTests`) | A small app routed through the real matcher, the policy and the middleware, with endpoints mapped in every shape the v2 stories need: one path with a `GET` per version, a `PUT` declared `(1, 2)`, a path where v2 adds a `POST` v1 lacks, a v1-only literal beside a parameter route with a v2, a v2-only path and a path that declares nothing. One case per row of the "Two versions on one URL" table, plus the `Allow` header of each version's `405` |
| `Api.Tests` (`Versioning/ApiVersionMatcherPolicyTests`) | The policy's order is before the HTTP method policy; it applies only where a version is declared; the branches it builds for one path (v1 strict, other versions with fallbacks, the unknown-version branch); the branch its jump table picks for each kind of `Accept` value |
| `Api.Tests` (`Versioning/ApiVersionsMetadataTests`) | Declaring no version, or a version below 1, throws; the versions an endpoint supports are the declared ones, or v1 only |
| `Api.Tests` | The `406` keeps the `Strict-Transport-Security` header, because it is written directly rather than through the exception handler (v1 X-HDR-01) |
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
| 2 | The middleware with its exempt paths and v1 default, the `406` problem, the version metadata and its extension method, endpoint tests | `Api.Tests` passes, including both OpenAPI snapshots unchanged |
| 3 | README section | Review |
| 4 | Run the full suite | `Api.Tests`, `Api.IntegrationTests` and then `Api.V1.Tests` pass against `docker compose up --build -d`, with no v1 test or snapshot edited |

## Acceptance Criteria

- [ ] The API version is sent in the `Accept` header as `application/vnd.defra.waste-organisations+json; version=<n>`, not in the URL path.
- [ ] A request with no `Accept` header, `application/json`, `*/*`, any other media type, our media type without `version`, or `version=1` is served v1, exactly as today.
- [ ] Responses keep `Content-Type: application/json`, and the version is not echoed back.
- [ ] Authentication and authorisation run before the version check: an unauthenticated request returns `401` whatever version it asks for.
- [ ] A version the endpoint does not support, a malformed version (`version=abc`, `version=0`, `version=1.1`) or conflicting versions return `406 Not Acceptable` with a problem details body whose `detail` is `Unsupported API version`.
- [ ] All five existing endpoints support v1 only, so a v2 request to any of them returns `406`.
- [ ] Every endpoint is checked by default, including any added later, except `/health`, `/health/*`, `/openapi/*`, `/redoc` and `/redoc/*`. A path that matches no endpoint still returns `404`, and a method no endpoint answers still returns `405`, whatever version is asked for.
- [ ] An endpoint with no declared versions supports v1 only; other versions are declared in code at the endpoint through one reusable extension method. The version is selected in one routing policy and checked in one reusable middleware. No endpoint handler parses the header, and no service, mapping or data class receives the version.
- [ ] Two endpoints may be mapped on the same path and HTTP method for different versions; the requested version selects the handler, and a v2 request to a path that has only a v1 handler returns `406`.
- [ ] A v1 request never reaches a v2-only endpoint: a v2-only path returns `404` to it, and a v2-only method on an existing path returns `405` with the v1 `Allow` list, so every v1 route test (X-ROUTE-01, X-ROUTE-02) passes unchanged with v2 endpoints mapped.
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
| Which endpoints are checked | All of them by default, so a new endpoint cannot be missed. An endpoint with no declared versions supports v1 only |
| Existing endpoints | Support v1 only, by the default, and are checked. A v2 request to them returns `406`, and this is expected. Every endpoint must declare v2 explicitly when v2 is released |
| Exempt paths | `/health`, `/health/*`, `/openapi/*`, `/redoc`, `/redoc/*`. A request matching no endpoint is not checked either, so it keeps its `404` |
| Routing's own `405` | Not versioned. Only route endpoints (`RouteEndpoint`, what `MapGet` and friends create) carry a version; the endpoint routing sets for a method nobody answers is a plain `Endpoint` and passes through, so the answer is `405` whatever the version, with the `Allow` list of the requested version |
| Where the version is decided | Twice, for two different jobs. A routing policy selects the endpoint for the requested version, because routing runs before any middleware and is the only place two handlers on one path can be told apart. The middleware after `UseAuthorization` then checks the chosen endpoint and writes the `406`, so authentication still comes first. The REGEN-167 technical notes allow reusable middleware for parsing the header |
| Two handlers on one URL | Allowed, and the normal way to add a v2 to an existing operation: map the v2 handler on the same path and method with `.WithApiVersions(2)`, and leave the v1 handler untouched (the ring-fencing decision of 22 October 2026) |
| v1 is strict | The v1 branch holds only endpoints that support v1. A v1 client gets `404` for a v2-only path and `405` for a v2-only method, exactly as it does today, rather than `406`, so the v1 API never changes shape |
| Unknown or malformed versions on a split path | Routed down the fallback branch (the v1 endpoints plus the v2-only methods), so they reach an endpoint and get `406` after authentication, like an unsupported version on any other path |
| Declared versions | Start at 1, like the versions the resolver accepts. `WithApiVersions(0)` throws an `ArgumentException` instead of colliding with the policy's branch for unknown versions |
| Lower layers | The version must not be passed into the application's lower layers (REGEN-167 technical notes, as corrected on 27 October 2026) |
| Response `Content-Type` | Unchanged; the version is not echoed |
| OpenAPI | The `406` is kept out of the v1 document, so the v1 contract snapshot does not change |
| Library | Reuse ASP.NET Core's endpoint metadata, middleware and problem details. No new package |
| Our media type without `version` next to our media type with a version | The entry without `version` counts as version 1. With `version=2` beside it the request is ambiguous and gets `406`; with `version=1` beside it the request is v1. Assumed during planning as the reading most consistent with the resolution table |
| Writing the `406` | Through `TypedResults.Problem(...).ExecuteAsync(...)`, for consistency with the other error paths. It goes through `IProblemDetailsService` when a writer accepts the request (on .NET 10 the default writer accepts our `+json` media type) and writes the same `application/problem+json` body itself otherwise. Written in the middleware rather than thrown into the exception handler, so the response keeps its HSTS header |
| Matching exempt paths | By whole path segment, ignoring case (`PathString.StartsWithSegments`), so `/health` and `/health/all` are exempt but a future `/healthy` would not be |

## Open Questions

None. The wording of the REGEN-167 technical notes is settled: "version headers may be parsed by reusable middleware" and "version information must not be passed into the application lower layers" (see Decisions).

## Dependencies

- None. Nothing has to land before this story. REGEN-141 and the other v2 stories depend on it.

## Risks

| Risk | Level | Mitigation |
|---|---|---|
| The strategy changes after the governance concerns are raised | Medium | Everything lives in one routing policy, one middleware and one extension method. Removing it is a small change, and handlers never see the version |
| A client already sends an unusual `Accept` header and starts getting `406` | Low | Only our own media type with a `version` parameter can trigger the check; any other `Accept` value is served v1 |
| The version check changes the OpenAPI document and breaks X-CONTRACT-01 | Low | The middleware adds no OpenAPI metadata and no endpoint changes. Both OpenAPI snapshot tests must pass unchanged |
| A new non-API path (for example another documentation or diagnostics route) is added and starts returning `406` to clients that send a version | Low | Clients without our media type are served v1, so they are unaffected. Add the path to the exempt list if needed |
| When v2 is released, an unchanged endpoint is not given v2 and v2 clients get `406` from it | Medium | Recorded in the Decisions and in the README section. A test in the v2 story can assert that every API endpoint declares the highest version |
| Something in front of the API strips or rewrites `Accept` | Low | Assumed not to. A stripped header falls back to v1, so v1 clients are unaffected |

## Notes

- Branch name: `feature/REGEN-167`.
- `RequireAuthorization` is enforced by `UseAuthorization`, and routing has already picked the endpoint by then, so middleware registered straight after it gets the "auth first" order and can read the endpoint's metadata through `HttpContext.GetEndpoint()`.
- `UseReDoc()` is middleware, not an endpoint, and is registered after `UseAuthorization`. That is why `/redoc` and `/redoc/*` must be exempted by path rather than by a "no endpoint matched" rule alone.
- `MediaTypeHeaderValue.TryParseList` handles several `Accept` values, `q` parameters and whitespace. Compare the media type and parameter name ignoring case.
- The routing policy implements `INodeBuilderPolicy`, which routing uses while building its decision tree. ASP.NET Core falls back to `IEndpointSelectorPolicy` only when an endpoint is dynamic (`IDynamicEndpointMetadata`); this API has none, so the selector form is not implemented. Its `Order` is `-1100`, below `HttpMethodMatcherPolicy` (`-1000`), because lower orders are applied first.
- Local commands, as in `AGENTS.md`: build and run `Api.Tests`; then `docker compose up --build -d`, run `Api.IntegrationTests`, then `Api.V1.Tests` on its own, then `docker compose down -v --remove-orphans`.

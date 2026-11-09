<!-- Example output of the spec-clarify skill. Synthetic story: REGEN-0000 is not a real
     ticket and the feature is illustrative. Kept short to show the shape; real specs are longer. -->

# REGEN-0000: Echo the request id on v2 responses

| | |
|---|---|
| Type | Improvement: every v2 response carries an `X-Request-Id` header with the request's trace id, so clients can quote it when they report a problem. No change for v1 clients |
| Priority | P2 (Medium): asked for by Manage Account support; nothing blocks on it |
| Parent | REGEN-0001 Operability of the v2 API |
| Risk | Low (see Risks) |
| Source | Jira story REGEN-0000 and its comment of 3 November 2026 (header name agreed); refinement answers of 9 November 2026 recorded in Decisions |

## Problem/Opportunity

When a v2 call fails, the client has nothing to quote to the team. Problem details already carry a `traceId`, but success responses carry nothing, and the id is only in the body. The API already propagates the CDP trace header (`x-cdp-request-id`, `TraceHeader` options, `src/Api/Utils/Logging`) into its logs, so the id exists; it is not returned.

Who benefits: Manage Account support, who can ask a client for the header value and find the request in the CDP logs in one search.

## Proposed Solution

A response header `X-Request-Id` on every v2 response, set from the incoming `x-cdp-request-id` when present, otherwise from the generated trace id the logging enrichment already uses. Implemented as a small middleware registered after `UseApiVersioning()` in `Program.cs`, applied only to requests that resolved to version 2, so v1 responses are unchanged.

### Code layout

- `src/Api/Utils/Logging/RequestIdHeaderMiddleware.cs` (new), registered through a `UseRequestIdHeader()` extension beside `UseHstsUnconditionally`.
- No DTO, service, schema or OpenAPI document changes; the header is documented in the v2 OpenAPI document's description text.

## Acceptance Criteria

- [ ] A v2 request with `x-cdp-request-id: abc` gets `X-Request-Id: abc` on a `2xx` response (Jira Scenario 1).
- [ ] A v2 request with `x-cdp-request-id: abc` that fails validation gets `X-Request-Id: abc` on the `400` problem details response, and the body's `traceId` matches (Jira Scenario 2).
- [ ] A v2 request without `x-cdp-request-id` gets an `X-Request-Id` equal to the trace id written to the log for that request.
- [ ] A v1 request, with or without `x-cdp-request-id`, gets no `X-Request-Id` header; no v1 test or snapshot changes.
- [ ] `401` and `403` responses carry the header when the request asked for version 2.
- [ ] `Api.Tests` covers the four cases above in process; `Api.IntegrationTests` checks one success and one `400` against Docker Compose.
- [ ] No NuGet package is added; `dotnet csharpier check .` and a warnings-as-errors build pass.

## Out of Scope

- Returning the header on v1 responses (the v1 contract is frozen).
- Changing the trace header name or the logging format.
- Exposing the header in the OpenAPI schema as a typed response header for every operation (text in the document description is enough for this story).

## Decisions

| Topic | Decision |
|---|---|
| Header name | `X-Request-Id`, not `x-cdp-request-id`: clients should not learn the platform's header name (comment of 3 November 2026) |
| v2 only | Applied only when the request resolved to version 2 (9 November 2026), so no v1 snapshot changes and the v1 tests stay untouched |
| Source of the id | The incoming `x-cdp-request-id` when present, otherwise the trace id the Serilog enrichment generates, so the header and the log agree (9 November 2026) |

## Open Questions

- None.

## Dependencies

- REGEN-167 (spec 0004) for the version resolution the middleware reads. Delivered.

## Risks

- Low. The middleware touches every v2 response; a bug would show in `Api.Tests` immediately. The header is additive and ignored by clients that do not know it.

## Notes

- Problem details already include `traceId`; this story does not change that body.

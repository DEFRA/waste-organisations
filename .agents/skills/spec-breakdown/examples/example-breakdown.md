<!-- Example output of the spec-breakdown skill. Synthetic story: REGEN-0000 is not a real
     ticket and the feature is illustrative. Shows the summary and the shape of the child specs. -->

# Breakdown of specs/0007-REGEN-0000-record-organisation-contacts.md

## Why it is too big

The parent adds a `contact` table and entity, a v2 `POST /organisations/{id}/contacts` endpoint
with validation and a service, the v2 OpenAPI changes and README text. Estimated non-generated
change: about 1,400 lines across changelog, entities, DTOs, services, endpoints, OpenAPI
transformers and tests, which is over the size guide and mixes a schema change with behaviour.

## Strategy

Schema first, then behaviour (the pattern of specs 0003 and 0005): the entities change shape, so
the changelog and entities land first and the endpoint slice builds on a merged schema. The
documentation slice is separate because it changes the v2 OpenAPI document and its snapshots.

## Slices

| # | Child spec | Includes | Parent criteria | Verified by | Depends on | Size |
|---|---|---|---|---|---|---|
| 1 | `specs/0008-REGEN-0000-contact-schema.md` | `db.changelog-1.3.xml` with `contact` and `ux_contact_values`, EF entity, `PostgresDbContext` mapping, schema tests | AC 6, AC 7 | `docker compose up --build -d` applies `v1.3.0`; `Api.Tests`, `Api.IntegrationTests` pass | none | ~350 lines |
| 2 | `specs/0009-REGEN-0000-create-contact-endpoint.md` | DTOs, validator and endpoint filter, service, `POST /organisations/{id}/contacts` with `.WithApiVersions(2)`, mapping, unit and integration tests | AC 1 to AC 5, AC 8 | `Api.Tests`, `Api.IntegrationTests` pass; no v1 test changes | 1 | ~650 lines |
| 3 | `specs/0010-REGEN-0000-contact-openapi-and-readme.md` | v2 OpenAPI document and schema transformer for the new operation, ReDoc check, README "API v2" paragraph, snapshot updates | AC 9, AC 10 | `Api.Tests` passes with only the v2 snapshots changed; `Api.V1.Tests` pass | 2 | ~200 lines |

Every one of the parent's ten acceptance criteria lands in exactly one slice.

## Dependency order

```
0008 contact schema
└── 0009 create contact endpoint
    └── 0010 OpenAPI and README
```

Nothing runs in parallel: each slice needs the previous one merged. Start with 0008.

## Shape of a child spec (0009, abridged)

```markdown
# REGEN-0000: Create a contact through v2 POST /organisations/{id}/contacts

| | |
|---|---|
| Type | Part 2 of 3 of REGEN-0000 (Feature): the endpoint, validation and service on the schema from spec 0008. No change for v1 clients |
| Priority | P1 (High): inherited from the parent |
| Parent | specs/0007-REGEN-0000-record-organisation-contacts.md |
| Risk | Medium (see Risks) |
| Source | The parent spec; refinement answers of 9 November 2026 recorded in its Decisions |

## Problem/Opportunity
(two sentences and a link to the parent's section)

## Proposed Solution
(request, response, errors, validation, persistence, code layout for this slice only)

## Acceptance Criteria
- [ ] AC 1 to AC 5 and AC 8 of the parent, reworded only where the slice boundary requires

## Out of Scope
- The schema (spec 0008) and the OpenAPI document and README (spec 0010)

## Decisions
| Topic | Decision |
|---|---|
| Split | Endpoint separated from schema and documentation (9 November 2026); see the parent's Decisions for everything else |

## Open Questions / Dependencies / Risks / Notes
(as in every spec)
```

## Parent update

The parent's "### Work breakdown" table now lists the three child specs in order with what each
is verified by, so the parent remains the single place that says how REGEN-0000 is delivered.

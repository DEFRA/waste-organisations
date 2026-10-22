# Contract changelog

Changes to `organisations-v2.yaml`. Each contract version has one entry, which states what changed and whether the change is breaking for consumers.

New enum values and new optional response fields are non-breaking. A removed field, a new mandatory request field, a narrowed type and a changed status code are breaking. See the "Versioning" section of `info.description` for the full policy.

## 2.0.0-draft

Non-breaking: this is the first draft of the v2 contract, so no published version precedes it.

First draft content:

- `POST /organisations` as `createOrganisation`, with the mandatory `Api-Version: 2` header and `basicAuth` or `bearerAuth` security.
- `CreateOrganisationRequest`, which requires `name`, `homeNation` and `addresses`, and requires `companiesHouseNumber` when `organisationType` is a Companies House code.
- `Organisation`, `Addresses` and `AddressesResponse`, with the five address kinds and the derived `isUkAddress`.
- `Address`, `AddressResponse` and `Country`, which require `addressLine`, `locality` and `postalCode`.
- `HomeNation`, `CompaniesHouseNumber`, and `OrganisationType` as the union of `CompaniesHouseOrganisationType` and `OtherOrganisationType`.
- The responses `201`, `400`, `401`, `403`, `409`, `415` and `500`, with `ProblemDetails`, `ValidationProblemDetails` and `DuplicateOrganisationProblem` error bodies.
- Fourteen named examples, which cover the request body and every response that carries a body.
- The "Getting started", "Rules", "Errors", "Versioning" and "Using the API safely" sections in `info.description`.

namespace Defra.WasteOrganisations.Api.Data.Postgres;

/// <summary>
/// One organisation type: its code, what it means, and whether Companies House registers it.
/// </summary>
/// <param name="Code">The value sent as <c>organisationType</c>, in upper snake case.</param>
/// <param name="Description">What the code means.</param>
/// <param name="RegisteredAtCompaniesHouse">
/// True for the types Companies House registers. The type-dependent request rules (the name, the
/// Companies House number and the required address kind) read this one flag rather than lists of
/// codes, so adding a type is one entry here and one changeset.
/// </param>
public sealed record OrganisationTypeCode(string Code, string Description, bool RegisteredAtCompaniesHouse);

/// <summary>
/// Every value <c>organisationType</c> may take, with its meaning: the source of truth for the
/// codes the v2 API publishes, and for the rows seeded into <c>organisation_type</c> by the
/// changelog (an integration test fails if the two differ). Consumers own their own labels,
/// English and Welsh; these descriptions say what a code means, not how to display it.
/// </summary>
/// <remarks>
/// The ten types come from the Confluence page "Organisation Types and Subtypes" (W&amp;CE Manage
/// Account space, updated 4 September 2026), adopted as the reference on 3 November 2026 in place
/// of the Companies House <c>company_type</c> values (spec 0005). The v2 API is not yet published,
/// so this list could still change; once it is published, a code is only ever added, here and in
/// a changeset, never renamed or removed.
/// </remarks>
public static class OrganisationTypeCodes
{
    public static readonly IReadOnlyList<OrganisationTypeCode> All =
    [
        // Registered with Companies House
        new("LTD", "Private limited company", true),
        new("PLC", "Public limited company", true),
        new("LIMITED_PARTNERSHIP", "Limited partnership", true),
        new("LLP", "Limited liability partnership", true),
        new("CIC", "Community interest company", true),
        // Registered elsewhere (HMRC, the FCA, the Charity Commission) or not in the UK
        new("SOLE_TRADER", "Sole trader", false),
        new("PARTNERSHIP", "General partnership", false),
        new("REGISTERED_SOCIETY", "Co-operative or community benefit society", false),
        new("CHARITABLE_ORGANISATION", "Charitable incorporated organisation or charitable trust", false),
        new("OVERSEAS_ORGANISATION", "Overseas organisation not registered in the UK", false),
    ];
}

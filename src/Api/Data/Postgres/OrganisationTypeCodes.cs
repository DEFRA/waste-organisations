namespace Defra.WasteOrganisations.Api.Data.Postgres;

public sealed record OrganisationTypeCode(string Code, string Description);

/// <summary>
/// Every value <c>organisationType</c> may take, with its meaning: the source of truth for the
/// codes the v2 API publishes, and for the rows seeded into <c>organisation_type</c> by the
/// changelog (an integration test fails if the two differ). Consumers own their own labels,
/// English and Welsh; these descriptions say what a code means, not how to display it.
/// </summary>
/// <remarks>
/// The Companies House <c>company_type</c> values and descriptions come from
/// <c>companieshouse/api-enumerations</c> <c>constants.yml</c> (commit 98b9594, 10 September 2026),
/// legacy values included. A published code is never renamed or removed; a new one is added
/// here and in a changeset.
/// </remarks>
public static class OrganisationTypeCodes
{
    public static readonly IReadOnlyList<OrganisationTypeCode> All =
    [
        // Companies House company_type
        new("private-unlimited", "Private unlimited company"),
        new("ltd", "Private limited company"),
        new("plc", "Public limited company"),
        new("old-public-company", "Old public company"),
        new(
            "private-limited-guarant-nsc-limited-exemption",
            "Private Limited Company by guarantee without share capital, use of 'Limited' exemption"
        ),
        new("limited-partnership", "Limited partnership"),
        new("private-limited-guarant-nsc", "Private limited by guarantee without share capital"),
        new("converted-or-closed", "Converted / closed"),
        new("private-unlimited-nsc", "Private unlimited company without share capital"),
        new("private-limited-shares-section-30-exemption", "Private Limited Company, use of 'Limited' exemption"),
        new("protected-cell-company", "Protected cell company"),
        new("assurance-company", "Assurance company"),
        new("oversea-company", "Overseas company"),
        new("eeig-establishment", "European Economic Interest Grouping Establishment (EEIG)"),
        // Companies House describes all four investment company codes the same way; the
        // suffixes tell them apart
        new("icvc-securities", "Investment company with variable capital (securities)"),
        new("icvc-warrant", "Investment company with variable capital (warrant)"),
        new("icvc-umbrella", "Investment company with variable capital (umbrella)"),
        new("registered-society-non-jurisdictional", "Registered society"),
        new("industrial-and-provident-society", "Industrial and Provident society"),
        new("northern-ireland", "Northern Ireland company"),
        new("northern-ireland-other", "Credit union (Northern Ireland)"),
        new("llp", "Limited liability partnership"),
        new("royal-charter", "Royal charter company"),
        new("investment-company-with-variable-capital", "Investment company with variable capital"),
        new("unregistered-company", "Unregistered company"),
        new("other", "Other company type"),
        new("european-public-limited-liability-company-se", "European public limited liability company (SE)"),
        new("united-kingdom-societas", "United Kingdom Societas"),
        new("uk-establishment", "UK establishment company"),
        new("scottish-partnership", "Scottish qualifying partnership"),
        new("charitable-incorporated-organisation", "Charitable incorporated organisation"),
        new("scottish-charitable-incorporated-organisation", "Scottish charitable incorporated organisation"),
        new(
            "further-education-or-sixth-form-college-corporation",
            "Further education or sixth form college corporation"
        ),
        new("eeig", "European Economic Interest Grouping (EEIG)"),
        new("ukeig", "United Kingdom Economic Interest Grouping"),
        new("registered-overseas-entity", "Overseas entity"),
        // Ours: legal forms Companies House does not register
        new("sole-trader", "Sole trader"),
        new("partnership", "Partnership not registered with Companies House"),
        new("unincorporated-association", "Unincorporated association"),
        new("public-body", "Public body"),
    ];
}

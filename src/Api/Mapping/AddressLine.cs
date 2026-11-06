using Defra.WasteOrganisations.Api.Data.Postgres.Entities;

namespace Defra.WasteOrganisations.Api.Mapping;

/// <summary>
/// Builds the one-line form of an address that the v2 API returns as <c>addressLine</c>. It is
/// computed on output, never stored.
/// </summary>
public static class AddressLine
{
    /// <summary>
    /// Joins the parts in postal order: <c>subBuildingName</c>, <c>buildingName</c>, then
    /// <c>buildingNumber</c> and <c>street</c> joined by one space, then <c>poBox</c>,
    /// <c>locality</c>, <c>region</c> and <c>postalCode</c>, skipping blank parts and joining the
    /// rest with <c>", "</c>. The country is left out, as in the Remapping table on the Confluence
    /// page "Companies House - Address Cleansing", but that table's own order is not used: it puts
    /// <c>region</c> before <c>locality</c> and has no <c>poBox</c>, so a PO box address with no
    /// other premises would lose the part that identifies it.
    /// </summary>
    public static string Build(Address address)
    {
        var numberAndStreet = string.Join(' ', NonBlank(address.BuildingNumber, address.Street));

        return string.Join(
            ", ",
            NonBlank(
                address.SubBuildingName,
                address.BuildingName,
                numberAndStreet,
                address.PoBox,
                address.Locality,
                address.Region,
                address.PostalCode
            )
        );
    }

    private static IEnumerable<string> NonBlank(params string?[] parts) =>
        parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim());
}

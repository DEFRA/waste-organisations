namespace Defra.WasteOrganisations.Testing;

// ux_organisation_companies_house_number allows one organisation per number, so every integration test
// or theory case that stores an organisation with a number takes its own constant (spec 0008, Decisions)
public static class CompaniesHouseNumbers
{
    public const string FullPayload = "12345678";

    public const string Organisation_WithTypeAndTwoAddresses_RoundTrips = "10000001";

    public const string SecondCurrentRegisteredAddress_IsRejectedThenAcceptedWhenFirstClosed = "10000002";

    public const string OrganisationAddress_WithUnknownUsageType_IsRejectedByForeignKey = "10000003";

    public const string DeletingOrganisationWithAddresses_IsRejectedByRestrict = "10000004";

    public const string OrganisationAddressValidToBeforeValidFrom_IsRejectedByCheckConstraint = "10000005";

    public const string Organisation_WithUnknownOrganisationType_IsRejectedByForeignKey = "10000006";

    public const string Organisation_WithRegulatorNationOutsideTheUkNations_IsRejectedByCheckConstraint = "10000007";

    public const string TwoOrganisations_MayLinkTheSameAddressRow_First = "10000008";

    public const string TwoOrganisations_MayLinkTheSameAddressRow_Second = "10000009";

    public const string Organisation_WithoutName_IsRejectedByCheckConstraintUnlessTradingNameAndNoNumber_NullName =
        "10000010";

    public const string Organisation_WithoutName_IsRejectedByCheckConstraintUnlessTradingNameAndNoNumber_BlankName =
        "10000011";
}

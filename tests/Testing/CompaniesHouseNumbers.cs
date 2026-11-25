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

    public const string CreateNameAndTradingName = "10000012";

    public const string CreateTypeLtd = "10000013";

    public const string CreateTypePlc = "10000014";

    public const string CreateTypeLimitedPartnership = "10000015";

    public const string CreateTypeLlp = "10000016";

    public const string CreateTypeCic = "10000017";

    public const string GetCreated = "10000018";

    public const string GetIdUpperCase = "10000019";

    public const string GetIdWithoutHyphens = "10000020";

    public const string GetEndedLink = "10000021";

    public const string GetEveryLinkEnded = "10000022";

    public const string GetKindRelinked = "10000023";

    public const string GetKindsDifferentValidFrom = "10000024";

    public const string GetWithValidTo = "10000025";

    public const string ErrorCreatedNotInV1 = "10000026";
}

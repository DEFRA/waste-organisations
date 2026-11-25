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

    public const string ServiceRetryFirst = "10000027";

    public const string ServiceRetrySecond = "10000028";

    public const string ServiceLinkFails = "10000029";

    public const string ServiceRetryFailsStored = "10000030";

    public const string ServiceRetryFailsSecond = "10000031";

    public const string CreateVariantUpperCaseFirst = "10000032";

    public const string CreateVariantUpperCaseSecond = "10000033";

    public const string CreateVariantPostcodeFirst = "10000034";

    public const string CreateVariantPostcodeSecond = "10000035";

    public const string CreateVariantAddressLine2First = "10000036";

    public const string CreateVariantAddressLine2Second = "10000037";

    public const string CreateVariantCountyFirst = "10000038";

    public const string CreateVariantCountySecond = "10000039";

    public const string CreateVariantTownFirst = "10000040";

    public const string CreateVariantTownSecond = "10000041";

    public const string CreateVariantCountryFirst = "10000042";

    public const string CreateVariantCountrySecond = "10000043";

    public const string CreateFiveKinds = "10000044";

    public const string CreateFiveAddresses = "10000045";

    public const string CreateTrimmedAndUpperCased = "SC000046";

    public const string CreateServerSetMembers = "10000047";

    public const string CreatePunctuatedPostcode = "10000048";

    public const string CreateNonUkPostcode = "10000049";

    public const string CreateSameAddressTwoItems = "10000050";

    public const string CreateSharedAddressFirst = "10000051";

    public const string CreateSharedAddressSecond = "10000052";

    public const string CreateAbsentParts = "10000053";

    public const string CreateBlankParts = "10000054";
}

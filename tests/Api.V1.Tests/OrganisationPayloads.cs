namespace Defra.WasteOrganisations.Api.V1.Tests;

/// <summary>
/// Fixed organisation PUT bodies, written as raw JSON so that a change to the API's own types
/// cannot change what these tests send. Values are fixed so that snapshots stay stable.
/// </summary>
public static class OrganisationPayloads
{
    /// <summary>
    /// Every field set, with one registration. Pass <c>null</c> as <paramref name="businessCountry"/>
    /// to send an explicit JSON <c>null</c>.
    /// </summary>
    public static string Full(
        string type = "SMALL_PRODUCER",
        int registrationYear = 2025,
        string status = "REGISTERED",
        string? businessCountry = "GB-ENG"
    ) =>
        $$"""
            {
              "name": "V1 Test Organisation Ltd",
              "tradingName": "V1 Trading Name",
              "businessCountry": {{JsonString(businessCountry)}},
              "companiesHouseNumber": "12345678",
              "address": {
                "addressLine1": "1 Test Street",
                "addressLine2": "Test Estate",
                "town": "Test Town",
                "county": "Test County",
                "postcode": "TE1 1ST",
                "country": "United Kingdom"
              },
              "registration": {
                "status": "{{status}}",
                "type": "{{type}}",
                "registrationYear": {{registrationYear}}
              }
            }
            """;

    /// <summary>Only the required fields: <c>name</c>, an empty <c>address</c> and one registration.</summary>
    public static string Minimal(
        string type = "SMALL_PRODUCER",
        int registrationYear = 2025,
        string status = "REGISTERED"
    ) =>
        $$"""
            {
              "name": "V1 Minimal Organisation Ltd",
              "address": {},
              "registration": {
                "status": "{{status}}",
                "type": "{{type}}",
                "registrationYear": {{registrationYear}}
              }
            }
            """;

    private static string JsonString(string? value) => value is null ? "null" : $"\"{value}\"";
}

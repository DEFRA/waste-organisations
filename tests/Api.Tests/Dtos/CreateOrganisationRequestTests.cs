using System.Text.Json;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Dtos;

namespace Defra.WasteOrganisations.Api.Tests.Dtos;

// A missing member must bind as null so that validation can report it under its JSON path; with
// the C# `required` modifier, as v1 uses, it would instead make the whole body unreadable
public class CreateOrganisationRequestTests
{
    private static readonly JsonSerializerOptions s_options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Deserialize_WithoutOrganisation_ShouldBindNull()
    {
        var request = JsonSerializer.Deserialize<CreateOrganisationRequest>("{}", s_options);

        request.Should().NotBeNull();
        request.Organisation.Should().BeNull();
    }

    [Fact]
    public void Deserialize_AddressWithoutStreet_ShouldBindNull()
    {
        const string json = """
            {
              "organisation": {
                "regulatorNation": "GB-XXX",
                "organisationType": "LTD",
                "addresses": [
                  { "addressTypes": ["REGISTERED", "SERVICE_OF_NOTICE"], "buildingNumber": "12", "locality": "Testville", "countryIso": "GB" }
                ]
              }
            }
            """;

        var request = JsonSerializer.Deserialize<CreateOrganisationRequest>(json, s_options);

        var organisation = request!.Organisation!;
        organisation.Name.Should().BeNull();
        organisation.RegulatorNation.Should().Be("GB-XXX");
        var address = organisation.Addresses.Should().ContainSingle().Subject;
        address.Street.Should().BeNull();
        address.AddressTypes.Should().Equal(AddressType.Registered, AddressType.ServiceOfNotice);
    }

    [Fact]
    public void Deserialize_WithUnknownAddressType_ShouldThrow()
    {
        const string json = """{ "organisation": { "addresses": [ { "addressTypes": ["HOME"] } ] } }""";

        var act = () => JsonSerializer.Deserialize<CreateOrganisationRequest>(json, s_options);

        act.Should().Throw<JsonException>();
    }

    // Server-set and dropped members are ignored, so adding an optional request field later is not
    // a breaking change for clients that are ahead of the server
    [Fact]
    public void Deserialize_WithUnknownMembers_ShouldIgnoreThem()
    {
        const string json = """
            { "organisation": { "id": "x", "mainActivitySicCode": "1", "addresses": [ { "addressLine": "x", "countryName": "x" } ] } }
            """;

        var act = () => JsonSerializer.Deserialize<CreateOrganisationRequest>(json, s_options);

        act.Should().NotThrow();
    }
}

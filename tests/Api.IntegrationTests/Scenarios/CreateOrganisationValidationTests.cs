using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

// The v2 create's validation 400 against the Compose stack: one case per rule, each changing a
// valid request in one way and pinning the problem details. The keys are asserted exactly, so a
// case also proves no other rule fires: an unknown or missing type, for example, must not bring
// the type-dependent rules with it. Validation runs before the handler, so nothing is stored
public class CreateOrganisationValidationTests : IntegrationTestBase
{
    private sealed record ValidationCase(Func<JsonObject> Body, string[] ExpectedKeys);

    private static readonly Dictionary<string, ValidationCase> s_cases = new()
    {
        ["NoOrganisation"] = new(() => new JsonObject(), ["organisation"]),
        ["NameMissingOnLtd"] = new(() => Ltd(o => o.Remove("name")), ["organisation.name"]),
        ["TradingNameMissingOnSoleTrader"] = new(
            () => SoleTrader(o => o.Remove("tradingName")),
            ["organisation.tradingName"]
        ),
        ["NameBlank"] = new(() => Ltd(o => o["name"] = "   "), ["organisation.name"]),
        ["RegulatorNationMissing"] = new(() => Ltd(o => o.Remove("regulatorNation")), ["organisation.regulatorNation"]),
        ["RegulatorNationUnknown"] = new(
            () => Ltd(o => o["regulatorNation"] = "GB-XXX"),
            ["organisation.regulatorNation"]
        ),
        // Without a type the name, number and address kind rules cannot apply, so a body that
        // would break the LTD ones still reports the type alone
        ["OrganisationTypeMissing"] = new(
            () =>
                Ltd(o =>
                {
                    o.Remove("organisationType");
                    o.Remove("name");
                    o.Remove("companiesHouseNumber");
                }),
            ["organisation.organisationType"]
        ),
        ["OrganisationTypeUnknown"] = new(
            () =>
                Ltd(o =>
                {
                    o["organisationType"] = "UNKNOWN";
                    o.Remove("name");
                    o.Remove("companiesHouseNumber");
                }),
            ["organisation.organisationType"]
        ),
        ["NumberMissingOnLtd"] = new(
            () => Ltd(o => o.Remove("companiesHouseNumber")),
            ["organisation.companiesHouseNumber"]
        ),
        ["NumberOnSoleTrader"] = new(
            () => SoleTrader(o => o["companiesHouseNumber"] = "12345678"),
            ["organisation.companiesHouseNumber"]
        ),
        ["NumberTooShort"] = new(
            () => Ltd(o => o["companiesHouseNumber"] = "1234567"),
            ["organisation.companiesHouseNumber"]
        ),
        ["NumberWithLetterAtEnd"] = new(
            () => Ltd(o => o["companiesHouseNumber"] = "IP12345R"),
            ["organisation.companiesHouseNumber"]
        ),
        ["LtdWithPrincipalOnly"] = new(
            () => Ltd(o => o["addresses"] = Addresses(Address("PRINCIPAL"))),
            ["organisation.addresses"]
        ),
        ["SoleTraderWithRegisteredOnly"] = new(
            () => SoleTrader(o => o["addresses"] = Addresses(Address("REGISTERED"))),
            ["organisation.addresses"]
        ),
        ["AddressesMissing"] = new(() => Ltd(o => o.Remove("addresses")), ["organisation.addresses"]),
        ["AddressesEmpty"] = new(() => Ltd(o => o["addresses"] = Addresses()), ["organisation.addresses"]),
        // There are five kinds and every item needs one, so a sixth item always repeats a kind too
        ["SixAddresses"] = new(
            () =>
                Ltd(o =>
                    o["addresses"] = Addresses(
                        Address("REGISTERED"),
                        Address("PRINCIPAL"),
                        Address("AUDIT"),
                        Address("SERVICE_OF_NOTICE"),
                        Address("CORRESPONDENCE"),
                        Address("CORRESPONDENCE")
                    )
                ),
            ["organisation.addresses", "organisation.addresses[5].addressTypes"]
        ),
        ["AddressTypesMissing"] = new(
            () => WithSecondAddress(Without(Address(), "addressTypes")),
            ["organisation.addresses[1].addressTypes"]
        ),
        ["AddressTypesEmpty"] = new(() => WithSecondAddress(Address()), ["organisation.addresses[1].addressTypes"]),
        ["RegisteredTwiceInOneItem"] = new(
            () => Ltd(o => o["addresses"] = Addresses(Address("REGISTERED", "REGISTERED"))),
            ["organisation.addresses[0].addressTypes"]
        ),
        // Reported under the later item, naming the item that already carries the kind
        ["RegisteredInTwoItems"] = new(
            () => WithSecondAddress(Address("REGISTERED", "CORRESPONDENCE")),
            ["organisation.addresses[1].addressTypes"]
        ),
        ["StreetMissing"] = new(
            () => WithSecondAddress(Without(Address("CORRESPONDENCE"), "street")),
            ["organisation.addresses[1].street"]
        ),
        ["LocalityMissing"] = new(
            () => WithSecondAddress(Without(Address("CORRESPONDENCE"), "locality")),
            ["organisation.addresses[1].locality"]
        ),
        ["CountryIsoMissing"] = new(
            () => WithSecondAddress(Without(Address("CORRESPONDENCE"), "countryIso")),
            ["organisation.addresses[1].countryIso"]
        ),
        ["CountryIsoThreeLetters"] = new(
            () => WithSecondAddress(Change(Address("CORRESPONDENCE"), "countryIso", "GBR")),
            ["organisation.addresses[1].countryIso"]
        ),
        ["GbWithoutPostalCode"] = new(
            () => WithSecondAddress(Without(Address("CORRESPONDENCE"), "postalCode")),
            ["organisation.addresses[1].postalCode"]
        ),
        ["GbWithBlankPostalCode"] = new(
            () => WithSecondAddress(Change(Address("CORRESPONDENCE"), "postalCode", " ")),
            ["organisation.addresses[1].postalCode"]
        ),
        // No single member is at fault, so the error is keyed by the item itself
        ["NoPremises"] = new(
            () => WithSecondAddress(Without(Address("CORRESPONDENCE"), "buildingNumber")),
            ["organisation.addresses[1]"]
        ),
        // One failure does not hide another, at any depth of the request
        ["SeveralFailures"] = new(
            () =>
                SoleTrader(o =>
                {
                    o.Remove("tradingName");
                    o["regulatorNation"] = "GB-XXX";
                    o["companiesHouseNumber"] = "12345678";
                    o["addresses"] = Addresses(
                        Without(Address("PRINCIPAL"), "street"),
                        Without(Address("CORRESPONDENCE"), "postalCode")
                    );
                }),
            [
                "organisation.tradingName",
                "organisation.regulatorNation",
                "organisation.companiesHouseNumber",
                "organisation.addresses[0].street",
                "organisation.addresses[1].postalCode",
            ]
        ),
    };

    public static TheoryData<string> Cases => new(s_cases.Keys);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task InvalidRequest_ShouldBeValidationProblem(string scenario)
    {
        var validationCase = s_cases[scenario];

        var response = await PostAsync(validationCase.Body());
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, content);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        JsonNode.Parse(content)!["errors"]!
            .AsObject()
            .Select(e => e.Key)
            .Should()
            .BeEquivalentTo(validationCase.ExpectedKeys);

        await VerifyJson(content).UseParameters(scenario);
    }

    // A valid LTD request with one REGISTERED address, changed by `change`
    private static JsonObject Ltd(Action<JsonObject>? change = null) =>
        Request(
            new JsonObject
            {
                ["name"] = "Acme Waste Ltd",
                ["companiesHouseNumber"] = "12345678",
                ["regulatorNation"] = "GB-ENG",
                ["organisationType"] = "LTD",
                ["addresses"] = Addresses(Address("REGISTERED")),
            },
            change
        );

    // A valid SOLE_TRADER request with one PRINCIPAL address, changed by `change`
    private static JsonObject SoleTrader(Action<JsonObject>? change = null) =>
        Request(
            new JsonObject
            {
                ["tradingName"] = "Acme Recycling",
                ["regulatorNation"] = "GB-WLS",
                ["organisationType"] = "SOLE_TRADER",
                ["addresses"] = Addresses(Address("PRINCIPAL")),
            },
            change
        );

    // A valid LTD request whose second address is the one under test, so the first keeps the
    // REGISTERED kind the type requires
    private static JsonObject WithSecondAddress(JsonObject address) =>
        Ltd(o => o["addresses"] = Addresses(Address("REGISTERED"), address));

    private static JsonObject Request(JsonObject organisation, Action<JsonObject>? change)
    {
        change?.Invoke(organisation);

        return new JsonObject { ["organisation"] = organisation };
    }

    private static JsonArray Addresses(params JsonNode[] addresses) => new(addresses);

    private static JsonObject Address(params string[] addressTypes) =>
        new()
        {
            ["addressTypes"] = new JsonArray(addressTypes.Select(t => (JsonNode?)JsonValue.Create(t)).ToArray()),
            ["buildingNumber"] = "12",
            ["street"] = "High Street",
            ["locality"] = "Testville",
            ["postalCode"] = "AB1 2CD",
            ["countryIso"] = "GB",
        };

    private static JsonObject Without(JsonObject node, string member)
    {
        node.Remove(member);

        return node;
    }

    private static JsonObject Change(JsonObject node, string member, string value)
    {
        node[member] = value;

        return node;
    }

    private static async Task<HttpResponseMessage> PostAsync(JsonObject body)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, Testing.Endpoints.Organisations.Post())
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.TryAddWithoutValidation("Accept", AcceptV2);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}

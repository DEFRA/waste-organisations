using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Dtos;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

// The v2 read against the Compose stack, of organisations this class creates through the v2 POST.
// Address rows are shared and never cleaned up, so every test sends address values unique to it
public class GetOrganisationDetailsTests : IntegrationTestBase
{
    private const string AcceptV2 = "application/vnd.defra.waste-organisations+json; version=2";

    // Following the Location of a create returns what the 201 returned, without the dates the read
    // leaves out
    [Fact]
    public async Task Created_ShouldBeReadableAtItsLocation()
    {
        var body = Ltd(Address(NewSuffix(), "REGISTERED", "PRINCIPAL"), Address(NewSuffix(), "CORRESPONDENCE"));
        var created = await CreateAsync(body);

        var response = await GetAsync(created.Location);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        var read = await ReadOrganisationAsync(response);
        read.Id.Should().Be(created.Organisation.Id);
        read.Name.Should().Be(created.Organisation.Name);
        read.CompaniesHouseNumber.Should().Be(created.Organisation.CompaniesHouseNumber);
        read.RegulatorNation.Should().Be(created.Organisation.RegulatorNation);
        read.OrganisationType.Should().Be(created.Organisation.OrganisationType);
        read.Addresses.Should().BeEquivalentTo(created.Organisation.Addresses);

        var organisation = JsonNode.Parse(content)!["organisations"]!.AsArray().Single()!.AsObject();
        ShouldHaveNone(organisation, "validFrom", "createdAt", "updatedAt");
        var addresses = organisation["addresses"]!.AsArray();
        addresses.Should().HaveCount(2);
        foreach (var address in addresses)
            ShouldHaveNone(address!.AsObject(), "validFrom", "validTo");
    }

    // Any form of the GUID the route accepts finds the organisation, which comes back with its id
    // in lower case with hyphens
    [Theory]
    [InlineData("upper case")]
    [InlineData("without hyphens")]
    public async Task IdInAnotherForm_ShouldBeOk(string form)
    {
        var created = await CreateAsync(Ltd(Address(NewSuffix(), "REGISTERED")));
        var id = created.Organisation.Id;
        var path = form switch
        {
            "upper case" => Testing.Endpoints.Organisations.Get(id.ToString().ToUpperInvariant()),
            "without hyphens" => Testing.Endpoints.Organisations.Get(id.ToString("N")),
            _ => throw new ArgumentOutOfRangeException(nameof(form), form, null),
        };

        var response = await GetAsync(path);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        JsonNode.Parse(content)!["organisations"]![0]!["id"]!.GetValue<string>().Should().Be(id.ToString("D"));
    }

    // Unique per test and per run, so a test never meets an address row another stored
    private static string NewSuffix() => Guid.NewGuid().ToString("N")[..12];

    private static object Address(string suffix, params string[] addressTypes) =>
        new
        {
            addressTypes,
            buildingNumber = "12",
            street = $"High Street {suffix}",
            locality = "Testville",
            postalCode = "AB1 2CD",
            countryIso = "GB",
        };

    private static object Ltd(params object[] addresses) =>
        new
        {
            organisation = new
            {
                name = "Acme Waste Ltd",
                companiesHouseNumber = "12345678",
                regulatorNation = "GB-ENG",
                organisationType = "LTD",
                addresses,
            },
        };

    private static async Task<HttpResponseMessage> PostAsync(object body)
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, Testing.Endpoints.Organisations.Post())
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.TryAddWithoutValidation("Accept", AcceptV2);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<(OrganisationResponse Organisation, string Location)> CreateAsync(object body)
    {
        var response = await PostAsync(body);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, content);

        var created = await response.Content.ReadFromJsonAsync<CreateOrganisationResponse>(
            TestContext.Current.CancellationToken
        );

        return (created!.Organisation, response.Headers.Location!.OriginalString);
    }

    private static async Task<HttpResponseMessage> GetAsync(
        string path,
        string? accept = AcceptV2,
        HttpClient? client = null
    )
    {
        using var defaultClient = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (accept is not null)
            request.Headers.TryAddWithoutValidation("Accept", accept);

        return await (client ?? defaultClient).SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<OrganisationDetailsResponse> ReadOrganisationAsync(HttpResponseMessage response) =>
        (
            await response.Content.ReadFromJsonAsync<GetOrganisationDetailsResponse>(
                TestContext.Current.CancellationToken
            )
        )!
            .Organisations.Should()
            .ContainSingle()
            .Subject;

    private static void ShouldHaveNone(JsonObject json, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
            json.ContainsKey(propertyName).Should().BeFalse(propertyName);
    }
}

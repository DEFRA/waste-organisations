using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Testing.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Organisation = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Organisation;
using StoredAddress = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Address;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

// The v2 read against the Compose stack, mostly of organisations this class creates through the v2 POST.
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
        created.Organisation.TradingName.Should().Be("Acme");
        read.Name.Should().Be(created.Organisation.Name);
        read.TradingName.Should().Be(created.Organisation.TradingName);
        read.CompaniesHouseNumber.Should().Be(created.Organisation.CompaniesHouseNumber);
        read.RegulatorNation.Should().Be(created.Organisation.RegulatorNation);
        read.OrganisationType.Should().Be(created.Organisation.OrganisationType);
        read.ValidTo.Should().Be(created.Organisation.ValidTo);
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

    // A link with a valid_to is history: its kind is no longer one of the organisation's addresses
    [Fact]
    public async Task EndedLink_ShouldNotBeReturned()
    {
        var registeredSuffix = NewSuffix();
        var created = await CreateAsync(
            Ltd(Address(registeredSuffix, "REGISTERED", "PRINCIPAL"), Address(NewSuffix(), "CORRESPONDENCE"))
        );
        var id = created.Organisation.Id;

        await ChangeStoredAsync(id, organisation => End(LinkOf(organisation, "CORRESPONDENCE")));

        var response = await GetAsync(created.Location);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var address = (await ReadOrganisationAsync(response)).Addresses.Should().ContainSingle().Subject;
        address.Street.Should().Be($"High Street {registeredSuffix}");
        address.AddressTypes.Should().BeEquivalentTo([AddressType.Registered, AddressType.Principal]);
    }

    [Fact]
    public async Task EveryLinkEnded_ShouldReturnEmptyAddresses()
    {
        var created = await CreateAsync(
            Ltd(Address(NewSuffix(), "REGISTERED", "PRINCIPAL"), Address(NewSuffix(), "CORRESPONDENCE"))
        );

        await ChangeStoredAsync(created.Organisation.Id, organisation => organisation.Addresses.ForEach(End));

        var response = await GetAsync(created.Location);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK, content);
        JsonNode.Parse(content)!["organisations"]![0]!["addresses"]!.AsArray().Should().BeEmpty();
    }

    // Two saves, since the database allows one current link per organisation and kind
    [Fact]
    public async Task KindRelinkedToAnotherAddress_ShouldReturnOnlyTheNewAddress()
    {
        var registeredSuffix = NewSuffix();
        var created = await CreateAsync(
            Ltd(Address(registeredSuffix, "REGISTERED"), Address(NewSuffix(), "CORRESPONDENCE"))
        );
        var id = created.Organisation.Id;
        var newSuffix = NewSuffix();

        await ChangeStoredAsync(id, organisation => End(LinkOf(organisation, "CORRESPONDENCE")));
        await ChangeStoredAsync(id, organisation =>
        {
            var ended = organisation.Addresses.Single(link => link.ValidTo is not null);
            organisation.Addresses.Add(
                new OrganisationAddress
                {
                    Id = Guid.NewGuid(),
                    UsageTypeId = ended.UsageTypeId,
                    Address = new StoredAddress
                    {
                        Id = Guid.NewGuid(),
                        BuildingNumber = "34",
                        Street = $"Low Road {newSuffix}",
                        Locality = "Testville",
                        PostalCode = "AB3 4CD",
                        CountryIso = "GB",
                    },
                }
            );
        });

        var response = await GetAsync(created.Location);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var addresses = (await ReadOrganisationAsync(response)).Addresses;
        addresses.Should().HaveCount(2);
        addresses
            .Should()
            .ContainSingle(address => address.AddressTypes.Contains(AddressType.Correspondence))
            .Which.Street.Should()
            .Be($"Low Road {newSuffix}");
        addresses
            .Should()
            .ContainSingle(address => address.AddressTypes.Contains(AddressType.Registered))
            .Which.Street.Should()
            .Be($"High Street {registeredSuffix}");
    }

    // The links' valid_from is not part of the response, so it never splits an address in two
    [Fact]
    public async Task KindsOfOneAddressWithDifferentValidFrom_ShouldBeOneItem()
    {
        var created = await CreateAsync(Ltd(Address(NewSuffix(), "REGISTERED", "PRINCIPAL")));

        await ChangeStoredAsync(created.Organisation.Id, organisation =>
        {
            var principal = LinkOf(organisation, "PRINCIPAL");
            principal.ValidFrom = principal.ValidFrom.AddDays(-1);
        });

        var response = await GetAsync(created.Location);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var address = (await ReadOrganisationAsync(response)).Addresses.Should().ContainSingle().Subject;
        address.AddressTypes.Should().BeEquivalentTo([AddressType.Registered, AddressType.Principal]);
    }

    // The read has no notion of an ended organisation: it returns the row with its validTo
    [Fact]
    public async Task OrganisationWithValidTo_ShouldStillBeReturned()
    {
        var created = await CreateAsync(Ltd(Address(NewSuffix(), "REGISTERED")));
        var validTo = default(DateTime);

        await ChangeStoredAsync(created.Organisation.Id, organisation =>
        {
            validTo = organisation.ValidFrom.AddDays(30);
            organisation.ValidTo = validTo;
        });

        var response = await GetAsync(created.Location);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var read = await ReadOrganisationAsync(response);
        read.Id.Should().Be(created.Organisation.Id);
        read.ValidTo.Should().Be(new DateTimeOffset(validTo));
        read.ValidTo.Value.Offset.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData("new")]
    [InlineData("empty")]
    public async Task UnknownId_ShouldBeNotFoundWithEmptyBody(string kind)
    {
        var id = kind switch
        {
            "new" => Guid.NewGuid(),
            "empty" => Guid.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        var response = await GetAsync(Testing.Endpoints.Organisations.Get(id));
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        content.Should().BeEmpty();
    }

    // The v1 PUT writes MongoDB only, and the v2 read looks in Postgres only
    [Fact]
    public async Task OrganisationCreatedThroughV1_ShouldBeNotFoundInV2()
    {
        var id = Guid.NewGuid();
        using var client = CreateClient();
        var put = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );
        put.StatusCode.Should().Be(HttpStatusCode.Created);
        var path = Testing.Endpoints.Organisations.Get(id);

        var v2 = await GetAsync(path);
        var v1 = await GetAsync(path, accept: null);

        v2.StatusCode.Should().Be(HttpStatusCode.NotFound);
        v1.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Empty bodies, as v1 (spec 0001, X-BODY-01)
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    // See compose.yml: an OAuth client with the write scope only
    [InlineData("WriteOnlyOAuth", HttpStatusCode.Forbidden)]
    public async Task WithoutReadAccess_ShouldBeRejectedWithEmptyBody(string? clientId, HttpStatusCode expected)
    {
        using var client = CreateClient(clientId);

        var response = await GetAsync(Testing.Endpoints.Organisations.Get(Guid.NewGuid()), client: client);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(expected);
        content.Should().BeEmpty();
    }

    [Fact]
    public async Task Version3Requested_ShouldBeNotAcceptable()
    {
        var response = await GetAsync(
            Testing.Endpoints.Organisations.Get(Guid.NewGuid()),
            "application/vnd.defra.waste-organisations+json; version=3"
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable, content);
        JsonNode.Parse(content)!["detail"]!.GetValue<string>().Should().Be("Unsupported API version");
    }

    // The {id:guid} constraint matches no endpoint, so routing answers before authentication
    [Theory]
    [InlineData("IntegrationTest")]
    [InlineData(null)]
    public async Task IdNotAGuid_ShouldBeNotFoundWithEmptyBody(string? clientId)
    {
        using var client = CreateClient(clientId);

        var response = await GetAsync(Testing.Endpoints.Organisations.Get("not-a-guid"), client: client);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        content.Should().BeEmpty();
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
                tradingName = "Acme",
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

    // Changes the stored rows directly, since no v2 endpoint changes an organisation yet
    private static async Task ChangeStoredAsync(Guid id, Action<Organisation> change)
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        var organisation = await context
            .Organisations.Include(o => o.Addresses)
                .ThenInclude(a => a.UsageType)
            .SingleAsync(o => o.Id == id, TestContext.Current.CancellationToken);
        change(organisation);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    // Just after the link started: the database requires valid_to to be later than valid_from
    private static void End(OrganisationAddress link) => link.ValidTo = link.ValidFrom.AddSeconds(1);

    private static OrganisationAddress LinkOf(Organisation organisation, string usageType) =>
        organisation.Addresses.Single(a => a.UsageType.Name == usageType);
}

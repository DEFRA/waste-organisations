using System.Net;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Defra.WasteOrganisations.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Address = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Address;
using Organisation = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Organisation;

namespace Defra.WasteOrganisations.Api.Tests.Endpoints.Organisations;

// The service is a substitute, so these tests need no Postgres; the stored rows and the current
// links filter are covered by Api.IntegrationTests
public class GetDetailsTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private const string MediaType = "application/vnd.defra.waste-organisations+json";
    private static readonly Guid s_id = new("0199c0f6-1f7a-7c8e-9f3a-5b2d8c4e7a10");
    private static readonly DateTime s_created = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private IPostgresOrganisationService MockPostgresOrganisationService { get; } =
        Substitute.For<IPostgresOrganisationService>();

    private IOrganisationService MockOrganisationService { get; } = Substitute.For<IOrganisationService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        services.AddTransient<IPostgresOrganisationService>(_ => MockPostgresOrganisationService);
        services.AddTransient<IOrganisationService>(_ => MockOrganisationService);
    }

    [Fact]
    public async Task WhenOrganisationFound_ShouldBeOk()
    {
        MockPostgresOrganisationService.GetAsync(s_id, Arg.Any<CancellationToken>()).Returns(StoredOrganisation());

        var response = await SendGet(s_id.ToString());
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await VerifyJson(content).DontScrubGuids().DontScrubDateTimes();
    }

    [Fact]
    public async Task WhenOrganisationNotFound_ShouldBeNotFoundWithEmptyBody()
    {
        MockPostgresOrganisationService.GetAsync(s_id, Arg.Any<CancellationToken>()).Returns((Organisation?)null);

        var response = await SendGet(s_id.ToString());
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        content.Should().BeEmpty();
        await MockPostgresOrganisationService.Received(1).GetAsync(s_id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenNoCredentials_ShouldBeUnauthorizedWithEmptyBody()
    {
        var response = await SendGet(s_id.ToString(), CreateClient(addAuthorizationHeader: false));
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        content.Should().BeEmpty();
        await ShouldNotHaveCalledThePostgresService();
    }

    [Fact]
    public async Task WhenWriteOnlyCredentials_ShouldBeForbiddenWithEmptyBody()
    {
        var response = await SendGet(s_id.ToString(), CreateClient(testUser: TestUser.WriteOnly));
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        content.Should().BeEmpty();
        await ShouldNotHaveCalledThePostgresService();
    }

    private async Task ShouldNotHaveCalledThePostgresService() =>
        await MockPostgresOrganisationService.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());

    // Every date member is set, so the snapshot shows that none of them reaches the response
    private static Organisation StoredOrganisation()
    {
        var organisationType = new OrganisationType { Id = Guid.NewGuid(), Code = "LTD" };
        var organisation = new Organisation
        {
            Id = s_id,
            Name = "Acme Waste Ltd",
            TradingName = "Acme",
            CompaniesHouseNumber = "12345678",
            RegulatorNation = "GB-ENG",
            OrganisationTypeId = organisationType.Id,
            OrganisationType = organisationType,
            ValidFrom = s_created,
            CreatedAt = s_created,
            UpdatedAt = s_created,
        };
        var millHouse = new Address
        {
            Id = Guid.NewGuid(),
            SubBuildingName = "Unit 4",
            BuildingName = "Mill House",
            BuildingNumber = "12A",
            Street = "High Street",
            Locality = "Testville",
            Region = "Testshire",
            PostalCode = "AB1 2CD",
            CountryIso = "GB",
            CreatedAt = s_created,
            UpdatedAt = s_created,
        };
        var harbourRoad = new Address
        {
            Id = Guid.NewGuid(),
            BuildingNumber = "12",
            Street = "Harbour Road",
            Locality = "Hong Kong",
            CountryIso = "HK",
            CreatedAt = s_created,
            UpdatedAt = s_created,
        };

        organisation.Addresses.Add(Link(organisation, millHouse, "REGISTERED"));
        organisation.Addresses.Add(Link(organisation, millHouse, "PRINCIPAL"));
        organisation.Addresses.Add(Link(organisation, harbourRoad, "CORRESPONDENCE"));

        return organisation;
    }

    private static OrganisationAddress Link(Organisation organisation, Address address, string usageType)
    {
        var addressUsageType = new AddressUsageType { Id = Guid.NewGuid(), Name = usageType };

        return new OrganisationAddress
        {
            Id = Guid.NewGuid(),
            OrganisationId = organisation.Id,
            Organisation = organisation,
            AddressId = address.Id,
            Address = address,
            UsageTypeId = addressUsageType.Id,
            UsageType = addressUsageType,
            ValidFrom = s_created,
            CreatedAt = s_created,
            UpdatedAt = s_created,
        };
    }

    private async Task<HttpResponseMessage> SendGet(
        string id,
        HttpClient? client = null,
        string? acceptHeaderValue = $"{MediaType}; version=2"
    )
    {
        client ??= CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, Testing.Endpoints.Organisations.Get(id));

        if (acceptHeaderValue is not null)
            request.Headers.TryAddWithoutValidation("Accept", acceptHeaderValue);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}

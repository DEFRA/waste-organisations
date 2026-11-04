using System.Net;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data.Postgres.Entities;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Defra.WasteOrganisations.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Organisation = Defra.WasteOrganisations.Api.Data.Postgres.Entities.Organisation;

namespace Defra.WasteOrganisations.Api.Tests.Endpoints.Organisations;

// Validation, authentication and versioning all run before the handler, so these tests need no
// Postgres; the service is a substitute, which also proves a rejected request never reaches it.
// The stored rows are covered by Api.IntegrationTests
public class PostTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private const string MediaType = "application/vnd.defra.waste-organisations+json";
    private static readonly DateTime s_now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private IPostgresOrganisationService MockOrganisationService { get; } =
        Substitute.For<IPostgresOrganisationService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        services.AddTransient<IPostgresOrganisationService>(_ => MockOrganisationService);
    }

    private static object Address(params string[] addressTypes) =>
        new
        {
            addressTypes,
            buildingNumber = "12",
            street = "High Street",
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
                addresses = addresses.Length > 0 ? addresses : new[] { Address("REGISTERED") },
            },
        };

    [Fact]
    public async Task WhenValidRequest_ShouldCreate()
    {
        GivenTheServiceStoresTheOrganisation();

        var response = await SendPost(
            Ltd(
                Address("REGISTERED", "PRINCIPAL"),
                new
                {
                    addressTypes = new[] { "CORRESPONDENCE" },
                    buildingName = "Tower",
                    street = "Main Road",
                    locality = "Dublin",
                    countryIso = "ie",
                }
            )
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (
            await response.Content.ReadFromJsonAsync<CreateOrganisationResponse>(TestContext.Current.CancellationToken)
        )!
            .Organisation
            .Id;
        response.Headers.Location.Should().Be($"/organisations/{id}");
        await MockOrganisationService.Received(1).OrganisationTypeIdAsync("LTD", Arg.Any<CancellationToken>());
        await VerifyJson(content);
    }

    [Fact]
    public async Task WhenSoleTraderWithoutTradingName_ShouldBeValidationProblem()
    {
        var response = await SendPost(
            new
            {
                organisation = new
                {
                    name = "Acme",
                    regulatorNation = "GB-WLS",
                    organisationType = "SOLE_TRADER",
                    addresses = new[] { Address("PRINCIPAL") },
                },
            }
        );

        await ShouldBeValidationProblem(response, "organisation.tradingName");
    }

    [Fact]
    public async Task WhenSecondAddressIsUkWithoutPostalCode_ShouldBeValidationProblem()
    {
        var response = await SendPost(
            Ltd(
                Address("REGISTERED"),
                new
                {
                    addressTypes = new[] { "CORRESPONDENCE" },
                    buildingNumber = "1",
                    street = "Mill Lane",
                    locality = "Testville",
                    countryIso = "GB",
                }
            )
        );

        await ShouldBeValidationProblem(response, "organisation.addresses[1].postalCode");
    }

    // The framework's validation would skip the organisation's rules once the address has an
    // attribute error, hiding the missing tradingName (spec 0005, Decisions)
    [Fact]
    public async Task WhenStreetAndTradingNameMissing_ShouldReportBoth()
    {
        var response = await SendPost(
            new
            {
                organisation = new
                {
                    regulatorNation = "GB-SCT",
                    organisationType = "SOLE_TRADER",
                    addresses = new[]
                    {
                        new
                        {
                            addressTypes = new[] { "PRINCIPAL" },
                            buildingNumber = "12",
                            locality = "Testville",
                            postalCode = "AB1 2CD",
                            countryIso = "GB",
                        },
                    },
                },
            }
        );

        await ShouldBeValidationProblem(response, "organisation.addresses[0].street", "organisation.tradingName");
    }

    [Fact]
    public async Task WhenOrganisationTypeUnknown_ShouldReportItAlone()
    {
        var response = await SendPost(
            new
            {
                organisation = new
                {
                    regulatorNation = "GB-NIR",
                    organisationType = "NOT_A_TYPE",
                    addresses = new[] { Address("CORRESPONDENCE") },
                },
            }
        );

        await ShouldBeValidationProblem(response, "organisation.organisationType");
    }

    [Theory]
    [InlineData("MalformedJson", "{\"organisation\": ")]
    [InlineData(
        "AddressTypesIsString",
        """{"organisation":{"name":"Acme Waste Ltd","companiesHouseNumber":"12345678","regulatorNation":"GB-ENG","organisationType":"LTD","addresses":[{"addressTypes":"REGISTERED","buildingNumber":"12","street":"High Street","locality":"Testville","postalCode":"AB1 2CD","countryIso":"GB"}]}}"""
    )]
    public async Task WhenBodyUnreadable_ShouldBeBadRequestWithDetail(string scenario, string body)
    {
        var response = await SendPost(new StringContent(body, Encoding.UTF8, "application/json"));
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ShouldNotHaveCalledTheService();
        await VerifyJson(content).UseParameters(scenario);
    }

    // v1 has no POST /organisations, so a request that does not ask for v2 keeps v1's 405
    // (spec 0004, X-ROUTE-01)
    [Theory]
    [InlineData(null)]
    [InlineData($"{MediaType}; version=1")]
    public async Task WhenVersion2NotRequested_ShouldBeMethodNotAllowed(string? acceptHeaderValue)
    {
        var response = await SendPost(Ltd(), acceptHeaderValue: acceptHeaderValue);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        response.Content.Headers.Allow.Should().Equal("GET");
        await ShouldNotHaveCalledTheService();
    }

    [Fact]
    public async Task WhenVersion3Requested_ShouldBeNotAcceptable()
    {
        var response = await SendPost(Ltd(), acceptHeaderValue: $"{MediaType}; version=3");
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
        await ShouldNotHaveCalledTheService();
        await VerifyJson(content);
    }

    [Fact]
    public async Task WhenNoCredentials_ShouldBeUnauthorizedWithEmptyBody()
    {
        var response = await SendPost(Ltd(), CreateClient(addAuthorizationHeader: false));
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        content.Should().BeEmpty();
        await ShouldNotHaveCalledTheService();
    }

    [Fact]
    public async Task WhenReadOnlyCredentials_ShouldBeForbiddenWithEmptyBody()
    {
        var response = await SendPost(Ltd(), CreateClient(testUser: TestUser.ReadOnly));
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        content.Should().BeEmpty();
        await ShouldNotHaveCalledTheService();
    }

    private async Task ShouldBeValidationProblem(HttpResponseMessage response, params string[] keys)
    {
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(
            TestContext.Current.CancellationToken
        );
        problem!.Errors.Keys.Should().BeEquivalentTo(keys);
        await ShouldNotHaveCalledTheService();
        await VerifyJson(content);
    }

    private async Task ShouldNotHaveCalledTheService() =>
        await MockOrganisationService
            .DidNotReceive()
            .CreateAsync(Arg.Any<Organisation>(), Arg.Any<CancellationToken>());

    // Stands in for PostgresOrganisationService: sets the navigations and timestamps the stored
    // organisation has, as the response mapping needs them
    private void GivenTheServiceStoresTheOrganisation()
    {
        var organisationType = new OrganisationType { Id = Guid.NewGuid(), Code = "LTD" };
        var usageTypes = Enum.GetValues<AddressType>()
            .Select(kind => new AddressUsageType { Id = Guid.NewGuid(), Name = kind.ToJsonValue() })
            .ToList();

        MockOrganisationService
            .OrganisationTypeIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(organisationType.Id);
        MockOrganisationService
            .UsageTypeIdsAsync(Arg.Any<CancellationToken>())
            .Returns(usageTypes.ToDictionary(u => u.Name.FromJsonValue<AddressType>(), u => u.Id));
        MockOrganisationService
            .CreateAsync(Arg.Any<Organisation>(), Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var organisation = (Organisation)args[0];
                organisation.OrganisationType = organisationType;
                organisation.ValidFrom = organisation.CreatedAt = organisation.UpdatedAt = s_now;

                foreach (var link in organisation.Addresses)
                    link.UsageType = usageTypes.Single(u => u.Id == link.UsageTypeId);

                return organisation;
            });
    }

    private Task<HttpResponseMessage> SendPost(
        object body,
        HttpClient? client = null,
        string? acceptHeaderValue = $"{MediaType}; version=2"
    ) => SendPost(JsonContent.Create(body), client, acceptHeaderValue);

    private async Task<HttpResponseMessage> SendPost(
        HttpContent content,
        HttpClient? client = null,
        string? acceptHeaderValue = $"{MediaType}; version=2"
    )
    {
        client ??= CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, Testing.Endpoints.Organisations.Post())
        {
            Content = content,
        };

        if (acceptHeaderValue is not null)
            request.Headers.TryAddWithoutValidation("Accept", acceptHeaderValue);

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}

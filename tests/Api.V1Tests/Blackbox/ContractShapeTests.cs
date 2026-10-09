using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Testing;
using Defra.WasteOrganisations.Testing.Fixtures;

namespace Defra.WasteOrganisations.Api.V1Tests.Blackbox;

[Trait("Category", "V1Baseline")]
[Trait("Category", "IntegrationTests")]
public class ContractShapeTests : BlackboxTestBase
{
    // SHAPE-01
    [Fact]
    public async Task WhenGettingOrganisation_ShouldExposeExactContractPropertyNames()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();

        await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Get(id),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        PropertyNames(root)
            .Should()
            .BeEquivalentTo(
                "id",
                "name",
                "tradingName",
                "businessCountry",
                "companiesHouseNumber",
                "address",
                "registrations"
            );

        PropertyNames(root.GetProperty("address"))
            .Should()
            .BeEquivalentTo("addressLine1", "addressLine2", "town", "county", "postcode", "country");

        var registration = root.GetProperty("registrations").EnumerateArray().First();

        PropertyNames(registration)
            .Should()
            .BeEquivalentTo("status", "type", "registrationYear", "created", "updated");

        root.GetProperty("businessCountry").ValueKind.Should().Be(JsonValueKind.String);
        registration.GetProperty("status").ValueKind.Should().Be(JsonValueKind.String);
        registration.GetProperty("type").ValueKind.Should().Be(JsonValueKind.String);
        registration.GetProperty("registrationYear").ValueKind.Should().Be(JsonValueKind.Number);
    }

    // SHAPE-02
    [Fact]
    public async Task WhenSearching_ShouldExposeSingleOrganisationsRootProperty()
    {
        var client = CreateApiKeyClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Search(EndpointQuery.New),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);

        PropertyNames(document.RootElement)
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("organisations");
    }

    // SHAPE-03
    [Fact]
    public async Task WhenBadRequest_ShouldReturnProblemDetailsContentType()
    {
        var client = CreateApiKeyClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Search(
                EndpointQuery.New.Where(EndpointFilter.Statuses("PENDING"))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    // SHAPE-03
    [Fact]
    public async Task WhenNotFound_ShouldReturnEmptyBodyWithoutProblemDetails()
    {
        var client = CreateApiKeyClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Get(NewOrganisationId()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().BeEmpty();
        response.Content.Headers.ContentType.Should().BeNull();
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name);
}

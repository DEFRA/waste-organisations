using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Data;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

// The v2 create refusing a stored Companies House number (spec 0008), against the Compose stack
public class DuplicateOrganisationTests : IntegrationTestBase
{
    [Fact]
    public async Task SameRequestTwice_ShouldBeConflictAndStoreNothing()
    {
        var body = Ltd(CompaniesHouseNumbers.Generate(), Address(NewSuffix(), "REGISTERED"));

        var first = await PostAsync(body);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadOrganisationAsync(first);

        var before = await CountRowsAsync();
        var second = await PostAsync(body);
        var after = await CountRowsAsync();

        await ShouldBeConflictAsync(second, created.Id);
        after.Should().Be(before);
    }

    [Fact]
    public async Task NumberInLowerCaseWithSpaces_ShouldBeConflict()
    {
        var number = CompaniesHouseNumbers.Generate("SC");

        var first = await PostAsync(Ltd(number, Address(NewSuffix(), "REGISTERED")));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadOrganisationAsync(first);

        var second = await PostAsync(Ltd($" {number.ToLowerInvariant()} ", Address(NewSuffix(), "REGISTERED")));

        await ShouldBeConflictAsync(second, created.Id);
    }

    private static async Task ShouldBeConflictAsync(HttpResponseMessage response, Guid existingId)
    {
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var problem = JsonNode.Parse(content)!;

        response.StatusCode.Should().Be(HttpStatusCode.Conflict, content);
        response.Headers.Location.Should().Be($"/organisations/{existingId}");
        problem["title"]!.GetValue<string>().Should().Be("Organisation already exists");
        problem["status"]!.GetValue<int>().Should().Be(409);
        problem["detail"]!
            .GetValue<string>()
            .Should()
            .Be("An organisation with this companiesHouseNumber already exists.");
        problem["organisationId"]!.GetValue<Guid>().Should().Be(existingId);
    }

    // Whole-table counts hold still between two requests because the integration tests run in one collection
    private static async Task<(int Organisations, int Links, int Addresses)> CountRowsAsync()
    {
        await using var provider = BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;

        return (
            await context.Organisations.CountAsync(cancellationToken),
            await context.OrganisationAddresses.CountAsync(cancellationToken),
            await context.Addresses.CountAsync(cancellationToken)
        );
    }

    private static object Address(string suffix, params string[] addressTypes) =>
        new
        {
            addressTypes,
            addressLine1 = $"12 High Street {suffix}",
            townOrCity = "Testville",
            postcode = "AB1 2CD",
            countryIso = "GB",
        };

    private static object Ltd(string companiesHouseNumber, params object[] addresses) =>
        new
        {
            organisation = new
            {
                name = "Acme Waste Ltd",
                companiesHouseNumber,
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

    private static async Task<OrganisationResponse> ReadOrganisationAsync(HttpResponseMessage response) =>
        (
            await response.Content.ReadFromJsonAsync<CreateOrganisationResponse>(TestContext.Current.CancellationToken)
        )!.Organisation;
}

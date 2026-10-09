using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Testing;
using Defra.WasteOrganisations.Testing.Fixtures;

namespace Defra.WasteOrganisations.Api.V1Tests.Blackbox;

[Trait("Category", "V1Baseline")]
[Trait("Category", "IntegrationTests")]
public class SearchOrganisationsTests : BlackboxTestBase
{
    // SEARCH-01
    [Fact]
    public async Task WhenNoFilters_ShouldReturnOkContainingCreatedOrganisation()
    {
        var client = CreateApiKeyClient();
        var id = await CreateOrganisation(client);

        var ids = await SearchIds(client, EndpointQuery.New);

        ids.Should().Contain(id);
    }

    // SEARCH-02
    [Theory]
    [InlineData(RegistrationType.SmallProducer)]
    [InlineData(RegistrationType.LargeProducer)]
    [InlineData(RegistrationType.ComplianceScheme)]
    [InlineData(RegistrationType.Reprocessor)]
    [InlineData(RegistrationType.Exporter)]
    public async Task WhenFilteredByType_ShouldReturnOnlyMatchingType(RegistrationType type)
    {
        var client = CreateApiKeyClient();
        var otherType =
            type == RegistrationType.SmallProducer ? RegistrationType.LargeProducer : RegistrationType.SmallProducer;

        var matching = await CreateOrganisation(client, type: type);
        var excluded = await CreateOrganisation(client, type: otherType);

        var ids = await SearchIds(client, EndpointQuery.New.Where(EndpointFilter.Registrations([type])));

        ids.Should().Contain(matching).And.NotContain(excluded);
    }

    // SEARCH-03
    [Fact]
    public async Task WhenFilteredByTwoTypes_ShouldReturnEitherType()
    {
        var client = CreateApiKeyClient();

        var large = await CreateOrganisation(client, type: RegistrationType.LargeProducer);
        var exporter = await CreateOrganisation(client, type: RegistrationType.Exporter);
        var excluded = await CreateOrganisation(client, type: RegistrationType.SmallProducer);

        var ids = await SearchIds(
            client,
            EndpointQuery.New.Where(
                EndpointFilter.Registrations([RegistrationType.LargeProducer, RegistrationType.Exporter])
            )
        );

        ids.Should().Contain(large).And.Contain(exporter).And.NotContain(excluded);
    }

    // SEARCH-04
    [Theory]
    [InlineData(2023)]
    [InlineData(2035)]
    [InlineData(2050)]
    public async Task WhenFilteredByYear_ShouldReturnOnlyMatchingYear(int year)
    {
        var client = CreateApiKeyClient();
        var otherYear = year == 2023 ? 2050 : 2023;

        var matching = await CreateOrganisation(client, registrationYear: year);
        var excluded = await CreateOrganisation(client, registrationYear: otherYear);

        var ids = await SearchIds(client, EndpointQuery.New.Where(EndpointFilter.RegistrationYears([year])));

        ids.Should().Contain(matching).And.NotContain(excluded);
    }

    // SEARCH-05
    [Fact]
    public async Task WhenFilteredByTwoYears_ShouldReturnEitherYear()
    {
        var client = CreateApiKeyClient();

        var first = await CreateOrganisation(client, registrationYear: 2024);
        var second = await CreateOrganisation(client, registrationYear: 2025);
        var excluded = await CreateOrganisation(client, registrationYear: 2030);

        var ids = await SearchIds(client, EndpointQuery.New.Where(EndpointFilter.RegistrationYears([2024, 2025])));

        ids.Should().Contain(first).And.Contain(second).And.NotContain(excluded);
    }

    // SEARCH-06
    [Theory]
    [InlineData(RegistrationStatus.Registered)]
    [InlineData(RegistrationStatus.Cancelled)]
    public async Task WhenFilteredByStatus_ShouldReturnOnlyMatchingStatus(RegistrationStatus status)
    {
        var client = CreateApiKeyClient();
        var otherStatus =
            status == RegistrationStatus.Registered ? RegistrationStatus.Cancelled : RegistrationStatus.Registered;

        var matching = await CreateOrganisation(client, status: status);
        var excluded = await CreateOrganisation(client, status: otherStatus);

        var ids = await SearchIds(client, EndpointQuery.New.Where(EndpointFilter.Statuses([status])));

        ids.Should().Contain(matching).And.NotContain(excluded);
    }

    // SEARCH-07
    [Fact]
    public async Task WhenAllThreeFiltersCombined_ShouldExcludePartialMatches()
    {
        var client = CreateApiKeyClient();

        var matchesAll = await CreateOrganisation(
            client,
            type: RegistrationType.SmallProducer,
            registrationYear: 2025,
            status: RegistrationStatus.Registered
        );
        var wrongStatus = await CreateOrganisation(
            client,
            type: RegistrationType.SmallProducer,
            registrationYear: 2025,
            status: RegistrationStatus.Cancelled
        );
        var wrongYear = await CreateOrganisation(
            client,
            type: RegistrationType.SmallProducer,
            registrationYear: 2030,
            status: RegistrationStatus.Registered
        );

        var ids = await SearchIds(
            client,
            EndpointQuery
                .New.Where(EndpointFilter.Registrations([RegistrationType.SmallProducer]))
                .Where(EndpointFilter.RegistrationYears([2025]))
                .Where(EndpointFilter.Statuses([RegistrationStatus.Registered]))
        );

        ids.Should().Contain(matchesAll).And.NotContain(wrongStatus).And.NotContain(wrongYear);
    }

    // SEARCH-08
    [Fact]
    public async Task WhenFilterMatchesNothing_ShouldReturnOkWithoutCreatedOrganisation()
    {
        var client = CreateApiKeyClient();

        var id = await CreateOrganisation(
            client,
            type: RegistrationType.SmallProducer,
            registrationYear: 2025,
            status: RegistrationStatus.Registered
        );

        var ids = await SearchIds(
            client,
            EndpointQuery
                .New.Where(EndpointFilter.Registrations([RegistrationType.Exporter]))
                .Where(EndpointFilter.RegistrationYears([2049]))
                .Where(EndpointFilter.Statuses([RegistrationStatus.Cancelled]))
        );

        ids.Should().NotContain(id);
    }

    // SEARCH-09
    [Fact]
    public async Task WhenRegistrationsInvalid_ShouldReturnBadRequest()
    {
        var client = CreateApiKeyClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Search(
                EndpointQuery.New.Where(EndpointFilter.Registrations("SMALL_PRODUCER,UNKNOWN"))
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // SEARCH-10
    [Fact]
    public async Task WhenRegistrationYearsInvalid_ShouldReturnBadRequest()
    {
        var client = CreateApiKeyClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Search(EndpointQuery.New.Where(EndpointFilter.RegistrationYears("2022"))),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // SEARCH-11
    [Fact]
    public async Task WhenStatusesInvalid_ShouldReturnBadRequest()
    {
        var client = CreateApiKeyClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Search(EndpointQuery.New.Where(EndpointFilter.Statuses("PENDING"))),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    private static async Task<Guid> CreateOrganisation(
        HttpClient client,
        RegistrationType type = RegistrationType.SmallProducer,
        int registrationYear = 2025,
        RegistrationStatus status = RegistrationStatus.Registered
    )
    {
        var id = NewOrganisationId();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures
                .Default()
                .With(
                    x => x.Registration,
                    RegistrationDtoFixtures
                        .Default()
                        .With(r => r.Type, type)
                        .With(r => r.RegistrationYear, registrationYear)
                        .With(r => r.Status, status)
                        .Create()
                )
                .Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return id;
    }

    private static async Task<Guid[]> SearchIds(HttpClient client, EndpointQuery query)
    {
        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Search(query),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<OrganisationSearch>(
            TestContext.Current.CancellationToken
        );

        result.Should().NotBeNull();

        return result.Organisations.Select(x => x.Id).ToArray();
    }
}

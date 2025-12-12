using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Testing;
using Defra.WasteOrganisations.Testing.Fixtures;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

public class OrganisationTests : MongoTestBase
{
    [Fact]
    public async Task OrganisationCreatedAndUpdated()
    {
        var client = CreateClient();
        var id = Guid.NewGuid();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures
                .Default()
                .With(
                    x => x.Registration,
                    RegistrationDtoFixtures.Default().With(x => x.Type, RegistrationType.LargeProducer).Create()
                )
                .Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var organisation = await client.GetStringAsync(
            Testing.Endpoints.Organisations.Get(id),
            cancellationToken: TestContext.Current.CancellationToken
        );

        await VerifyJson(organisation);
    }

    [Fact]
    public async Task OrganisationSearch()
    {
        var client = CreateClient();
        var organisations = OrganisationRegistrationDtoFixtures.Organisation().CreateMany(10);
        var registrations = RegistrationDtoFixtures.Registration().CreateMany(100).ToArray();

        foreach (var organisation in organisations)
        {
            var id = Guid.NewGuid();

            await client.PutAsJsonAsync(
                Testing.Endpoints.Organisations.Put(id),
                organisation,
                TestContext.Current.CancellationToken
            );

            var random = Random.Shared.Next(1, 10);

            for (var i = 0; i < random; i++)
            {
                await client.PutAsJsonAsync(
                    Testing.Endpoints.Organisations.Put(id),
                    organisation with
                    {
                        Registration = registrations[Random.Shared.Next(registrations.Length)],
                    },
                    TestContext.Current.CancellationToken
                );
            }
        }

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Search(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var allOrganisations = await response.Content.ReadFromJsonAsync<OrganisationSearch>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        allOrganisations.Should().NotBeNull();
        allOrganisations.Organisations.Length.Should().Be(10);

        var largeProducers = allOrganisations
            .Organisations.Where(x => x.Registrations.Any(y => y.Type == RegistrationType.LargeProducer))
            .ToArray();

        response = await client.GetAsync(
            Testing.Endpoints.Organisations.Search(
                EndpointQuery.New.Where(EndpointFilter.Registrations([RegistrationType.LargeProducer]))
            ),
            TestContext.Current.CancellationToken
        );

        var largeProducerOrganisations = await response.Content.ReadFromJsonAsync<OrganisationSearch>(
            cancellationToken: TestContext.Current.CancellationToken
        );

        largeProducerOrganisations.Should().NotBeNull();
        largeProducerOrganisations.Organisations.Length.Should().Be(largeProducers.Length);
    }
}

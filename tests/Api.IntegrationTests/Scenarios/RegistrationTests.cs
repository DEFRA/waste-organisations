using System.Net;
using System.Net.Http.Json;
using Api.Dtos;
using Api.Extensions;
using AutoFixture;
using AwesomeAssertions;
using Testing.Fixtures;

namespace Api.IntegrationTests.Scenarios;

public class RegistrationTests : MongoTestBase
{
    [Fact]
    public async Task RegistrationCreatedAndUpdated()
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
            Testing.Endpoints.Organisations.RegistrationsPut(id, RegistrationType.LargeProducer.ToJsonValue(), "2026"),
            RegistrationRequestDtoFixtures.Default().With(x => x.Status, RegistrationStatus.Registered).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(id, RegistrationType.LargeProducer.ToJsonValue(), "2026"),
            RegistrationRequestDtoFixtures.Default().With(x => x.Status, RegistrationStatus.Cancelled).Create(),
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
    public async Task RegistrationDeleted()
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
            Testing.Endpoints.Organisations.RegistrationsPut(id, RegistrationType.LargeProducer.ToJsonValue(), "2026"),
            RegistrationRequestDtoFixtures.Default().With(x => x.Status, RegistrationStatus.Registered).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(id, RegistrationType.LargeProducer.ToJsonValue(), "2027"),
            RegistrationRequestDtoFixtures.Default().With(x => x.Status, RegistrationStatus.Registered).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        response = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(
                id,
                RegistrationType.SmallProducer.ToJsonValue(),
                "2025"
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var organisation = await client.GetStringAsync(
            Testing.Endpoints.Organisations.Get(id),
            cancellationToken: TestContext.Current.CancellationToken
        );

        await VerifyJson(organisation);
    }
}

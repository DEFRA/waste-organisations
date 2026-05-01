using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Defra.WasteOrganisations.Testing.Fixtures;
using MongoDB.Driver;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

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

        await Task.Delay(250, TestContext.Current.CancellationToken);

        response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(id, RegistrationType.LargeProducer.ToJsonValue(), "2026"),
            RegistrationRequestDtoFixtures.Default().With(x => x.Status, RegistrationStatus.Registered).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await Task.Delay(250, TestContext.Current.CancellationToken);

        response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(id, RegistrationType.LargeProducer.ToJsonValue(), "2026"),
            RegistrationRequestDtoFixtures.Default().With(x => x.Status, RegistrationStatus.Cancelled).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await client.GetStringAsync(
            Testing.Endpoints.Organisations.Get(id),
            cancellationToken: TestContext.Current.CancellationToken
        );

        await VerifyJson(content);

        var organisation = JsonSerializer.Deserialize<Organisation>(content);

        organisation.Should().NotBeNull();
        organisation.Registrations.Length.Should().Be(2);

        // First registration created has same values for Created and Updated
        organisation.Registrations[0].Created.Should().Be(organisation.Registrations[0].Updated);
        // Second registration created has a later Created date
        organisation.Registrations[1].Created.Should().BeAfter(organisation.Registrations[0].Created);
        // Second registration after update has an Updated date after its Created date
        organisation.Registrations[1].Updated.Should().BeAfter(organisation.Registrations[1].Created);
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

        await Task.Delay(250, TestContext.Current.CancellationToken);

        response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(id, RegistrationType.LargeProducer.ToJsonValue(), "2026"),
            RegistrationRequestDtoFixtures.Default().With(x => x.Status, RegistrationStatus.Registered).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await Task.Delay(250, TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task BackwardsCompatibleWithProductionData()
    {
        var client = CreateClient();
        var id = Guid.NewGuid();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Remove Created and Updated fields from stored registration to replicate
        // production state at time of writing
        var update = Builders<Data.Entities.Organisation>
            .Update.Unset("registrations.$[].created")
            .Unset("registrations.$[].updated");

        await Organisations.UpdateOneAsync(
            x => x.Id == id,
            update,
            cancellationToken: TestContext.Current.CancellationToken
        );

        var mongoOrganisation = await Organisations
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        var organisation = await client.GetFromJsonAsync<Organisation>(
            Testing.Endpoints.Organisations.Get(id),
            cancellationToken: TestContext.Current.CancellationToken
        );

        mongoOrganisation.Created.Should().Be(mongoOrganisation.Updated);

        organisation.Should().NotBeNull();
        organisation.Registrations[0].Created.Should().Be(mongoOrganisation.Created);
        organisation.Registrations[0].Updated.Should().Be(mongoOrganisation.Created);

        await Task.Delay(250, TestContext.Current.CancellationToken);

        response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.Default().With(x => x.TradingName, "Different Trading Name").Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        mongoOrganisation = await Organisations
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        organisation = await client.GetFromJsonAsync<Organisation>(
            Testing.Endpoints.Organisations.Get(id),
            cancellationToken: TestContext.Current.CancellationToken
        );

        mongoOrganisation.Updated.Should().BeAfter(mongoOrganisation.Created);

        organisation.Should().NotBeNull();
        organisation.Registrations[0].Created.Should().Be(mongoOrganisation.Created);
        organisation.Registrations[0].Updated.Should().Be(mongoOrganisation.Created);
    }
}

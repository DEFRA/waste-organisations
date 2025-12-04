using System.Net;
using System.Net.Http.Json;
using Api.Dtos;
using AutoFixture;
using AwesomeAssertions;
using Testing.Fixtures;

namespace Api.IntegrationTests.Scenarios;

public class OrganisationTests : IntegrationTestBase
{
    [Fact]
    public async Task OrganisationCreatedAndUpdated()
    {
        var client = CreateClient();
        var id = Guid.NewGuid().ToString();

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
}

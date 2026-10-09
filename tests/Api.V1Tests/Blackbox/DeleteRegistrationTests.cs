using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Defra.WasteOrganisations.Testing.Fixtures;

namespace Defra.WasteOrganisations.Api.V1Tests.Blackbox;

[Trait("Category", "V1Baseline")]
[Trait("Category", "IntegrationTests")]
public class DeleteRegistrationTests : BlackboxTestBase
{
    private static string SmallProducerWire => RegistrationType.SmallProducer.ToJsonValue();
    private static string LargeProducerWire => RegistrationType.LargeProducer.ToJsonValue();

    private static async Task CreateOrganisation(HttpClient client, Guid id)
    {
        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static async Task AddRegistration(
        HttpClient client,
        Guid id,
        string type,
        string registrationYear
    )
    {
        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(id, type, registrationYear),
            new RegistrationRequest { Status = RegistrationStatus.Registered },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // DEL-01
    [Fact]
    public async Task WhenRegistrationExists_ShouldReturnNoContentAndKeepOthers()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();
        await CreateOrganisation(client, id);
        await AddRegistration(client, id, LargeProducerWire, "2026");

        var response = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(id, LargeProducerWire, "2026"),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().BeEmpty();

        var organisation = await client.GetFromJsonAsync<Organisation>(
            Testing.Endpoints.Organisations.Get(id),
            TestContext.Current.CancellationToken
        );

        organisation.Should().NotBeNull();
        organisation
            .Registrations.Select(x => x.Type)
            .Should()
            .BeEquivalentTo([RegistrationType.SmallProducer]);
    }

    // DEL-02
    [Fact]
    public async Task WhenDeletedTwice_ShouldReturnNoContentThenNotFound()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();
        await CreateOrganisation(client, id);

        var first = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(id, SmallProducerWire, "2025"),
            TestContext.Current.CancellationToken
        );
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(id, SmallProducerWire, "2025"),
            TestContext.Current.CancellationToken
        );
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // DEL-03
    [Fact]
    public async Task WhenOrganisationUnknown_ShouldReturnNotFound()
    {
        var client = CreateApiKeyClient();

        var response = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(
                NewOrganisationId(),
                SmallProducerWire,
                "2025"
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // DEL-04
    [Fact]
    public async Task WhenRegistrationNotPresent_ShouldReturnNotFound()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();
        await CreateOrganisation(client, id);

        var response = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(id, LargeProducerWire, "2026"),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // DEL-05
    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("INVALID")]
    public async Task WhenInvalidType_ShouldReturnBadRequest(string type)
    {
        var client = CreateApiKeyClient();

        var response = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(NewOrganisationId(), type, "2025"),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // DEL-06
    [Theory]
    [InlineData("2022")]
    [InlineData("2051")]
    public async Task WhenRegistrationYearOutOfRange_ShouldReturnBadRequest(string registrationYear)
    {
        var client = CreateApiKeyClient();

        var response = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(
                NewOrganisationId(),
                SmallProducerWire,
                registrationYear
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body).IgnoreParameters();
    }

    // DEL-07
    [Fact]
    public async Task WhenLastRegistrationDeleted_ShouldReturnNoContentAndKeepOrganisation()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();
        await CreateOrganisation(client, id);

        var response = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(id, SmallProducerWire, "2025"),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var organisation = await client.GetFromJsonAsync<Organisation>(
            Testing.Endpoints.Organisations.Get(id),
            TestContext.Current.CancellationToken
        );

        organisation.Should().NotBeNull();
        organisation.Registrations.Should().BeEmpty();
    }
}

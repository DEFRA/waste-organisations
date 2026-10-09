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
public class PutRegistrationTests : BlackboxTestBase
{
    private static string SmallProducerWire => RegistrationType.SmallProducer.ToJsonValue();

    private static async Task CreateOrganisation(HttpClient client, Guid id)
    {
        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // REG-01
    [Fact]
    public async Task WhenNewTypeAndYear_ShouldReturnCreatedWithLocationAndBody()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();
        await CreateOrganisation(client, id);

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(id, RegistrationType.LargeProducer.ToJsonValue(), "2026"),
            new RegistrationRequest { Status = RegistrationStatus.Registered },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be($"/organisations/{id}/registrations/LARGE_PRODUCER-2026");

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // REG-02
    [Fact]
    public async Task WhenSameTypeAndYearWithDifferentStatus_ShouldReturnOkAndKeepCreated()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();

        var createResponse = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<Organisation>(
            TestContext.Current.CancellationToken
        );
        var original = created!.Registrations.Single();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(id, SmallProducerWire, "2025"),
            new RegistrationRequest { Status = RegistrationStatus.Cancelled },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var registration = await response.Content.ReadFromJsonAsync<RegistrationResponse>(
            TestContext.Current.CancellationToken
        );

        registration.Should().NotBeNull();
        registration.Status.Should().Be(RegistrationStatus.Cancelled);
        registration.Created.Should().Be(original.Created);
        registration.Updated.Should().BeOnOrAfter(original.Updated);
    }

    // REG-03
    [Fact]
    public async Task WhenSameTypeAndYearWithSameStatus_ShouldReturnOk()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();
        await CreateOrganisation(client, id);

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(id, SmallProducerWire, "2025"),
            new RegistrationRequest { Status = RegistrationStatus.Registered },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var registration = await response.Content.ReadFromJsonAsync<RegistrationResponse>(
            TestContext.Current.CancellationToken
        );

        registration.Should().NotBeNull();
        registration.Status.Should().Be(RegistrationStatus.Registered);
    }

    // REG-04
    [Fact]
    public async Task WhenRegistrationAdded_SubsequentGet_ShouldListIt()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();
        await CreateOrganisation(client, id);

        await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(id, RegistrationType.LargeProducer.ToJsonValue(), "2026"),
            new RegistrationRequest { Status = RegistrationStatus.Registered },
            TestContext.Current.CancellationToken
        );

        var organisation = await client.GetFromJsonAsync<Organisation>(
            Testing.Endpoints.Organisations.Get(id),
            TestContext.Current.CancellationToken
        );

        organisation.Should().NotBeNull();
        organisation
            .Registrations.Select(x => x.Type)
            .Should()
            .BeEquivalentTo([RegistrationType.SmallProducer, RegistrationType.LargeProducer]);
    }

    // REG-05
    [Fact]
    public async Task WhenOrganisationUnknown_ShouldReturnNotFound()
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(NewOrganisationId(), SmallProducerWire, "2025"),
            new RegistrationRequest { Status = RegistrationStatus.Registered },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // REG-06
    [Theory]
    [InlineData("UNKNOWN")]
    [InlineData("INVALID")]
    public async Task WhenInvalidType_ShouldReturnBadRequest(string type)
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(NewOrganisationId(), type, "2025"),
            new RegistrationRequest { Status = RegistrationStatus.Registered },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // REG-07
    [Theory]
    [InlineData("2022")]
    [InlineData("2051")]
    public async Task WhenRegistrationYearOutOfRange_ShouldReturnBadRequest(string registrationYear)
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(NewOrganisationId(), SmallProducerWire, registrationYear),
            new RegistrationRequest { Status = RegistrationStatus.Registered },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body).IgnoreParameters();
    }

    // REG-07
    [Fact]
    public async Task WhenRegistrationYearNonNumeric_ShouldReturnNotFound()
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(NewOrganisationId(), SmallProducerWire, "abc"),
            new RegistrationRequest { Status = RegistrationStatus.Registered },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // REG-08
    [Theory]
    [InlineData("2023")]
    [InlineData("2050")]
    public async Task WhenRegistrationYearOnBoundary_ShouldReturnCreated(string registrationYear)
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();
        await CreateOrganisation(client, id);

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(id, SmallProducerWire, registrationYear),
            new RegistrationRequest { Status = RegistrationStatus.Registered },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // REG-09
    [Fact]
    public async Task WhenBodyStatusInvalid_ShouldReturnBadRequest()
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(NewOrganisationId(), SmallProducerWire, "2025"),
            new { status = "Invalid" },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // REG-09
    [Fact]
    public async Task WhenBodyStatusMissing_ShouldReturnBadRequest()
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(NewOrganisationId(), SmallProducerWire, "2025"),
            new { },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // REG-10
    [Theory]
    [InlineData(RegistrationType.SmallProducer, RegistrationStatus.Registered)]
    [InlineData(RegistrationType.SmallProducer, RegistrationStatus.Cancelled)]
    [InlineData(RegistrationType.LargeProducer, RegistrationStatus.Registered)]
    [InlineData(RegistrationType.LargeProducer, RegistrationStatus.Cancelled)]
    [InlineData(RegistrationType.ComplianceScheme, RegistrationStatus.Registered)]
    [InlineData(RegistrationType.ComplianceScheme, RegistrationStatus.Cancelled)]
    [InlineData(RegistrationType.Reprocessor, RegistrationStatus.Registered)]
    [InlineData(RegistrationType.Reprocessor, RegistrationStatus.Cancelled)]
    [InlineData(RegistrationType.Exporter, RegistrationStatus.Registered)]
    [InlineData(RegistrationType.Exporter, RegistrationStatus.Cancelled)]
    public async Task WhenEveryTypeAndStatus_ShouldReturnCreated(RegistrationType type, RegistrationStatus status)
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();
        await CreateOrganisation(client, id);

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(id, type.ToJsonValue(), "2026"),
            new RegistrationRequest { Status = status },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Testing.Fixtures;

namespace Defra.WasteOrganisations.Api.V1Tests.Blackbox;

[Trait("Category", "V1Baseline")]
[Trait("Category", "IntegrationTests")]
public class PutOrganisationTests : BlackboxTestBase
{
    // ORG-01
    [Fact]
    public async Task WhenNewId_ShouldReturnCreatedWithLocationAndBody()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be($"/organisations/{id}");

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // ORG-02
    [Fact]
    public async Task WhenCreated_SubsequentGet_ShouldHaveExactlyOneRegistration()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();

        await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );

        var organisation = await client.GetFromJsonAsync<Organisation>(
            Testing.Endpoints.Organisations.Get(id),
            TestContext.Current.CancellationToken
        );

        organisation.Should().NotBeNull();
        organisation.Registrations.Should().ContainSingle();
    }

    // ORG-03
    [Fact]
    public async Task WhenExistingIdWithDifferentRegistration_ShouldReturnOkWithBothRegistrations()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();

        await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures
                .Default()
                .With(x => x.Registration, RegistrationDtoFixtures.LargeProducer().Create())
                .Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var organisation = await response.Content.ReadFromJsonAsync<Organisation>(
            TestContext.Current.CancellationToken
        );

        organisation.Should().NotBeNull();
        organisation
            .Registrations.Select(x => x.Type)
            .Should()
            .BeEquivalentTo([RegistrationType.SmallProducer, RegistrationType.LargeProducer]);
    }

    // ORG-04
    [Fact]
    public async Task WhenExistingIdWithSameRegistrationDifferentStatus_ShouldReturnOkAndUpdateStatus()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();

        await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures
                .Default()
                .With(
                    x => x.Registration,
                    RegistrationDtoFixtures.Default().With(x => x.Status, RegistrationStatus.Cancelled).Create()
                )
                .Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var organisation = await response.Content.ReadFromJsonAsync<Organisation>(
            TestContext.Current.CancellationToken
        );

        organisation.Should().NotBeNull();
        organisation.Registrations.Should().ContainSingle();
        organisation.Registrations[0].Status.Should().Be(RegistrationStatus.Cancelled);
    }

    // ORG-05
    [Fact]
    public async Task WhenExistingFieldsUpdated_ShouldReplaceAndReturnOk()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();

        await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );

        var update = new OrganisationRegistration
        {
            Name = "Updated Name Ltd",
            TradingName = "Updated Trading Name",
            BusinessCountry = BusinessCountry.Wales,
            CompaniesHouseNumber = "99999999",
            Address = new Address
            {
                AddressLine1 = "Updated Line 1",
                AddressLine2 = "Updated Line 2",
                Town = "Updated Town",
                County = "Updated County",
                Postcode = "UP1",
                Country = "UK",
            },
            Registration = RegistrationDtoFixtures.Default().Create(),
        };

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            update,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // ORG-06
    [Theory]
    [InlineData(2023)]
    [InlineData(2050)]
    public async Task WhenRegistrationYearOnBoundary_ShouldReturnCreated(int registrationYear)
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(NewOrganisationId()),
            OrganisationRegistrationDtoFixtures
                .Default()
                .With(
                    x => x.Registration,
                    RegistrationDtoFixtures.Default().With(x => x.RegistrationYear, registrationYear).Create()
                )
                .Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ORG-07
    [Theory]
    [InlineData(2022)]
    [InlineData(2051)]
    public async Task WhenRegistrationYearOutOfRange_ShouldReturnBadRequest(int registrationYear)
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(NewOrganisationId()),
            OrganisationRegistrationDtoFixtures
                .Default()
                .With(
                    x => x.Registration,
                    RegistrationDtoFixtures.Default().With(x => x.RegistrationYear, registrationYear).Create()
                )
                .Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body).IgnoreParameters();
    }

    // ORG-08
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
    public async Task WhenEveryTypeAndStatus_ShouldReturnCreated(
        RegistrationType type,
        RegistrationStatus status
    )
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(NewOrganisationId()),
            OrganisationRegistrationDtoFixtures
                .Default()
                .With(
                    x => x.Registration,
                    RegistrationDtoFixtures.Default().With(x => x.Type, type).With(x => x.Status, status).Create()
                )
                .Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    // ORG-09
    [Fact]
    public async Task WhenBusinessCountryInvalid_ShouldReturnBadRequest()
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(NewOrganisationId()),
            new { BusinessCountry = "Invalid" },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // ORG-10
    [Fact]
    public async Task WhenRegistrationTypeInvalid_ShouldReturnBadRequest()
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(NewOrganisationId()),
            new { Address = new { }, Registration = new { Type = "Invalid" } },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // ORG-11
    [Fact]
    public async Task WhenRegistrationStatusInvalid_ShouldReturnBadRequest()
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(NewOrganisationId()),
            new { Address = new { }, Registration = new { Status = "Invalid" } },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // ORG-12
    [Fact]
    public async Task WhenNameMissing_ShouldReturnBadRequest()
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(NewOrganisationId()),
            new
            {
                address = new { },
                registration = new
                {
                    status = "REGISTERED",
                    type = "SMALL_PRODUCER",
                    registrationYear = 2025,
                },
            },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // ORG-12
    [Fact]
    public async Task WhenAddressMissing_ShouldReturnBadRequest()
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(NewOrganisationId()),
            new
            {
                name = "Missing Address Ltd",
                registration = new
                {
                    status = "REGISTERED",
                    type = "SMALL_PRODUCER",
                    registrationYear = 2025,
                },
            },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // ORG-12
    [Fact]
    public async Task WhenRegistrationMissing_ShouldReturnBadRequest()
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(NewOrganisationId()),
            new { name = "Missing Registration Ltd", address = new { } },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }

    // ORG-13
    [Theory]
    [InlineData("")]
    [InlineData("{ not valid json")]
    public async Task WhenBodyMalformed_ShouldReturnBadRequest(string payload)
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsync(
            Testing.Endpoints.Organisations.Put(NewOrganisationId()),
            new StringContent(payload, Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ORG-14
    [Fact]
    public async Task WhenIdNotGuid_ShouldReturnNotFound()
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put("not-a-guid"),
            OrganisationRegistrationDtoFixtures.Default().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ORG-15
    [Theory]
    [InlineData(BusinessCountry.England, "GB-ENG")]
    [InlineData(BusinessCountry.NorthernIreland, "GB-NIR")]
    [InlineData(BusinessCountry.Scotland, "GB-SCT")]
    [InlineData(BusinessCountry.Wales, "GB-WLS")]
    public async Task WhenBusinessCountryValid_ShouldEchoWireValue(
        BusinessCountry businessCountry,
        string wireValue
    )
    {
        var client = CreateApiKeyClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(NewOrganisationId()),
            OrganisationRegistrationDtoFixtures.Default().With(x => x.BusinessCountry, businessCountry).Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);

        document.RootElement.GetProperty("businessCountry").GetString().Should().Be(wireValue);
    }
}

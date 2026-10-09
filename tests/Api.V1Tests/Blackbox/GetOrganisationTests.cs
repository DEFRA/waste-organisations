using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Testing.Fixtures;

namespace Defra.WasteOrganisations.Api.V1Tests.Blackbox;

[Trait("Category", "V1Baseline")]
[Trait("Category", "IntegrationTests")]
public class GetOrganisationTests : BlackboxTestBase
{
    // GET-01
    [Fact]
    public async Task WhenOrganisationExists_ShouldReturnOk()
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

        await VerifyJson(body);
    }

    // GET-02
    [Fact]
    public async Task WhenOrganisationUnknown_ShouldReturnNotFound()
    {
        var client = CreateApiKeyClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Get(NewOrganisationId()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // GET-03
    [Fact]
    public async Task WhenIdNotGuid_ShouldReturnNotFound()
    {
        var client = CreateApiKeyClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Get("not-a-guid"),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // GET-04
    [Fact]
    public async Task WhenOptionalFieldsAbsent_ShouldPinShape()
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();

        var organisation = new OrganisationRegistration
        {
            Name = "Pinned Shape Ltd",
            Address = new Address(),
            Registration = RegistrationDtoFixtures.Default().Create(),
        };

        await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            organisation,
            TestContext.Current.CancellationToken
        );

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Get(id),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body);
    }
}

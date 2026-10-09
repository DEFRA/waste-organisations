using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Authentication;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Defra.WasteOrganisations.Testing.Fixtures;

namespace Defra.WasteOrganisations.Api.V1Tests.Blackbox;

public enum Operation
{
    Search,
    Get,
    PutOrganisation,
    PutRegistration,
    DeleteRegistration,
}

[Trait("Category", "V1Baseline")]
[Trait("Category", "IntegrationTests")]
public class AuthTests : BlackboxTestBase
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

    private static async Task Provision(HttpClient client, Operation operation, Guid id)
    {
        if (operation is Operation.Get or Operation.PutRegistration or Operation.DeleteRegistration)
            await CreateOrganisation(client, id);
    }

    private static Task<HttpResponseMessage> Execute(HttpClient client, Operation operation, Guid id) =>
        operation switch
        {
            Operation.Search => client.GetAsync(
                Testing.Endpoints.Organisations.Search(),
                TestContext.Current.CancellationToken
            ),
            Operation.Get => client.GetAsync(
                Testing.Endpoints.Organisations.Get(id),
                TestContext.Current.CancellationToken
            ),
            Operation.PutOrganisation => client.PutAsJsonAsync(
                Testing.Endpoints.Organisations.Put(id),
                OrganisationRegistrationDtoFixtures.Default().Create(),
                TestContext.Current.CancellationToken
            ),
            Operation.PutRegistration => client.PutAsJsonAsync(
                Testing.Endpoints.Organisations.RegistrationsPut(id, LargeProducerWire, "2026"),
                new RegistrationRequest { Status = RegistrationStatus.Registered },
                TestContext.Current.CancellationToken
            ),
            Operation.DeleteRegistration => client.DeleteAsync(
                Testing.Endpoints.Organisations.RegistrationsDelete(id, SmallProducerWire, "2025"),
                TestContext.Current.CancellationToken
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

    private static HttpClient CreateClientWithAuthorization(AuthenticationHeaderValue? authorization)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = authorization;
        return client;
    }

    // AUTH-01
    [Theory]
    [InlineData(Operation.Search)]
    [InlineData(Operation.Get)]
    [InlineData(Operation.PutOrganisation)]
    [InlineData(Operation.PutRegistration)]
    [InlineData(Operation.DeleteRegistration)]
    public async Task WhenNoAuthorizationHeader_ShouldReturnUnauthorized(Operation operation)
    {
        var client = CreateClientWithAuthorization(null);

        var response = await Execute(client, operation, NewOrganisationId());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AUTH-02
    [Theory]
    [InlineData(Operation.Search)]
    [InlineData(Operation.Get)]
    [InlineData(Operation.PutOrganisation)]
    [InlineData(Operation.PutRegistration)]
    [InlineData(Operation.DeleteRegistration)]
    public async Task WhenDeveloperApiKey_ShouldSucceed(Operation operation)
    {
        var client = CreateApiKeyClient();
        var id = NewOrganisationId();
        await Provision(client, operation, id);

        var response = await Execute(client, operation, id);

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    // AUTH-03
    [Fact]
    public async Task WhenApiKeyWrongSecret_ShouldReturnUnauthorized()
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("Developer:wrong-secret"));
        var client = CreateClientWithAuthorization(
            new AuthenticationHeaderValue(BasicAuthenticationHandler.SchemeName, credentials)
        );

        var response = await Execute(client, Operation.Search, NewOrganisationId());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // AUTH-04
    [Theory]
    [InlineData(Operation.Search)]
    [InlineData(Operation.Get)]
    [InlineData(Operation.PutOrganisation)]
    [InlineData(Operation.PutRegistration)]
    [InlineData(Operation.DeleteRegistration)]
    public async Task WhenIntegrationTestOAuth_ShouldSucceed(Operation operation)
    {
        var client = CreateOAuthClient();
        var id = NewOrganisationId();
        await Provision(client, operation, id);

        var response = await Execute(client, operation, id);

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    // AUTH-05
    [Theory]
    [InlineData("Negotiate", "Developer:developer-pwd")] // unknown scheme
    [InlineData(BasicAuthenticationHandler.SchemeName, "UnknownClient:some-secret")] // unknown client id
    [InlineData(BasicAuthenticationHandler.SchemeName, "no-colon-credential")] // malformed credential
    public async Task WhenCredentialsMalformed_ShouldReturnUnauthorized(string scheme, string plaintext)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));
        var client = CreateClientWithAuthorization(new AuthenticationHeaderValue(scheme, credentials));

        var response = await Execute(client, Operation.Search, NewOrganisationId());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

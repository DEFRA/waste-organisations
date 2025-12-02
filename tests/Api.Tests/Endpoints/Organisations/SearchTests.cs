using System.Net;
using Api.Authentication;
using AwesomeAssertions;

namespace Api.Tests.Endpoints.Organisations;

public class SearchTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task WhenNoOrganisations_ShouldBeOk()
    {
        var client = CreateClient();

        var response = await client.GetStringAsync(
            Testing.Endpoints.Organisations.Search(),
            TestContext.Current.CancellationToken
        );

        await VerifyJson(response).DontScrubGuids();
    }

    [Fact]
    public async Task WhenNoOrganisations_AndOAuth_ShouldBeOk()
    {
        var client = CreateClient(clientType: AclOptions.ClientType.OAuth);

        var response = await client.GetStringAsync(
            Testing.Endpoints.Organisations.Search(),
            TestContext.Current.CancellationToken
        );

        await VerifyJson(response).DontScrubGuids();
    }

    [Fact]
    public async Task WhenNoAuthenticatedUser_ShouldBeUnauthorized()
    {
        var client = CreateClient(addAuthorizationHeader: false);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Search(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WhenNoAuthorizedUser_ShouldBeForbidden()
    {
        var client = CreateClient(testUser: TestUser.WriteOnly);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Search(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

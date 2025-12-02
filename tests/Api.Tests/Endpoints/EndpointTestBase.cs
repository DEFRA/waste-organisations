using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Api.Authentication;
using Testing;

namespace Api.Tests.Endpoints;

public class EndpointTestBase : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    protected EndpointTestBase(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    {
        _factory = factory;
        _factory.OutputHelper = outputHelper;
    }

    protected HttpClient CreateClient(
        bool addAuthorizationHeader = true,
        TestUser testUser = TestUser.ReadWrite,
        AclOptions.ClientType clientType = AclOptions.ClientType.ApiKey
    )
    {
        var client = _factory.CreateClient();

        if (!addAuthorizationHeader)
            return client;

        var clientName = GenerateClientName(testUser, clientType);

        // See appsettings.IntegrationTests.json for the client configuration below

        client.DefaultRequestHeaders.Authorization = clientType switch
        {
            AclOptions.ClientType.ApiKey => new AuthenticationHeaderValue(
                BasicAuthenticationHandler.SchemeName,
                Convert.ToBase64String(
                    testUser switch
                    {
                        TestUser.ReadOnly => Encoding.UTF8.GetBytes($"{clientName}:integration-test-read"),
                        TestUser.WriteOnly => Encoding.UTF8.GetBytes($"{clientName}:integration-test-write"),
                        _ => Encoding.UTF8.GetBytes($"{clientName}:integration-test-readwrite"),
                    }
                )
            ),
            AclOptions.ClientType.OAuth => new AuthenticationHeaderValue(
                JwtAuthenticationHandler.SchemeName,
                GenerateJwt(clientName)
            ),
            _ => client.DefaultRequestHeaders.Authorization,
        };

        return client;
    }

    private static string GenerateClientName(TestUser testUser, AclOptions.ClientType clientType)
    {
        const string prefix = "IntegrationTest";

        return testUser switch
        {
            TestUser.ReadOnly => $"{prefix}-{clientType}-Read",
            TestUser.WriteOnly => $"{prefix}-{clientType}-Write",
            _ => $"{prefix}-{clientType}-ReadWrite",
        };
    }

    private static string GenerateJwt(string clientId)
    {
        var claims = new[] { new Claim(Claims.ClientId, clientId) };

        return Jwt.GenerateJwt(claims);
    }

    protected enum TestUser
    {
        ReadWrite,
        ReadOnly,
        WriteOnly,
    }
}

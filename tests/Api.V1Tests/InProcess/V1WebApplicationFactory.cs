using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Defra.WasteOrganisations.Api.Authentication;
using Defra.WasteOrganisations.Testing;
using MartinCostello.Logging.XUnit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Defra.WasteOrganisations.Api.V1Tests.InProcess;

public class V1WebApplicationFactory : WebApplicationFactory<Program>, ITestOutputHelperAccessor
{
    public ITestOutputHelper? OutputHelper { get; set; }

    public enum TestUser
    {
        ReadOnly,
        WriteOnly,
        ReadWrite,
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(config => config.AddXUnit(this));
        builder.UseSetting("integrationTest", "true");
        builder.UseEnvironment("IntegrationTests");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(_ => { });

        return base.CreateHost(builder);
    }

    public HttpClient CreateClient(
        TestUser testUser,
        AclOptions.ClientType clientType,
        Action<IServiceCollection> configureServices
    )
    {
        var client = WithWebHostBuilder(builder => builder.ConfigureTestServices(configureServices)).CreateClient();

        client.DefaultRequestHeaders.Authorization = AuthorizationHeader(testUser, clientType);

        return client;
    }

    private static AuthenticationHeaderValue AuthorizationHeader(TestUser testUser, AclOptions.ClientType clientType)
    {
        // See appsettings.IntegrationTests.json for the client configuration below
        var clientName = $"IntegrationTest-{clientType}-{ScopeSuffix(testUser)}";

        return clientType switch
        {
            AclOptions.ClientType.ApiKey => new AuthenticationHeaderValue(
                BasicAuthenticationHandler.SchemeName,
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientName}:{Secret(testUser)}"))
            ),
            AclOptions.ClientType.OAuth => new AuthenticationHeaderValue(
                JwtAuthenticationHandler.SchemeName,
                Jwt.GenerateJwt([new Claim(Claims.ClientId, clientName)])
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(clientType)),
        };
    }

    private static string ScopeSuffix(TestUser testUser) =>
        testUser switch
        {
            TestUser.ReadOnly => "Read",
            TestUser.WriteOnly => "Write",
            _ => "ReadWrite",
        };

    private static string Secret(TestUser testUser) =>
        testUser switch
        {
            TestUser.ReadOnly => "integration-test-read",
            TestUser.WriteOnly => "integration-test-write",
            _ => "integration-test-readwrite",
        };
}

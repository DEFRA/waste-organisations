using System.Net;
using Api.Services;
using AutoFixture;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Testing.Fixtures;

namespace Api.Tests.Endpoints.Organisations;

public class GetTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IOrganisationService MockOrganisationService { get; } = Substitute.For<IOrganisationService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        services.AddTransient<IOrganisationService>(sp => MockOrganisationService);
    }

    [Fact]
    public async Task WhenOrganisationFound_ShouldBeOk()
    {
        var client = CreateClient();
        var id = new Guid("b6f76437-65b6-4ed2-a7d5-c50e9af76201");
        MockOrganisationService
            .Get(id, Arg.Any<CancellationToken>())
            .Returns(OrganisationEntityFixtures.Default().With(x => x.Id, id).Create());

        var response = await client.GetStringAsync(
            Testing.Endpoints.Organisations.Get(id),
            TestContext.Current.CancellationToken
        );

        await VerifyJson(response).DontScrubGuids();
    }

    [Fact]
    public async Task WhenOrganisationNotFound_ShouldBeNotFound()
    {
        var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Get(Guid.Empty.ToString()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

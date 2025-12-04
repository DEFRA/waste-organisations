using System.Net;
using System.Net.Http.Json;
using Api.Mapping;
using Api.Services;
using AutoFixture;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Testing.Fixtures;
using Organisation = Api.Data.Entities.Organisation;

namespace Api.Tests.Endpoints.Organisations;

public class PutTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IOrganisationService MockOrganisationService { get; } = Substitute.For<IOrganisationService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        services.AddTransient<IOrganisationService>(sp => MockOrganisationService);
    }

    [Fact]
    public async Task WhenNoOrganisation_ShouldCreate()
    {
        var client = CreateClient();
        var id = new Guid("26647e8d-176e-440e-b7e4-75a9252cbd4b");
        var request = OrganisationRegistrationDtoFixtures.Default().Create();
        MockOrganisationService
            .Create(Arg.Any<Organisation>(), Arg.Any<CancellationToken>())
            .Returns(request.ToEntity(id));

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id.ToString()),
            request,
            TestContext.Current.CancellationToken
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task WhenOrganisation_ShouldUpdate()
    {
        var client = CreateClient();
        var id = new Guid("26647e8d-176e-440e-b7e4-75a9252cbd4b");
        var request = OrganisationRegistrationDtoFixtures.Default().Create();
        MockOrganisationService
            .Get(id, Arg.Any<CancellationToken>())
            .Returns(
                OrganisationEntityFixtures.Organisation().With(x => x.Id, id).With(x => x.Registrations, []).Create()
            );
        MockOrganisationService
            .Update(Arg.Any<Organisation>(), Arg.Any<CancellationToken>())
            .Returns<Organisation>(args => (Organisation)args[0]);

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id.ToString()),
            request,
            TestContext.Current.CancellationToken
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // Still need request/payload validation for things like enums etc
    // Need auto fixture to thin out type creation
}

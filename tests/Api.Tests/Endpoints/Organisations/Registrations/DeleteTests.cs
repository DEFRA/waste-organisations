using System.Net;
using Api.Dtos;
using Api.Extensions;
using Api.Services;
using AutoFixture;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Testing.Fixtures;
using Organisation = Api.Data.Entities.Organisation;

namespace Api.Tests.Endpoints.Organisations.Registrations;

public class DeleteTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IOrganisationService MockOrganisationService { get; } = Substitute.For<IOrganisationService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        services.AddTransient<IOrganisationService>(_ => MockOrganisationService);
    }

    [Theory]
    [InlineData("UNKNOWN", "2025")]
    [InlineData("SMALL_PRODUCER", "2022")]
    [InlineData("SMALL_PRODUCER", "2051")]
    public async Task WhenInvalidRoute_ShouldBeBadRequest(string type, string registrationYear)
    {
        var client = CreateClient();

        var response = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(OrganisationData.Id, type, registrationYear),
            TestContext.Current.CancellationToken
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
    }

    [Fact]
    public async Task WhenOrganisationNotFound_ShouldBeNotFound()
    {
        var client = CreateClient();
        MockOrganisationService
            .Get(OrganisationData.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Organisation?>(null));

        var response = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(
                OrganisationData.Id,
                RegistrationType.SmallProducer.ToJsonValue(),
                "2025"
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenOrganisationFound_AndRegistrationNotFound_ShouldBeNotFound()
    {
        var client = CreateClient();
        MockOrganisationService
            .Get(OrganisationData.Id, Arg.Any<CancellationToken>())
            .Returns(OrganisationEntityFixtures.Default().With(x => x.Id, OrganisationData.Id).Create());

        var response = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(
                OrganisationData.Id,
                RegistrationType.SmallProducer.ToJsonValue(),
                "2026"
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenOrganisationFound_AndRegistrationFound_ShouldBeDeleted()
    {
        var client = CreateClient();
        var remaining = RegistrationEntityFixtures.Default().With(x => x.RegistrationYear, 2026).Create();
        MockOrganisationService
            .Get(OrganisationData.Id, Arg.Any<CancellationToken>())
            .Returns(
                OrganisationEntityFixtures
                    .Default()
                    .With(x => x.Id, OrganisationData.Id)
                    .With(x => x.Registrations, [RegistrationEntityFixtures.Default().Create(), remaining])
                    .Create()
            );
        Organisation? organisation = null;
        MockOrganisationService
            .Update(Arg.Any<Organisation>(), Arg.Any<CancellationToken>())
            .Returns<Organisation>(args =>
            {
                organisation = (Organisation)args[0];
                return organisation;
            });

        var response = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(
                OrganisationData.Id,
                RegistrationType.SmallProducer.ToJsonValue(),
                "2025"
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        organisation.Should().NotBeNull();
        organisation.Registrations.Should().BeEquivalentTo([remaining]);
    }
}

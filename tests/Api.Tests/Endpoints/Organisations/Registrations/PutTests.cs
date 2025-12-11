using System.Net;
using System.Net.Http.Json;
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

public class PutTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
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

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(OrganisationData.Id, type, registrationYear),
            new RegistrationRequest { Status = RegistrationStatus.Registered },
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

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(
                OrganisationData.Id,
                RegistrationType.SmallProducer.ToJsonValue(),
                "2025"
            ),
            new RegistrationRequest { Status = RegistrationStatus.Registered },
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task WhenOrganisationFound_AndRegistrationDoesNotExist_ShouldBeCreated()
    {
        var client = CreateClient();
        MockOrganisationService
            .Get(OrganisationData.Id, Arg.Any<CancellationToken>())
            .Returns(
                OrganisationEntityFixtures
                    .Default()
                    .With(x => x.Id, OrganisationData.Id)
                    .With(x => x.Registrations, [RegistrationEntityFixtures.Default().Create()])
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

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(
                OrganisationData.Id,
                RegistrationType.SmallProducer.ToJsonValue(),
                "2026"
            ),
            new RegistrationRequest { Status = RegistrationStatus.Registered },
            TestContext.Current.CancellationToken
        );

        organisation.Should().NotBeNull();
        organisation.Registrations.Length.Should().Be(2);
        organisation
            .Registrations.Should()
            .ContainEquivalentOf(
                RegistrationEntityFixtures
                    .Default()
                    .With(x => x.Type, RegistrationType.SmallProducer.ToJsonValue())
                    .With(x => x.RegistrationYear, 2026)
                    .With(x => x.Status, RegistrationStatus.Registered.ToJsonValue())
                    .Create()
            );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response
            .Headers.Location.Should()
            .Be($"/organisations/{OrganisationData.Id}/registrations/SMALL_PRODUCER-2026");

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        await VerifyJson(content);
    }

    [Fact]
    public async Task WhenOrganisationFound_AndRegistrationExists_ShouldBeUpdated()
    {
        var client = CreateClient();
        MockOrganisationService
            .Get(OrganisationData.Id, Arg.Any<CancellationToken>())
            .Returns(
                OrganisationEntityFixtures
                    .Default()
                    .With(x => x.Id, OrganisationData.Id)
                    .With(x => x.Registrations, [RegistrationEntityFixtures.Default().Create()])
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

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(
                OrganisationData.Id,
                RegistrationType.SmallProducer.ToJsonValue(),
                "2025"
            ),
            new RegistrationRequest { Status = RegistrationStatus.Cancelled },
            TestContext.Current.CancellationToken
        );

        organisation.Should().NotBeNull();
        organisation.Registrations.Length.Should().Be(1);
        organisation
            .Registrations.Should()
            .BeEquivalentTo([
                RegistrationEntityFixtures
                    .Default()
                    .With(x => x.Type, RegistrationType.SmallProducer.ToJsonValue())
                    .With(x => x.RegistrationYear, 2025)
                    .With(x => x.Status, RegistrationStatus.Cancelled.ToJsonValue())
                    .Create(),
            ]);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        await VerifyJson(content);
    }
}

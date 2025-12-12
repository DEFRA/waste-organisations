using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Mapping;
using Defra.WasteOrganisations.Api.Services;
using Defra.WasteOrganisations.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Organisation = Defra.WasteOrganisations.Api.Data.Entities.Organisation;

namespace Defra.WasteOrganisations.Api.Tests.Endpoints.Organisations;

public class PutTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IOrganisationService MockOrganisationService { get; } = Substitute.For<IOrganisationService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        services.AddTransient<IOrganisationService>(_ => MockOrganisationService);
    }

    [Fact]
    public async Task WhenNoOrganisation_ShouldCreate()
    {
        var client = CreateClient();
        var request = OrganisationRegistrationDtoFixtures.Default().Create();
        MockOrganisationService
            .Get(OrganisationData.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Organisation?>(null));
        MockOrganisationService
            .Create(Arg.Is<Organisation>(x => x.Id == OrganisationData.Id), Arg.Any<CancellationToken>())
            .Returns(request.ToEntity(OrganisationData.Id));

        var response = await client.PutAsJsonAsync(
            Defra.WasteOrganisations.Testing.Endpoints.Organisations.Put(OrganisationData.Id),
            request,
            TestContext.Current.CancellationToken
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData(2023)]
    [InlineData(2050)]
    public async Task WhenNoOrganisation_AndRegistrationYearOnBoundary_ShouldCreate(int registrationYear)
    {
        var client = CreateClient();
        var request = OrganisationRegistrationDtoFixtures
            .Default()
            .With(
                x => x.Registration,
                RegistrationDtoFixtures.Default().With(x => x.RegistrationYear, registrationYear).Create()
            )
            .Create();
        MockOrganisationService
            .Get(OrganisationData.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Organisation?>(null));
        MockOrganisationService
            .Create(Arg.Is<Organisation>(x => x.Id == OrganisationData.Id), Arg.Any<CancellationToken>())
            .Returns(request.ToEntity(OrganisationData.Id));

        var response = await client.PutAsJsonAsync(
            Defra.WasteOrganisations.Testing.Endpoints.Organisations.Put(OrganisationData.Id),
            request,
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().Be($"/organisations/{OrganisationData.Id}");
    }

    [Fact]
    public async Task WhenOrganisation_ShouldUpdate()
    {
        var client = CreateClient();
        var request = OrganisationRegistrationDtoFixtures.Default().Create();
        MockOrganisationService
            .Get(OrganisationData.Id, Arg.Any<CancellationToken>())
            .Returns(
                OrganisationEntityFixtures
                    .Organisation()
                    .With(x => x.Id, OrganisationData.Id)
                    .With(x => x.Registrations, [])
                    .Create()
            );
        MockOrganisationService
            .Update(Arg.Any<Organisation>(), Arg.Any<CancellationToken>())
            .Returns<Organisation>(args => (Organisation)args[0]);

        var response = await client.PutAsJsonAsync(
            Defra.WasteOrganisations.Testing.Endpoints.Organisations.Put(OrganisationData.Id),
            request,
            TestContext.Current.CancellationToken
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WhenInvalidRequest_BusinessCountryIsInvalid_ShouldNotCreate()
    {
        var client = CreateClient();

        var response = await client.PutAsJsonAsync(
            Defra.WasteOrganisations.Testing.Endpoints.Organisations.Put(Guid.NewGuid()),
            new { BusinessCountry = "Invalid" },
            TestContext.Current.CancellationToken
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
    }

    [Fact]
    public async Task WhenInvalidRequest_RegistrationIsInvalid_ShouldNotCreate()
    {
        var client = CreateClient();

        var response = await client.PutAsJsonAsync(
            Defra.WasteOrganisations.Testing.Endpoints.Organisations.Put(Guid.NewGuid()),
            new { Address = new { }, Registration = new { } },
            TestContext.Current.CancellationToken
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
    }

    [Fact]
    public async Task WhenInvalidRequest_RegistrationStatusIsInvalid_ShouldNotCreate()
    {
        var client = CreateClient();

        var response = await client.PutAsJsonAsync(
            Defra.WasteOrganisations.Testing.Endpoints.Organisations.Put(Guid.NewGuid()),
            new { Address = new { }, Registration = new { Status = "Invalid" } },
            TestContext.Current.CancellationToken
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
    }

    [Fact]
    public async Task WhenInvalidRequest_RegistrationTypeIsInvalid_ShouldNotCreate()
    {
        var client = CreateClient();

        var response = await client.PutAsJsonAsync(
            Defra.WasteOrganisations.Testing.Endpoints.Organisations.Put(Guid.NewGuid()),
            new { Address = new { }, Registration = new { Type = "Invalid" } },
            TestContext.Current.CancellationToken
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
    }

    [Theory]
    [InlineData(2022)]
    [InlineData(2051)]
    public async Task WhenInvalidRequest_RegistrationYearIsInvalid_ShouldNotCreate(int registrationYear)
    {
        var client = CreateClient();

        var response = await client.PutAsJsonAsync(
            Defra.WasteOrganisations.Testing.Endpoints.Organisations.Put(Guid.NewGuid()),
            OrganisationRegistrationDtoFixtures
                .Default()
                .With(
                    x => x.Registration,
                    RegistrationDtoFixtures.Default().With(x => x.RegistrationYear, registrationYear).Create()
                )
                .Create(),
            TestContext.Current.CancellationToken
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).IgnoreParameters().DontScrubGuids();
    }
}

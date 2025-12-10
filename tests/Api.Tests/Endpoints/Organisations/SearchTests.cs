using System.Net;
using Api.Authentication;
using Api.Dtos;
using Api.Extensions;
using Api.Services;
using AutoFixture;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Testing;
using Testing.Fixtures;

namespace Api.Tests.Endpoints.Organisations;

public class SearchTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IOrganisationService MockOrganisationService { get; } = Substitute.For<IOrganisationService>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        services.AddTransient<IOrganisationService>(_ => MockOrganisationService);
    }

    [Fact]
    public async Task WhenNoOrganisations_ShouldBeOk()
    {
        var client = CreateClient();
        MockOrganisationService
            .Search(
                Arg.Any<List<RegistrationType>>(),
                Arg.Any<List<int>>(),
                Arg.Any<List<RegistrationStatus>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns([]);

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
        MockOrganisationService
            .Search(
                Arg.Any<List<RegistrationType>>(),
                Arg.Any<List<int>>(),
                Arg.Any<List<RegistrationStatus>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns([]);

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Search(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
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

    [Fact]
    public async Task WhenInvalidRegistrations_ShouldBeBadRequest()
    {
        var client = CreateClient();
        var requestUri = Testing.Endpoints.Organisations.Search(
            EndpointQuery.New.Where(
                EndpointFilter.Registrations($"INVALID,INVALID_2,{RegistrationType.LargeProducer.ToJsonValue()}")
            )
        );

        var response = await client.GetAsync(requestUri, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
    }

    [Fact]
    public async Task WhenInvalidRegistrationYears_ShouldBeBadRequest()
    {
        var client = CreateClient();
        var requestUri = Testing.Endpoints.Organisations.Search(
            EndpointQuery.New.Where(EndpointFilter.RegistrationYears("INVALID,INVALID_2,2025,2022,2023,2050,2051"))
        );

        var response = await client.GetAsync(requestUri, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
    }

    [Fact]
    public async Task WhenInvalidStatuses_ShouldBeBadRequest()
    {
        var client = CreateClient();
        var requestUri = Testing.Endpoints.Organisations.Search(
            EndpointQuery.New.Where(
                EndpointFilter.Statuses($"INVALID,INVALID_2,{RegistrationStatus.Registered.ToJsonValue()}")
            )
        );

        var response = await client.GetAsync(requestUri, TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
    }

    [Fact]
    public async Task WhenOrganisations_ShouldBeOk()
    {
        var client = CreateClient();
        RegistrationType[] registrationsTypes = [RegistrationType.LargeProducer, RegistrationType.SmallProducer];
        int[] registrationYears = [2024, 2025];
        RegistrationStatus[] registrationStatuses = [RegistrationStatus.Registered];
        var requestUri = Testing.Endpoints.Organisations.Search(
            EndpointQuery
                .New.Where(EndpointFilter.Registrations(registrationsTypes))
                .Where(EndpointFilter.RegistrationYears(registrationYears))
                .Where(EndpointFilter.Statuses(registrationStatuses))
        );
        MockOrganisationService
            .Search(
                Arg.Is<List<RegistrationType>>(x => x.SequenceEqual(registrationsTypes)),
                Arg.Is<List<int>>(x => x.SequenceEqual(registrationYears)),
                Arg.Is<List<RegistrationStatus>>(x => x.SequenceEqual(registrationStatuses)),
                Arg.Any<CancellationToken>()
            )
            .Returns([
                OrganisationEntityFixtures
                    .Default()
                    .With(x => x.Id, new Guid("d9a5615a-ebe8-4486-a057-f1aa9b6746fc"))
                    .Create(),
            ]);

        var response = await client.GetStringAsync(requestUri, TestContext.Current.CancellationToken);

        await VerifyJson(response).DontScrubGuids();
    }
}

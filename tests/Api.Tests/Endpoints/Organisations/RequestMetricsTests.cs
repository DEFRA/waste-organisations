using System.Net;
using Api.Data.Entities;
using Api.Services;
using Api.Utils.Metrics;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Api.Tests.Endpoints.Organisations;

public class RequestMetricsTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    private IOrganisationService MockOrganisationService { get; } = Substitute.For<IOrganisationService>();
    private IRequestMetrics MockRequestMetrics { get; } = Substitute.For<IRequestMetrics>();

    protected override void ConfigureTestServices(IServiceCollection services)
    {
        base.ConfigureTestServices(services);

        services.AddTransient<IOrganisationService>(_ => MockOrganisationService);
        services.AddTransient<IRequestMetrics>(_ => MockRequestMetrics);
    }

    [Fact]
    public async Task WhenOrganisationServiceThrows_ShouldNotBeSwallowed_AndReportFault()
    {
        var client = CreateClient();
        MockOrganisationService.Get(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Throws(new Exception("BOOM!"));

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Get(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        MockRequestMetrics
            .Received(1)
            .RequestFaulted(
                "/organisations/{id:guid}",
                "GET",
                StatusCodes.Status200OK,
                Arg.Is<Exception>(x => x.Message == "BOOM!")
            );
        MockRequestMetrics
            .Received(1)
            .RequestCompleted("/organisations/{id:guid}", "GET", StatusCodes.Status200OK, Arg.Is<double>(x => x > 0));
    }

    [Fact]
    public async Task WhenOrganisationServiceSucceeds_ShouldNotReportFault()
    {
        var client = CreateClient();
        MockOrganisationService
            .Get(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Organisation?>(null));

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Get(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        MockRequestMetrics
            .DidNotReceiveWithAnyArgs()
            .RequestFaulted(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<Exception>());
        MockRequestMetrics
            .Received(1)
            .RequestCompleted(
                "/organisations/{id:guid}",
                "GET",
                StatusCodes.Status404NotFound,
                Arg.Is<double>(x => x > 0)
            );
    }
}

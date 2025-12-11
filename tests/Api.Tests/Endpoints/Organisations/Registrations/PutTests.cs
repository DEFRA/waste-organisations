using System.Net;
using System.Net.Http.Json;
using Api.Dtos;
using Api.Extensions;
using AwesomeAssertions;

namespace Api.Tests.Endpoints.Organisations.Registrations;

public class PutTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task WhenRegistration_ShouldUpdate()
    {
        var client = CreateClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(
                OrganisationData.Id,
                RegistrationType.LargeProducer.ToJsonValue(),
                "2025"
            ),
            new RegistrationRequest { Status = RegistrationStatus.Cancelled },
            TestContext.Current.CancellationToken
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task WhenInvalidRoute_ShouldBeBadRequest()
    {
        var client = CreateClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPut(OrganisationData.Id, "UNKNOWN", "2025"),
            new RegistrationRequest { Status = RegistrationStatus.Registered },
            TestContext.Current.CancellationToken
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
    }
}

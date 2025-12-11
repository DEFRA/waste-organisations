using System.Net;
using Api.Dtos;
using Api.Extensions;
using AwesomeAssertions;

namespace Api.Tests.Endpoints.Organisations.Registrations;

public class DeleteTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task WhenRegistration_ShouldDelete()
    {
        var client = CreateClient();

        var response = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(
                OrganisationData.Id,
                RegistrationType.LargeProducer.ToJsonValue(),
                "2025"
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
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
}

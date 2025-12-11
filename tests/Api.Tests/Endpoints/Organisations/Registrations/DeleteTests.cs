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
}

using System.Net;
using Api.Dtos;
using AwesomeAssertions;
using Testing;

namespace Api.Tests.Endpoints.Registrations;

public class DeleteTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task WhenRegistration_ShouldDelete()
    {
        var client = CreateClient();
        var id = new Guid("26647e8d-176e-440e-b7e4-75a9252cbd4b").ToString();

        var response = await client.DeleteAsync(
            Testing.Endpoints.Organisations.RegistrationsDelete(
                id,
                RegistrationType.LargeProducer.ToJsonValue(),
                "2025"
            ),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

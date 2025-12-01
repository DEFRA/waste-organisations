using System.Net;
using System.Net.Http.Json;
using Api.Dtos;
using AwesomeAssertions;
using Testing;

namespace Api.Tests.Endpoints.Organisations.Registrations;

public class PostTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task WhenNoRegistration_ShouldCreate()
    {
        var client = CreateClient();
        var id = new Guid("26647e8d-176e-440e-b7e4-75a9252cbd4b").ToString();

        var response = await client.PostAsJsonAsync(
            Testing.Endpoints.Organisations.RegistrationsPost(id, RegistrationType.LargeProducer.ToJsonValue(), "2025"),
            RegistrationStatus.Registered,
            TestContext.Current.CancellationToken
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

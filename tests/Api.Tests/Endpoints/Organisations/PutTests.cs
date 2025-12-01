using System.Net;
using System.Net.Http.Json;
using Api.Dtos;
using AwesomeAssertions;

namespace Api.Tests.Endpoints.Organisations;

public class PutTests(ApiWebApplicationFactory factory, ITestOutputHelper outputHelper)
    : EndpointTestBase(factory, outputHelper)
{
    [Fact]
    public async Task WhenNoOrganisation_ShouldCreate()
    {
        var client = CreateClient();
        var id = new Guid("26647e8d-176e-440e-b7e4-75a9252cbd4b").ToString();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            new OrganisationRegistration
            {
                Name = "name",
                Address = new Address(),
                Registration = new Registration
                {
                    Status = RegistrationStatus.Registered,
                    Type = RegistrationType.SmallProducer,
                },
            },
            TestContext.Current.CancellationToken
        );
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(content).DontScrubGuids();
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

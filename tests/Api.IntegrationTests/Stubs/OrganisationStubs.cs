using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Testing.Fixtures;
using Xunit.Internal;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Stubs;

public class OrganisationStubs : MongoTestBase
{
    private static string Destination => Path.Combine("..", "..", "..", "Stubs", "Generated");

    [Fact]
    public async Task GenerateStubs()
    {
        Directory.CreateDirectory(Destination);
        Directory.GetFiles(Destination).ForEach(File.Delete);

        await LargeProducer();
        await ComplianceScheme();
    }

    private static async Task LargeProducer()
    {
        var client = CreateClient();
        var id = new Guid("9d3c4d0f-8e5a-4b91-9f7a-2e8d6a1c5f42");

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.LargeProducer().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await WriteResponse(response, $"_organisations_{id}.json");
    }

    private static async Task ComplianceScheme()
    {
        var client = CreateClient();
        var id = new Guid("c71b2e84-3f9d-47aa-a8c6-5b4ef0139d8e");

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.ComplianceScheme().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await WriteResponse(response, $"_organisations_{id}.json");
    }

    private static async Task WriteResponse(HttpResponseMessage response, string fileName)
    {
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(content);

        await File.WriteAllTextAsync(
            Path.Combine(Destination, fileName),
            JsonSerializer.Serialize(
                document,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                }
            ),
            TestContext.Current.CancellationToken
        );
    }
}

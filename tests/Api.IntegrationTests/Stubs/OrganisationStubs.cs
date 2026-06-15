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
    private static readonly int[] s_years = [2026, 2027, 2028, 2029, 2030];

    [Fact]
    public async Task GenerateStubs()
    {
        Directory.CreateDirectory(Destination);
        Directory.GetFiles(Destination).ForEach(File.Delete);

        await LargeProducer(new Guid("9d3c4d0f-8e5a-4b91-9f7a-2e8d6a1c5f42"));
        await ComplianceScheme(new Guid("c71b2e84-3f9d-47aa-a8c6-5b4ef0139d8e"));

        // Organisations used in epr-local-environment
        await LargeProducer(new Guid("e2316c5e-d434-41da-8274-494dc0762d20"));
        await ComplianceScheme(new Guid("94bfc917-b9b6-45d7-847b-e5f500bfe198"));
    }

    private static async Task LargeProducer(Guid id)
    {
        var client = CreateClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.LargeProducer().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        foreach (var year in s_years)
        {
            response = await client.PutAsJsonAsync(
                Testing.Endpoints.Organisations.Put(id),
                OrganisationRegistrationDtoFixtures
                    .LargeProducer()
                    .With(
                        x => x.Registration,
                        RegistrationDtoFixtures.LargeProducer().With(x => x.RegistrationYear, year).Create()
                    )
                    .Create(),
                TestContext.Current.CancellationToken
            );

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await WriteResponse(response, $"_organisations_{id}.json");
    }

    private static async Task ComplianceScheme(Guid id)
    {
        var client = CreateClient();

        var response = await client.PutAsJsonAsync(
            Testing.Endpoints.Organisations.Put(id),
            OrganisationRegistrationDtoFixtures.ComplianceScheme().Create(),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        foreach (var year in s_years)
        {
            response = await client.PutAsJsonAsync(
                Testing.Endpoints.Organisations.Put(id),
                OrganisationRegistrationDtoFixtures
                    .LargeProducer()
                    .With(
                        x => x.Registration,
                        RegistrationDtoFixtures.ComplianceScheme().With(x => x.RegistrationYear, year).Create()
                    )
                    .Create(),
                TestContext.Current.CancellationToken
            );

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

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

using System.Text.Json;
using AwesomeAssertions;

namespace Defra.WasteOrganisations.Api.V1Tests.InProcess;

[Trait("Category", "V1Baseline")]
public class OpenApiTests : IClassFixture<V1WebApplicationFactory>
{
    private readonly V1WebApplicationFactory _factory;

    public OpenApiTests(V1WebApplicationFactory factory, ITestOutputHelper outputHelper)
    {
        _factory = factory;
        _factory.OutputHelper = outputHelper;
    }

    private static readonly (string Path, string Method, int[] Statuses)[] Operations =
    [
        ("/organisations", "get", [200, 400, 500]),
        ("/organisations/{id}", "get", [200, 404, 500]),
        ("/organisations/{id}", "put", [200, 201, 400, 500]),
        ("/organisations/{id}/registrations/{type}-{registrationYear}", "put", [200, 201, 400, 500]),
        ("/organisations/{id}/registrations/{type}-{registrationYear}", "delete", [204, 404, 500]),
    ];

    // OPENAPI-01: the snapshot is the machine-readable v1 contract.
    [Fact]
    public async Task WhenFetchingV1Document_ShouldMatchSnapshot()
    {
        var document = await FetchDocument();

        await VerifyJson(document);
    }

    // OPENAPI-02: presence-only checks so new endpoints or optional fields never fail this test.
    [Fact]
    public async Task WhenParsingV1Document_ShouldContainV1Contract()
    {
        using var document = JsonDocument.Parse(await FetchDocument());
        var root = document.RootElement;
        var paths = root.GetProperty("paths");

        foreach (var (path, method, statuses) in Operations)
        {
            paths.TryGetProperty(path, out var pathItem).Should().BeTrue($"path {path} must be present");
            pathItem.TryGetProperty(method, out var operation).Should().BeTrue($"{method} {path} must be present");

            var responses = operation.GetProperty("responses");
            foreach (var status in statuses)
            {
                responses
                    .TryGetProperty(status.ToString(), out _)
                    .Should()
                    .BeTrue($"{method} {path} must document status {status}");
            }
        }

        var schemas = root.GetProperty("components").GetProperty("schemas");

        RequiredProperties(schemas, "OrganisationRegistration").Should().Contain(["name", "address", "registration"]);
        RequiredProperties(schemas, "RegistrationRequest").Should().Contain("status");

        EnumValues(schemas, "RegistrationType")
            .Should()
            .Contain(["SMALL_PRODUCER", "LARGE_PRODUCER", "COMPLIANCE_SCHEME", "REPROCESSOR", "EXPORTER"]);
        EnumValues(schemas, "RegistrationStatus").Should().Contain(["REGISTERED", "CANCELLED"]);
        EnumValues(schemas, "BusinessCountry").Should().Contain(["GB-ENG", "GB-NIR", "GB-SCT", "GB-WLS"]);
    }

    private async Task<string> FetchDocument()
    {
        var client = _factory.CreateClient();

        return await client.GetStringAsync(Testing.Endpoints.OpenApi.V1, TestContext.Current.CancellationToken);
    }

    private static IEnumerable<string> RequiredProperties(JsonElement schemas, string schema) =>
        StringValues(schemas.GetProperty(schema).GetProperty("required"));

    private static IEnumerable<string> EnumValues(JsonElement schemas, string schema) =>
        StringValues(schemas.GetProperty(schema).GetProperty("enum"));

    private static IEnumerable<string> StringValues(JsonElement array) =>
        array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!);
}

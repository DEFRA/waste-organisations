using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Dtos;

namespace Defra.WasteOrganisations.Api.IntegrationTests.Scenarios;

// The v2 create's responses that are not a validation 400, against the Compose stack: bodies the
// API cannot read, the status codes of authentication and versioning, and the proof that a
// created organisation goes to Postgres only (spec 0005, Tests)
public class CreateOrganisationErrorTests : IntegrationTestBase
{
    private const string MediaType = "application/vnd.defra.waste-organisations+json";
    private const string AcceptV2 = $"{MediaType}; version=2";

    private const string ValidBody = """
        {"organisation":{"name":"Acme Waste Ltd","companiesHouseNumber":"12345678","regulatorNation":"GB-ENG","organisationType":"LTD","addresses":[{"addressTypes":["REGISTERED"],"buildingNumber":"12","street":"High Street","locality":"Testville","postalCode":"AB1 2CD","countryIso":"GB"}]}}
        """;

    // Each body fails JSON binding, so the request never reaches validation and the existing
    // exception handler answers with a `detail`, as v1 does, rather than `errors`
    [Theory]
    [InlineData("UnknownAddressType", "\"addressTypes\":[\"REGISTERED\"]", "\"addressTypes\":[\"HEAD_OFFICE\"]")]
    [InlineData("AddressTypesIsString", "\"addressTypes\":[\"REGISTERED\"]", "\"addressTypes\":\"REGISTERED\"")]
    [InlineData("NameIsNumber", "\"name\":\"Acme Waste Ltd\"", "\"name\":42")]
    [InlineData("MalformedJson", "}}", "")]
    public async Task UnreadableBody_ShouldBeBadRequestWithDetail(string scenario, string replace, string with)
    {
        ValidBody.Should().Contain(replace);

        await ShouldBeBadRequestWithDetail(ValidBody.Replace(replace, with), scenario);
    }

    [Fact]
    public async Task EmptyBody_ShouldBeBadRequestWithDetail() =>
        await ShouldBeBadRequestWithDetail(string.Empty, "EmptyBody");

    [Fact]
    public async Task ContentTypeNotJson_ShouldBeUnsupportedMediaType()
    {
        var response = await PostAsync(new StringContent(ValidBody, Encoding.UTF8, "text/plain"));

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    // Empty bodies, as v1 (spec 0001, X-BODY-01)
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    // See compose.yml: an OAuth client with the read scope only
    [InlineData("ReadOnlyOAuth", HttpStatusCode.Forbidden)]
    public async Task WithoutWriteAccess_ShouldBeRejectedWithEmptyBody(string? clientId, HttpStatusCode expected)
    {
        using var client = CreateClient(clientId);

        var response = await PostAsync(JsonBody(ValidBody), client: client);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(expected);
        content.Should().BeEmpty();
    }

    // v1 has no POST /organisations, so a request that does not ask for v2 keeps v1's 405
    // (spec 0004, X-ROUTE-01)
    [Theory]
    [InlineData(null)]
    [InlineData($"{MediaType}; version=1")]
    public async Task Version2NotRequested_ShouldBeMethodNotAllowed(string? accept)
    {
        var response = await PostAsync(JsonBody(ValidBody), accept);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        response.Content.Headers.Allow.Should().Equal("GET");
    }

    [Fact]
    public async Task Version3Requested_ShouldBeNotAcceptable()
    {
        var response = await PostAsync(JsonBody(ValidBody), $"{MediaType}; version=3");
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
        await VerifyJson(content);
    }

    // Nothing is written to MongoDB, so v1, which reads Mongo, does not find the new id
    [Fact]
    public async Task Created_ShouldNotBeFoundByV1Get()
    {
        var id = await CreateAsync();
        using var client = CreateClient();

        var response = await client.GetAsync(
            Testing.Endpoints.Organisations.Get(id),
            TestContext.Current.CancellationToken
        );

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // The accepted gap until REGEN-140 adds a v2 GET: `Location` cannot be followed with version=2
    [Fact]
    public async Task Created_ShouldNotBeReadableByV2GetYet()
    {
        var id = await CreateAsync();
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Testing.Endpoints.Organisations.Get(id));
        request.Headers.TryAddWithoutValidation("Accept", AcceptV2);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }

    private static async Task ShouldBeBadRequestWithDetail(string body, string scenario)
    {
        var response = await PostAsync(JsonBody(body));
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, content);
        var problem = JsonNode.Parse(content)!.AsObject();
        problem["detail"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        problem.ContainsKey("errors").Should().BeFalse();

        await VerifyJson(content).UseParameters(scenario);
    }

    // A valid request with an address unique to the call, as address rows are shared and never
    // cleaned up (see CreateOrganisationTests)
    private static async Task<Guid> CreateAsync()
    {
        var street = $"High Street {Guid.NewGuid():N}";
        var response = await PostAsync(JsonBody(ValidBody.Replace("\"High Street\"", $"\"{street}\"")));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (
            await response.Content.ReadFromJsonAsync<CreateOrganisationResponse>(TestContext.Current.CancellationToken)
        )!
            .Organisation
            .Id;
    }

    private static StringContent JsonBody(string body) => new(body, Encoding.UTF8, "application/json");

    private static async Task<HttpResponseMessage> PostAsync(
        HttpContent content,
        string? accept = AcceptV2,
        HttpClient? client = null
    )
    {
        using var ownClient = client is null ? CreateClient() : null;
        using var request = new HttpRequestMessage(HttpMethod.Post, Testing.Endpoints.Organisations.Post())
        {
            Content = content,
        };

        if (accept is not null)
            request.Headers.TryAddWithoutValidation("Accept", accept);

        return await (client ?? ownClient!).SendAsync(request, TestContext.Current.CancellationToken);
    }
}

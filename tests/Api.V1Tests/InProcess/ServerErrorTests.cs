using System.Net;
using System.Net.Http.Json;
using AutoFixture;
using AwesomeAssertions;
using Defra.WasteOrganisations.Api.Authentication;
using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using Defra.WasteOrganisations.Api.Services;
using Defra.WasteOrganisations.Testing.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Organisation = Defra.WasteOrganisations.Api.Data.Entities.Organisation;

namespace Defra.WasteOrganisations.Api.V1Tests.InProcess;

[Trait("Category", "V1Baseline")]
public class ServerErrorTests : IClassFixture<V1WebApplicationFactory>
{
    private static readonly Guid OrganisationId = Guid.NewGuid();
    private readonly V1WebApplicationFactory _factory;

    public ServerErrorTests(V1WebApplicationFactory factory, ITestOutputHelper outputHelper)
    {
        _factory = factory;
        _factory.OutputHelper = outputHelper;
    }

    public enum Operation
    {
        Search,
        Get,
        PutOrganisation,
        PutRegistration,
        DeleteRegistration,
    }

    private static string SmallProducerWire => RegistrationType.SmallProducer.ToJsonValue();
    private static string LargeProducerWire => RegistrationType.LargeProducer.ToJsonValue();

    [Theory]
    [InlineData(Operation.Search)] // S-500
    [InlineData(Operation.Get)] // S-500
    [InlineData(Operation.PutOrganisation)] // S-500
    [InlineData(Operation.PutRegistration)] // S-500
    [InlineData(Operation.DeleteRegistration)] // S-500
    public async Task WhenServiceThrows_ShouldReturnProblemDetails500(Operation operation)
    {
        var client = _factory.CreateClient(
            V1WebApplicationFactory.TestUser.ReadWrite,
            AclOptions.ClientType.ApiKey,
            ConfigureThrowingService
        );

        var response = await Execute(client, operation);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        await VerifyJson(body).IgnoreParameters();
    }

    private static void ConfigureThrowingService(IServiceCollection services)
    {
        var organisationService = Substitute.For<IOrganisationService>();

        organisationService.Get(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Throws(new InvalidOperationException());
        organisationService
            .Create(Arg.Any<Organisation>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException());
        organisationService
            .Update(Arg.Any<Organisation>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException());
        organisationService
            .Search(
                Arg.Any<List<RegistrationType>>(),
                Arg.Any<List<int>>(),
                Arg.Any<List<RegistrationStatus>>(),
                Arg.Any<CancellationToken>()
            )
            .Throws(new InvalidOperationException());

        services.AddTransient<IOrganisationService>(_ => organisationService);
    }

    private static Task<HttpResponseMessage> Execute(HttpClient client, Operation operation) =>
        operation switch
        {
            Operation.Search => client.GetAsync(
                Testing.Endpoints.Organisations.Search(),
                TestContext.Current.CancellationToken
            ),
            Operation.Get => client.GetAsync(
                Testing.Endpoints.Organisations.Get(OrganisationId),
                TestContext.Current.CancellationToken
            ),
            Operation.PutOrganisation => client.PutAsJsonAsync(
                Testing.Endpoints.Organisations.Put(OrganisationId),
                OrganisationRegistrationDtoFixtures.Default().Create(),
                TestContext.Current.CancellationToken
            ),
            Operation.PutRegistration => client.PutAsJsonAsync(
                Testing.Endpoints.Organisations.RegistrationsPut(OrganisationId, LargeProducerWire, "2026"),
                new RegistrationRequest { Status = RegistrationStatus.Registered },
                TestContext.Current.CancellationToken
            ),
            Operation.DeleteRegistration => client.DeleteAsync(
                Testing.Endpoints.Organisations.RegistrationsDelete(OrganisationId, SmallProducerWire, "2025"),
                TestContext.Current.CancellationToken
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
}

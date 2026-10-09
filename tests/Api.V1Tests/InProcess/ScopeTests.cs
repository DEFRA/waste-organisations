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
using Organisation = Defra.WasteOrganisations.Api.Data.Entities.Organisation;

namespace Defra.WasteOrganisations.Api.V1Tests.InProcess;

[Trait("Category", "V1Baseline")]
public class ScopeTests : IClassFixture<V1WebApplicationFactory>
{
    private static readonly Guid OrganisationId = Guid.NewGuid();
    private readonly V1WebApplicationFactory _factory;

    public ScopeTests(V1WebApplicationFactory factory, ITestOutputHelper outputHelper)
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

    // AUTH-06: read-only client reads Search and Get, and is forbidden on every write.
    [Theory]
    [InlineData(Operation.Search, AclOptions.ClientType.ApiKey, true)]
    [InlineData(Operation.Get, AclOptions.ClientType.ApiKey, true)]
    [InlineData(Operation.PutOrganisation, AclOptions.ClientType.ApiKey, false)]
    [InlineData(Operation.PutRegistration, AclOptions.ClientType.ApiKey, false)]
    [InlineData(Operation.DeleteRegistration, AclOptions.ClientType.ApiKey, false)]
    [InlineData(Operation.Search, AclOptions.ClientType.OAuth, true)]
    [InlineData(Operation.Get, AclOptions.ClientType.OAuth, true)]
    [InlineData(Operation.PutOrganisation, AclOptions.ClientType.OAuth, false)]
    [InlineData(Operation.PutRegistration, AclOptions.ClientType.OAuth, false)]
    [InlineData(Operation.DeleteRegistration, AclOptions.ClientType.OAuth, false)]
    public async Task WhenReadOnlyClient_ShouldAllowReadsAndForbidWrites(
        Operation operation,
        AclOptions.ClientType clientType,
        bool shouldSucceed
    )
    {
        var client = CreateClient(V1WebApplicationFactory.TestUser.ReadOnly, clientType);

        var response = await Execute(client, operation);

        AssertOutcome(response, shouldSucceed);
    }

    // AUTH-07: write-only client is forbidden on reads, and succeeds on every write.
    [Theory]
    [InlineData(Operation.Search, AclOptions.ClientType.ApiKey, false)]
    [InlineData(Operation.Get, AclOptions.ClientType.ApiKey, false)]
    [InlineData(Operation.PutOrganisation, AclOptions.ClientType.ApiKey, true)]
    [InlineData(Operation.PutRegistration, AclOptions.ClientType.ApiKey, true)]
    [InlineData(Operation.DeleteRegistration, AclOptions.ClientType.ApiKey, true)]
    [InlineData(Operation.Search, AclOptions.ClientType.OAuth, false)]
    [InlineData(Operation.Get, AclOptions.ClientType.OAuth, false)]
    [InlineData(Operation.PutOrganisation, AclOptions.ClientType.OAuth, true)]
    [InlineData(Operation.PutRegistration, AclOptions.ClientType.OAuth, true)]
    [InlineData(Operation.DeleteRegistration, AclOptions.ClientType.OAuth, true)]
    public async Task WhenWriteOnlyClient_ShouldForbidReadsAndAllowWrites(
        Operation operation,
        AclOptions.ClientType clientType,
        bool shouldSucceed
    )
    {
        var client = CreateClient(V1WebApplicationFactory.TestUser.WriteOnly, clientType);

        var response = await Execute(client, operation);

        AssertOutcome(response, shouldSucceed);
    }

    // AUTH-08: read-write client succeeds on every operation.
    [Theory]
    [InlineData(Operation.Search, AclOptions.ClientType.ApiKey)]
    [InlineData(Operation.Get, AclOptions.ClientType.ApiKey)]
    [InlineData(Operation.PutOrganisation, AclOptions.ClientType.ApiKey)]
    [InlineData(Operation.PutRegistration, AclOptions.ClientType.ApiKey)]
    [InlineData(Operation.DeleteRegistration, AclOptions.ClientType.ApiKey)]
    [InlineData(Operation.Search, AclOptions.ClientType.OAuth)]
    [InlineData(Operation.Get, AclOptions.ClientType.OAuth)]
    [InlineData(Operation.PutOrganisation, AclOptions.ClientType.OAuth)]
    [InlineData(Operation.PutRegistration, AclOptions.ClientType.OAuth)]
    [InlineData(Operation.DeleteRegistration, AclOptions.ClientType.OAuth)]
    public async Task WhenReadWriteClient_ShouldSucceedOnEveryOperation(
        Operation operation,
        AclOptions.ClientType clientType
    )
    {
        var client = CreateClient(V1WebApplicationFactory.TestUser.ReadWrite, clientType);

        var response = await Execute(client, operation);

        response.IsSuccessStatusCode.Should().BeTrue();
    }

    private static void AssertOutcome(HttpResponseMessage response, bool shouldSucceed)
    {
        if (shouldSucceed)
            response.IsSuccessStatusCode.Should().BeTrue();
        else
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private HttpClient CreateClient(V1WebApplicationFactory.TestUser testUser, AclOptions.ClientType clientType) =>
        _factory.CreateClient(testUser, clientType, ConfigureSuccessfulService);

    private static void ConfigureSuccessfulService(IServiceCollection services)
    {
        var organisation = OrganisationEntityFixtures.Default().With(x => x.Id, OrganisationId).Create();
        var organisationService = Substitute.For<IOrganisationService>();

        organisationService.Get(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(organisation);
        organisationService
            .Create(Arg.Any<Organisation>(), Arg.Any<CancellationToken>())
            .Returns(args => (Organisation)args[0]);
        organisationService
            .Update(Arg.Any<Organisation>(), Arg.Any<CancellationToken>())
            .Returns(args => (Organisation)args[0]);
        organisationService
            .Search(
                Arg.Any<List<RegistrationType>>(),
                Arg.Any<List<int>>(),
                Arg.Any<List<RegistrationStatus>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new List<Organisation> { organisation });

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

using System.Diagnostics.CodeAnalysis;
using Defra.WasteOrganisations.Api.Data.Entities;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using MongoDB.Driver.Authentication.AWS;

namespace Defra.WasteOrganisations.Api.Data;

[ExcludeFromCodeCoverage(Justification = "See integration tests")]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDbContext(
        this IServiceCollection services,
        IConfiguration configuration,
        bool integrationTest
    )
    {
        services
            .AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .ValidateDataAnnotations();

        if (integrationTest)
            return services;

        services.AddHostedService<MongoIndexService>();
        services.AddScoped<IDbContext, MongoDbContext>();
        services.AddSingleton(sp =>
        {
            MongoClientSettings.Extensions.AddAWSAuthentication();

            var options = sp.GetRequiredService<IOptions<MongoDbOptions>>();
            var settings = MongoClientSettings.FromConnectionString(options.Value.DatabaseUri);
            var client = new MongoClient(settings);

            RegisterConventions();

            return client.GetDatabase(options.Value.DatabaseName);
        });

        return services;
    }

    public static void RegisterConventions()
    {
        var conventionPack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String),
        };

        ConventionRegistry.Register(nameof(conventionPack), conventionPack, _ => true);

        BsonSerializer.RegisterSerializer(new RegistrationKeySerializer());
    }
}

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteOrganisations.Api.Data.Entities;

public record Organisation
{
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public required Guid Id { get; init; }
    public int Version { get; init; } = 1;
    public DateTime Created { get; init; }
    public DateTime Updated { get; init; }

    public required string Name { get; init; }
    public string? TradingName { get; init; }
    public string? BusinessCountry { get; init; }
    public string? CompaniesHouseNumber { get; init; }
    public required Address Address { get; init; }
    public Registration[] Registrations { get; init; } = [];

    public Dictionary<RegistrationKey, Registration> RegistrationsAsDictionary() =>
        Registrations.ToDictionary(x => new RegistrationKey(x.Type, x.RegistrationYear), x => x);
}

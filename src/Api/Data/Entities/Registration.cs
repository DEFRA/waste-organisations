using MongoDB.Bson.Serialization.Attributes;

namespace Defra.WasteOrganisations.Api.Data.Entities;

public record Registration
{
    public required string Type { get; init; }
    public required int RegistrationYear { get; init; }
    public required string Status { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? Created { get; init; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? Updated { get; init; }

    public RegistrationKey Key => new(Type, RegistrationYear);
}

using Defra.WasteOrganisations.Api.Dtos;
using Defra.WasteOrganisations.Api.Extensions;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Defra.WasteOrganisations.Api.Data.Entities;

public record RegistrationKey(string Type, int RegistrationYear)
{
    public RegistrationKey(RegistrationType type, int registrationYear)
        : this(type.ToJsonValue(), registrationYear) { }

    public override string ToString() => $"{Type}-{RegistrationYear}";

    public static RegistrationKey Parse(string value)
    {
        var parts = value.Split('-');

        return new RegistrationKey(Enum.Parse<RegistrationType>(parts[0]), int.Parse(parts[1]));
    }
}

public class RegistrationKeySerializer : SerializerBase<RegistrationKey>
{
    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, RegistrationKey value)
    {
        context.Writer.WriteString(value.ToString());
    }

    public override RegistrationKey Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var value = context.Reader.ReadString();

        return RegistrationKey.Parse(value);
    }
}

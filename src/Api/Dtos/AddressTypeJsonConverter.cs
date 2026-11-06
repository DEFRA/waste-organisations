using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos;

/// <summary>
/// Reads and writes <see cref="AddressType"/> by its wire names only. The plain
/// <see cref="JsonStringEnumConverter"/> also accepts integers and numeric strings, so
/// <c>["REGISTERED", 999]</c> bound to a kind no usage type has, passed validation and failed in
/// the mapping with a <c>500</c>; with integer values off it is an unreadable body, a <c>400</c>.
/// </summary>
public sealed class AddressTypeJsonConverter()
    : JsonStringEnumConverter<AddressType>(namingPolicy: null, allowIntegerValues: false);

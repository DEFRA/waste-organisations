using System.Text.Json;

namespace Defra.WasteOrganisations.Api.Extensions;

public static class EnumExtensions
{
    public static string ToJsonValue<TEnum>(this TEnum value)
        where TEnum : struct, Enum => JsonSerializer.Serialize(value).Trim('"');

    public static TEnum FromJsonValue<TEnum>(this string value)
        where TEnum : struct, Enum => JsonSerializer.Deserialize<TEnum>($"\"{value}\"");
}

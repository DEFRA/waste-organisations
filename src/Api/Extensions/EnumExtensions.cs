using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Api.Extensions;

[SuppressMessage(
    "Minor Code Smell",
    "S2325:Methods and properties that don't access instance data should be static - Sonar has not caught up with latest syntax"
)]
public static class EnumExtensions
{
    public static string ToJsonValue<TEnum>(this TEnum value)
        where TEnum : struct, Enum => JsonSerializer.Serialize(value).Trim('"');

    extension(string value)
    {
        public TEnum FromJsonValue<TEnum>()
            where TEnum : struct, Enum => JsonSerializer.Deserialize<TEnum>($"\"{value}\"");
    }
}

using Api.Extensions;

namespace Testing.Extensions;

public static class EnumExtensions
{
    private static readonly Random s_random = new();

    public static string RandomJsonValue<TEnum>()
        where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>();

        return values[s_random.Next(0, values.Length)].ToJsonValue();
    }
}

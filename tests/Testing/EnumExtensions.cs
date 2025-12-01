using System.Text.Json.Serialization;

namespace Testing;

public static class EnumExtensions
{
    public static string ToJsonValue(this Enum value)
    {
        var memberInfo = value.GetType().GetMember(value.ToString())[0];
        var attribute = memberInfo.CustomAttributes.FirstOrDefault(x =>
            x.AttributeType == typeof(JsonStringEnumMemberNameAttribute)
        );

        if (attribute != null)
        {
            // The constructor argument contains the Name value
            return attribute.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? value.ToString();
        }

        return value.ToString();
    }
}

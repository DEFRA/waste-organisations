using System.Text.Json;

namespace Api.Dtos;

public class RegistrationTypeFromRoute
{
    public RegistrationType RegistrationType { get; private init; }

    /// <summary>
    /// Special method used for model binding
    /// </summary>
    /// <param name="value"></param>
    /// <param name="provider"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    // ReSharper disable once UnusedMember.Global
    public static bool TryParse(string value, IFormatProvider provider, out RegistrationTypeFromRoute? result)
    {
        result = null;

        try
        {
            result = new RegistrationTypeFromRoute
            {
                RegistrationType = JsonSerializer.Deserialize<RegistrationType>($"\"{value}\""),
            };

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Api.Dtos.Attributes;

public abstract class BaseCommaSeparatedListAttribute : ValidationAttribute
{
    protected static List<string> ParseInvalidValues(object? value, Func<string, bool> isValidFunc)
    {
        var values = (value as string)?.Split(',');

        if (values is null || values.Length <= 0)
            return [];

        var invalidValues = new List<string>();

        foreach (var val in values)
        {
            try
            {
                if (!isValidFunc(val))
                    invalidValues.Add(val);
            }
            catch (Exception)
            {
                invalidValues.Add(val);
            }
        }

        return invalidValues;
    }

    protected ValidationResult? HandleInvalidValues(List<string> invalidValues) =>
        invalidValues.Count != 0 ? new ValidationResult(FormatError(invalidValues)) : ValidationResult.Success;

    private string FormatError(List<string> invalidValues) =>
        ErrorMessage is not null
            ? $"{ErrorMessage} - {string.Join(", ", invalidValues)}"
            : string.Join(", ", invalidValues);
}

using System.ComponentModel.DataAnnotations;
using Api.Extensions;

namespace Api.Dtos.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class EnumCommaSeparatedListAttribute(Type enumType) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var values = (value as string)?.Split(',');

        if (values is null || values.Length <= 0)
            return ValidationResult.Success;

        var invalidValues = new List<string>();

        foreach (var val in values)
        {
            try
            {
                val.FromJsonValue(enumType);
            }
            catch (Exception)
            {
                invalidValues.Add(val);
            }
        }

        return invalidValues.Count != 0 ? new ValidationResult(FormatError(invalidValues)) : ValidationResult.Success;
    }

    private string FormatError(List<string> invalidValues) =>
        ErrorMessage is not null
            ? $"{ErrorMessage} - {string.Join(", ", invalidValues)}"
            : string.Join(", ", invalidValues);
}

using System.ComponentModel.DataAnnotations;

namespace Api.Dtos.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class NumericCommaSeparatedListAttribute(int minimum, int maximum) : BaseCommaSeparatedListAttribute
{
    private readonly RangeAttribute _range = new(minimum, maximum);

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var invalidValues = ParseInvalidValues(
            value,
            val =>
            {
                var parsed = int.Parse(val);

                return _range.IsValid(parsed);
            }
        );

        return HandleInvalidValues(invalidValues);
    }
}

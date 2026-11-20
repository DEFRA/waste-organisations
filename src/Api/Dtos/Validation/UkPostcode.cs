using System.Text.RegularExpressions;

namespace Defra.WasteOrganisations.Api.Dtos.Validation;

/// <summary>
/// The UK postcode rule of spec 0007, Postcodes: every character that is not a letter or a digit is
/// removed, the rest is upper-cased and must be a full UK postcode, stored as the outward code, one
/// space and the inward code. The check is of the format only.
/// </summary>
public static partial class UkPostcode
{
    private const int InwardCodeLength = 3;

    public static bool TryNormalise(string? value, out string? postcode)
    {
        var compact = Compact(value);

        if (!FullPostcode().IsMatch(compact))
        {
            postcode = null;
            return false;
        }

        postcode = $"{compact[..^InwardCodeLength]} {compact[^InwardCodeLength..]}";
        return true;
    }

    public static bool IsBlank(string? value) => Compact(value).Length == 0;

    private static string Compact(string? value) =>
        string.Concat((value ?? "").Where(char.IsLetterOrDigit)).ToUpperInvariant();

    [GeneratedRegex("^(GIR0AA|[A-Z]{1,2}[0-9][A-Z0-9]?[0-9][A-Z]{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex FullPostcode();
}

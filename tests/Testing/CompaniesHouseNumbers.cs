using System.Globalization;
using System.Text.RegularExpressions;

namespace Defra.WasteOrganisations.Testing;

// ux_organisation_companies_house_number allows one organisation per number, so every organisation a test
// stores with a number takes one that no other test in the run has used (spec 0008, Decisions)
public static partial class CompaniesHouseNumbers
{
    // Fixed because the FullPayload_ShouldCreateOrganisationAndRows snapshot records it
    public const string FullPayload = "12345678";

    private static readonly Lock IssuedLock = new();
    private static readonly HashSet<string> Issued = [FullPayload];

    // Eight digits, the form of a company registered in England and Wales
    public static string Generate() =>
        Issue(() => Random.Shared.Next(100_000_000).ToString("D8", CultureInfo.InvariantCulture));

    // Two letters and six digits, the form of a company registered elsewhere (SC for Scotland)
    public static string Generate(string prefix)
    {
        if (!PrefixPattern().IsMatch(prefix))
            throw new ArgumentException("A prefix is two upper-case letters", nameof(prefix));

        return Issue(() => prefix + Random.Shared.Next(1_000_000).ToString("D6", CultureInfo.InvariantCulture));
    }

    // Draws again on a repeat, so a number is issued once per run
    private static string Issue(Func<string> draw)
    {
        lock (IssuedLock)
        {
            string number;
            do
            {
                number = draw();
            } while (!Issued.Add(number));

            return number;
        }
    }

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex PrefixPattern();
}

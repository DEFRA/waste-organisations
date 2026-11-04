using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Defra.WasteOrganisations.Api.Dtos.Validation;

/// <summary>
/// Validates a <see cref="CreateOrganisationRequest"/> and returns every error keyed by the JSON
/// path of the failing member, such as <c>organisation.addresses[1].postalCode</c>, ready for
/// <c>TypedResults.ValidationProblem</c>.
/// </summary>
/// <remarks>
/// The framework's minimal API validation cannot produce the v2 <c>400</c>: it keys errors by C#
/// member names, does not validate the items of an array, and skips <see cref="IValidatableObject"/>
/// on a node when a member below it has an attribute error, so a missing <c>street</c> would hide a
/// missing <c>tradingName</c>. This validator walks the whole request, runs the data annotations of
/// every member and the <see cref="IValidatableObject"/> rules of every node regardless of other
/// errors, and names members by their <see cref="JsonPropertyNameAttribute"/>.
/// </remarks>
public static class CreateOrganisationRequestValidator
{
    public static Dictionary<string, string[]> Validate(CreateOrganisationRequest request)
    {
        var errors = new Dictionary<string, List<string>>();

        ValidateNode(request, "", errors);

        if (request.Organisation is { } organisation)
        {
            ValidateNode(organisation, "organisation", errors);

            var addresses = organisation.Addresses ?? [];

            for (var index = 0; index < addresses.Count; index++)
            {
                var path = $"organisation.addresses[{index}]";

                if (addresses[index] is { } address)
                    ValidateNode(address, path, errors);
                else
                    Add(errors, path, "The address must not be null.");
            }
        }

        return errors.ToDictionary(e => e.Key, e => e.Value.ToArray());
    }

    private static void ValidateNode(object node, string path, Dictionary<string, List<string>> errors)
    {
        foreach (var property in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var jsonName = JsonName(property);
            // The display name puts the JSON name into the attributes' default messages
            var context = new ValidationContext(node) { MemberName = property.Name, DisplayName = jsonName };
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateProperty(property.GetValue(node), context, results))
            {
                foreach (var result in results)
                    Add(errors, Join(path, jsonName), result.ErrorMessage);
            }
        }

        if (node is not IValidatableObject validatable)
            return;

        foreach (var result in validatable.Validate(new ValidationContext(node)))
        {
            // A rule that names no member is about the node itself, such as the premises rule
            var members = result.MemberNames.Any() ? result.MemberNames : [""];

            foreach (var member in members)
                Add(errors, Join(path, member), result.ErrorMessage);
        }
    }

    private static string JsonName(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);

    private static string Join(string path, string member) =>
        (path, member) switch
        {
            ("", _) => member,
            (_, "") => path,
            _ => $"{path}.{member}",
        };

    private static void Add(Dictionary<string, List<string>> errors, string key, string? message)
    {
        if (!errors.TryGetValue(key, out var messages))
            errors[key] = messages = [];

        messages.Add(message ?? "The value is invalid.");
    }
}

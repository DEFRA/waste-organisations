using System.ComponentModel.DataAnnotations;

namespace Defra.WasteOrganisations.Api.Utils.Logging;

public class TraceHeader
{
    [ConfigurationKeyName("TraceHeader")]
    [Required]
    public required string Name { get; set; }
}

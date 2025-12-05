using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace Api.Data;

[ExcludeFromCodeCoverage]
public class MongoDbOptions
{
    public const string SectionName = "Mongo";

    [Required]
    public string? DatabaseUri { get; set; }

    [Required]
    public string? DatabaseName { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace Defra.WasteOrganisations.Api.Authentication;

public class AclOptions
{
    public Dictionary<string, Client> Clients { get; init; } = new();

    public class Client
    {
        [Required]
        public required ClientType Type { get; init; }

        public string? Secret { get; init; }

        [Required]
        public required string[] Scopes { get; init; } = [];
    }

    public enum ClientType
    {
        OAuth,
        ApiKey,
    }
}

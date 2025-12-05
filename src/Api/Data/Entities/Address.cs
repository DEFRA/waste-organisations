namespace Api.Data.Entities;

public record Address
{
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? Town { get; init; }
    public string? County { get; init; }
    public string? Postcode { get; init; }
    public string? Country { get; init; }
}

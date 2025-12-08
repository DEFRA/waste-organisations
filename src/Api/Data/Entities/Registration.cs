namespace Api.Data.Entities;

public record Registration
{
    public required string Type { get; init; }
    public required int RegistrationYear { get; init; }
    public required string Status { get; init; }
}

namespace ITCafe.Api.Dtos.Auth;

public record LoginRequest
{
    public string? Email { get; init; }
    public string? Username { get; init; }
    public required string Password { get; init; }
}

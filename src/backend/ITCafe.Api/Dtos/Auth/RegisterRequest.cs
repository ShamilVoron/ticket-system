namespace ITCafe.Api.Dtos.Auth;

public record RegisterRequest(string FullName, string Email, string Password, string Role);

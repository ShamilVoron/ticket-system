namespace ITCafe.Api.Dtos.Auth;

public record AuthResponse(string Token, string UserId, string FullName, string Email, string Role, string AvatarUrl);

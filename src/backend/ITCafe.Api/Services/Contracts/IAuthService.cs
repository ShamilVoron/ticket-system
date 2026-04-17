using ITCafe.Api.Dtos.Auth;
using ITCafe.Api.Models;

namespace ITCafe.Api.Services.Contracts;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    string GenerateJwtToken(UserAccount account, string? normalizedRole = null);
}

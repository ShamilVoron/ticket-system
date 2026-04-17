using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITCafe.Api.Data;
using ITCafe.Api.Dtos.Auth;
using ITCafe.Api.Services.Contracts;

namespace ITCafe.Api.Controllers;

/// <summary>
/// Аутентификация и регистрация пользователей.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly AppDbContext _context;

    public AuthController(IAuthService authService, AppDbContext context)
    {
        _authService = authService;
        _context = context;
    }

    /// <summary>Регистрация нового пользователя.</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>Вход по email или username. Возвращает JWT-токен.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    /// <summary>Сгенерировать тестовый Bearer-токен (только super_admin).</summary>
    [Authorize(Roles = "super_admin")]
    [HttpPost("generate-test-token")]
    public async Task<ActionResult> GenerateTestToken(GenerateTestTokenRequest request)
    {
        var targetUserId = string.IsNullOrWhiteSpace(request.UserId)
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : request.UserId;

        if (string.IsNullOrWhiteSpace(targetUserId))
            return BadRequest("UserId is required");

        var account = await _context.UserAccounts.FirstOrDefaultAsync(u => u.UserId == targetUserId);
        if (account == null)
            return NotFound(new { error = "User not found" });

        var token = _authService.GenerateJwtToken(account);
        return Ok(new
        {
            token,
            account.UserId,
            account.Email,
            account.Role,
            account.FullName,
            expiresInHours = 24
        });
    }

    /// <summary>Для клиентского аккаунта: CompanyId и название для поля clientId в CreateTicket.</summary>
    [Authorize(Roles = "client")]
    [HttpGet("ticket-context")]
    public async Task<ActionResult<TicketContextResponse>> GetTicketContext()
    {
        var email = User.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
            return Ok(new TicketContextResponse(null, null));

        var client = await _context.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email.ToLower() == email);
        if (client == null)
            return Ok(new TicketContextResponse(null, null));

        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == client.CompanyId);

        var name = company?.Name ?? client.FullName;
        return Ok(new TicketContextResponse(client.CompanyId, name));
    }

    public record TicketContextResponse(int? CompanyId, string? CompanyName);
}

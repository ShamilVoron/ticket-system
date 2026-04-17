using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ITCafe.Api.Dtos.Employees;
using ITCafe.Api.Services.Contracts;

namespace ITCafe.Api.Controllers;

/// <summary>Управление сотрудниками.</summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeesController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new InvalidOperationException("User identifier not found");

    /// <summary>Возвращает список всех сотрудников.</summary>
    [HttpGet]
    [Authorize(Roles = "support_l1,support_l2,developer,field_engineer,accountant,super_admin,coordinator,sysadmin,head_support,head_dev,head_engineers,head_repair,director,procurement,agent")]
    public async Task<ActionResult<IEnumerable<StaffDto>>> GetAllStaff()
    {
        var staff = await _employeeService.GetAllStaffAsync();
        return Ok(staff);
    }

    /// <summary>Текущий пользователь: профиль сотрудника и <c>PermissionsJson</c> (для UI).</summary>
    [HttpGet("me")]
    [Authorize(Roles = "support_l1,support_l2,developer,field_engineer,accountant,super_admin,coordinator,sysadmin,head_support,head_dev,head_engineers,head_repair,director,procurement,agent")]
    public async Task<ActionResult<EmployeeDto>> GetMe()
    {
        var employee = await _employeeService.GetEmployeeAsync(CurrentUserId());
        if (employee == null) return NotFound();
        return Ok(employee);
    }

    /// <summary>Возвращает данные сотрудника.</summary>
    [HttpGet("{userId}")]
    public async Task<ActionResult<EmployeeDto>> GetEmployee(string userId)
    {
        if (User.IsInRole("field_engineer")
            && !string.Equals(CurrentUserId(), userId, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var employee = await _employeeService.GetEmployeeAsync(userId);
        if (employee == null) return NotFound();
        return Ok(employee);
    }

    /// <summary>Изменяет логин сотрудника.</summary>
    [HttpPost("{userId}/change-login")]
    public async Task<IActionResult> ChangeLogin(string userId, ChangeLoginDto dto)
    {
        if (CurrentUserId() != userId && !User.IsInRole("super_admin"))
            return Forbid();
        var success = await _employeeService.ChangeLoginAsync(userId, dto.NewLogin);
        if (!success) return NotFound();
        return Ok();
    }

    /// <summary>Изменяет пароль сотрудника.</summary>
    [HttpPost("{userId}/change-password")]
    public async Task<IActionResult> ChangePassword(string userId, ChangePasswordDto dto)
    {
        if (CurrentUserId() != userId && !User.IsInRole("super_admin"))
            return Forbid();
        var success = await _employeeService.ChangePasswordAsync(userId, dto.OldPassword, dto.NewPassword);
        if (!success) return NotFound();
        return Ok();
    }

    /// <summary>Изменяет расписание сотрудника.</summary>
    [HttpPost("{userId}/change-schedule")]
    public async Task<IActionResult> ChangeSchedule(string userId, ChangeScheduleDto dto)
    {
        if (CurrentUserId() != userId && !User.IsInRole("super_admin"))
            return Forbid();
        var success = await _employeeService.ChangeScheduleAsync(userId, dto.NewSchedule, dto.NewScheduleGridJson);
        if (!success) return NotFound();
        return Ok();
    }

    /// <summary>Обновляет аватар сотрудника.</summary>
    [HttpPost("{userId}/change-avatar")]
    public async Task<IActionResult> ChangeAvatar(string userId, IFormFile file, [FromServices] IWebHostEnvironment env)
    {
        if (CurrentUserId() != userId && !User.IsInRole("super_admin"))
            return Forbid();
        try
        {
            var avatarUrl = await _employeeService.ChangeAvatarAsync(userId, file, env.WebRootPath);
            return Ok(new { avatarUrl });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Обновляет профиль сотрудника.</summary>
    [HttpPost("{userId}/update-profile")]
    public async Task<IActionResult> UpdateEmployeeProfile(string userId, UpdateEmployeeProfileDto dto)
    {
        if (CurrentUserId() != userId && !User.IsInRole("super_admin"))
            return Forbid();

        if (!string.IsNullOrWhiteSpace(dto.Role) && !User.IsInRole("super_admin"))
            return Forbid();

        return await _employeeService.UpdateProfileAsync(userId, dto);
    }

    /// <summary>Удаляет сотрудника.</summary>
    [HttpDelete("{userId}")]
    public async Task<IActionResult> DeleteEmployee(string userId)
    {
        if (!User.IsInRole("super_admin"))
            return Forbid();
        var success = await _employeeService.DeleteEmployeeAsync(userId);
        if (!success) return NotFound("Employee not found.");
        return Ok(new { message = "Employee deleted." });
    }

    /// <summary>Создаёт учётную запись сотрудника.</summary>
    [HttpPost("create-account")]
    [Authorize(Roles = "super_admin")]
    public async Task<ActionResult<object>> CreateEmployeeAccount(CreateEmployeeAccountDto dto)
    {
        try
        {
            var result = await _employeeService.CreateAccountAsync(dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }
}

using ITCafe.Api.Dtos.Employees;
using Microsoft.AspNetCore.Mvc;

namespace ITCafe.Api.Services.Contracts;

public interface IEmployeeService
{
    Task<IEnumerable<StaffDto>> GetAllStaffAsync();
    Task<EmployeeDto?> GetEmployeeAsync(string userId);
    Task<bool> ChangeLoginAsync(string userId, string newLogin);
    Task<bool> ChangePasswordAsync(string userId, string oldPassword, string newPassword);
    Task<bool> ChangeScheduleAsync(string userId, string? newSchedule, string? newScheduleGridJson);
    Task<string> ChangeAvatarAsync(string userId, IFormFile file, string webRootPath);
    Task<IActionResult> UpdateProfileAsync(string userId, UpdateEmployeeProfileDto dto);
    Task<bool> DeleteEmployeeAsync(string userId);
    Task<object> CreateAccountAsync(CreateEmployeeAccountDto dto);
}

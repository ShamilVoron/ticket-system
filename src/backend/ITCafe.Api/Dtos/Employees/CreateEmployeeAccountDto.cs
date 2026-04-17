namespace ITCafe.Api.Dtos.Employees;

public record CreateEmployeeAccountDto(
    string FullName,
    string Password,
    string Role,
    string? Login = null,
    string? Email = null,
    string? Department = null
);

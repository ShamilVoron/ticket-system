using System.Net;
using System.Net.Http.Json;
using ITCafe.Api.Data;
using ITCafe.Api.Dtos.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITCafe.Tests.Integration;

[Collection("Integration")]
public class EmployeesIntegrationTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public EmployeesIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        await SeedAuthenticatedUser();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task SeedAuthenticatedUser()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserAccounts.Add(new Api.Models.UserAccount
        {
            UserId = "super-admin-test",
            FullName = "Admin User",
            Email = "admin@example.com",
            Password = BCrypt.Net.BCrypt.HashPassword("Password123"),
            Role = "super_admin"
        });
        db.Employees.Add(new Api.Models.Employee
        {
            UserId = "super-admin-test",
            FullName = "Admin User",
            Role = "Супер-админ",
            Login = "admin",
            Email = "admin@example.com"
        });
        await db.SaveChangesAsync();

        var loginRequest = new { email = "admin@example.com", password = "Password123" };
        var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
        var result = await response.Content.ReadFromJsonAsync<Api.Dtos.Auth.AuthResponse>();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result!.Token);
    }

    [Fact]
    public async Task GetAllStaff_ShouldReturnAtLeastOneUser()
    {
        var response = await _client.GetAsync("/api/Employees");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<StaffDto>>();
        Assert.NotNull(result);
        Assert.NotEmpty(result!);
    }

    [Fact]
    public async Task CreateAccount_ShouldCreateEmployee_WhenValidRequest()
    {
        var request = new CreateEmployeeAccountDto("New Employee", "Password123", "support_l1", "newemp", "newemp@example.com", "IT");
        var response = await _client.PostAsJsonAsync("/api/Employees/create-account", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(result);
        Assert.Equal("newemp@example.com", result!["email"].ToString());
    }

    [Fact]
    public async Task CreateAccount_ShouldReturnBadRequest_WhenInvalidRole()
    {
        var request = new CreateEmployeeAccountDto("New Employee", "Password123", "invalid_role", "bademp", "bademp@example.com");
        var response = await _client.PostAsJsonAsync("/api/Employees/create-account", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

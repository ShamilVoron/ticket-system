using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ITCafe.Api.Data;
using ITCafe.Api.Dtos.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITCafe.Tests.Integration;

[Collection("Integration")]
public class AuthIntegrationTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
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
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_ShouldReturnToken_WhenValidRequest()
    {
        var request = new RegisterRequest("Test User", "test@example.com", "Password123", "support_l1");

        var response = await _client.PostAsJsonAsync("/api/Auth/register", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result!.Token));
    }

    [Fact]
    public async Task Register_ShouldReturnConflict_WhenDuplicateEmail()
    {
        var request = new RegisterRequest("Test User", "dup@example.com", "Password123", "support_l1");
        await _client.PostAsJsonAsync("/api/Auth/register", request);

        var response = await _client.PostAsJsonAsync("/api/Auth/register", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_ShouldReturnToken_WhenValidCredentials()
    {
        var registerRequest = new RegisterRequest("Login User", "login@example.com", "Password123", "support_l1");
        await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);

        var loginRequest = new LoginRequest { Email = "login@example.com", Password = "Password123" };
        var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result!.Token));
    }

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenWrongPassword()
    {
        var registerRequest = new RegisterRequest("Login User2", "login2@example.com", "Password123", "support_l1");
        await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);

        var loginRequest = new LoginRequest { Email = "login2@example.com", Password = "wrongpassword" };
        var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_ShouldMigratePlainTextPassword_ToBcrypt()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserAccounts.Add(new Api.Models.UserAccount
        {
            UserId = "legacy-user",
            FullName = "Legacy",
            Email = "legacy@example.com",
            Password = "plaintext123",
            Role = "client"
        });
        db.SaveChanges();

        var loginRequest = new LoginRequest { Email = "legacy@example.com", Password = "plaintext123" };
        var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify password was re-hashed
        db.ChangeTracker.Clear();
        var account = db.UserAccounts.First(u => u.Email == "legacy@example.com");
        Assert.StartsWith("$2", account.Password);
    }
}

using System.Net;
using System.Net.Http.Json;
using ITCafe.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITCafe.Tests.Integration;

[Collection("Integration")]
public class Phase1ControllersIntegrationTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private string _staffUserId = "staff-prefs-test";

    public Phase1ControllersIntegrationTests(CustomWebApplicationFactory factory)
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
        await SeedStaffUser();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task SeedStaffUser()
    {
        // Unique per class instance to avoid Conflict if DB is reused across runs.
        var email = $"staff-{Guid.NewGuid():N}@example.com";
        var userId = $"staff-prefs-{Guid.NewGuid():N}";
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserAccounts.Add(new Api.Models.UserAccount
        {
            UserId = userId,
            FullName = "Staff User",
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword("Password123"),
            Role = "coordinator"
        });
        db.Employees.Add(new Api.Models.Employee
        {
            UserId = userId,
            FullName = "Staff User",
            Role = "Координатор",
            Login = $"staff-{Guid.NewGuid():N}"[..16],
            Email = email
        });
        await db.SaveChangesAsync();

        var loginRequest = new { email, password = "Password123" };
        var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
        var result = await response.Content.ReadFromJsonAsync<Api.Dtos.Auth.AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result!.Token);

        // AgentPreferences round-trip needs a stable userId after seed.
        _staffUserId = userId;
    }

    [Fact]
    public async Task Departments_GetAll_ShouldReturnList()
    {
        var response = await _client.GetAsync("/api/Departments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
        Assert.NotNull(list);
        Assert.NotEmpty(list!);
    }

    [Fact]
    public async Task Reports_Repairs_ShouldReturnEmptySummary()
    {
        var response = await _client.GetAsync("/api/Reports/repairs?month=2026-04");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(json);
        Assert.True(json!.ContainsKey("items") || json.ContainsKey("summary"));
    }

    [Fact]
    public async Task AgentPreferences_RoundTrip()
    {
        var body = new
        {
            userId = _staffUserId,
            theme = "dark",
            backgroundUrl = "",
            dashboardBlocks = new[] { "tickets" },
            accentColor = "#112233",
            windowColor = "#ffffff",
            textColor = "#111827",
        };

        var save = await _client.PostAsJsonAsync("/api/AgentPreferences", body);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var get = await _client.GetAsync($"/api/AgentPreferences/{_staffUserId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var prefs = await get.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(prefs);
        Assert.Equal("dark", prefs!["theme"]?.ToString());
    }

    [Fact]
    public async Task Spreadsheets_CreateAndGet()
    {
        var create = await _client.PostAsJsonAsync("/api/Spreadsheets", new { name = "Test Sheet", rows = 5, cols = 3 });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(created);
        Assert.True(created!.ContainsKey("id"));

        var id = Convert.ToInt32(created["id"].ToString());
        var get = await _client.GetAsync($"/api/Spreadsheets/{id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task GoogleSync_ShouldRejectWithoutKey()
    {
        var response = await _client.PostAsJsonAsync("/api/sync/google/companies-objects", new
        {
            dryRun = true,
            rows = new[] { new { companyName = "Acme", companyCode = "ACM", objectName = "HQ", objectCode = "HQ1" } }
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

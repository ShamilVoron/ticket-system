using System.Net;
using System.Net.Http.Json;
using ITCafe.Api.Data;
using ITCafe.Api.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ITCafe.Tests.Integration;

/// <summary>Phase 8 hardening: companies CRUD, messenger, departments, KB published list.</summary>
[Collection("Integration")]
public class Phase8HardeningIntegrationTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private readonly string _email = $"phase8-{Guid.NewGuid():N}@example.com";
    private readonly string _userId = $"phase8-{Guid.NewGuid():N}";

    public Phase8HardeningIntegrationTests(CustomWebApplicationFactory factory)
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
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserAccounts.Add(new UserAccount
        {
            UserId = _userId,
            FullName = "Phase8 Staff",
            Email = _email,
            Password = BCrypt.Net.BCrypt.HashPassword("Password123"),
            Role = "coordinator"
        });
        db.Employees.Add(new Employee
        {
            UserId = _userId,
            FullName = "Phase8 Staff",
            Role = "Координатор",
            Login = $"p8-{Guid.NewGuid():N}"[..20],
            Email = _email
        });
        await db.SaveChangesAsync();

        var loginRequest = new { email = _email, password = "Password123" };
        var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Api.Dtos.Auth.AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result!.Token);
    }

    [Fact]
    public async Task Companies_Crud_HappyPath()
    {
        var createBody = new
        {
            name = $"Acme {Guid.NewGuid():N}",
            email = $"acme-{Guid.NewGuid():N}@example.com",
            phone = "+10000000000",
            hqAddress = "1 Main St",
            isActive = true,
            syncSource = "test",
        };

        var create = await _client.PostAsJsonAsync("/api/Companies", createBody);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<Company>();
        Assert.NotNull(created);
        Assert.True(created!.Id > 0);
        Assert.Equal(createBody.name, created.Name);

        var list = await _client.GetAsync("/api/Companies");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var companies = await list.Content.ReadFromJsonAsync<List<Company>>();
        Assert.NotNull(companies);
        Assert.Contains(companies!, c => c.Id == created.Id);

        var updateBody = new
        {
            name = $"{createBody.name} Updated",
            email = createBody.email,
            phone = "+19999999999",
            hqAddress = "2 Main St",
            isActive = true,
            syncSource = "test",
        };
        var update = await _client.PutAsJsonAsync($"/api/Companies/{created.Id}", updateBody);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<Company>();
        Assert.NotNull(updated);
        Assert.Equal(updateBody.name, updated!.Name);
        Assert.Equal("+19999999999", updated.Phone);

        var delete = await _client.DeleteAsync($"/api/Companies/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        var activeList = await _client.GetAsync("/api/Companies");
        var active = await activeList.Content.ReadFromJsonAsync<List<Company>>();
        Assert.DoesNotContain(active!, c => c.Id == created.Id);

        var withInactive = await _client.GetAsync("/api/Companies?includeInactive=true");
        var all = await withInactive.Content.ReadFromJsonAsync<List<Company>>();
        var softDeleted = Assert.Single(all!, c => c.Id == created.Id);
        Assert.False(softDeleted.IsActive);
    }

    [Fact]
    public async Task Messenger_ListConversations_EmptyOk()
    {
        var response = await _client.GetAsync("/api/Messenger/conversations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<object>>();
        Assert.NotNull(list);
        Assert.Empty(list!);
    }

    [Fact]
    public async Task Departments_Get_ReturnsList()
    {
        var response = await _client.GetAsync("/api/Departments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
        Assert.NotNull(list);
        Assert.NotEmpty(list!);
        Assert.Contains(list!, d => d.ContainsKey("value") && d.ContainsKey("label"));
    }

    [Fact]
    public async Task KnowledgeBase_ListPublished()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.KbArticles.Add(new KbArticle
            {
                Title = "Published Guide",
                Body = "How to reset a printer.",
                Tags = "printer,reset",
                IsPublished = true,
                UpdatedAtUtc = DateTime.UtcNow,
            });
            db.KbArticles.Add(new KbArticle
            {
                Title = "Draft Only",
                Body = "Should not appear.",
                Tags = "draft",
                IsPublished = false,
                UpdatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // Published list is AllowAnonymous — still works with auth header.
        var response = await _client.GetAsync("/api/KnowledgeBase/articles/published");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
        Assert.NotNull(list);
        Assert.Contains(list!, a => a["title"]?.ToString() == "Published Guide");
        Assert.DoesNotContain(list!, a => a["title"]?.ToString() == "Draft Only");
    }

    [Fact]
    public async Task Health_ShouldReturnHealthy()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

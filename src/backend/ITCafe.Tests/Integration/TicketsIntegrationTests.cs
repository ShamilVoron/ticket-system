using System.Net;
using System.Net.Http.Json;
using ITCafe.Api.Data;
using ITCafe.Api.Dtos.Tickets;
using ITCafe.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ITCafe.Tests.Integration;

[Collection("Integration")]
public class TicketsIntegrationTests : IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public TicketsIntegrationTests(CustomWebApplicationFactory factory)
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
        // Register всегда создаёт client; для проверок от имени сотрудника кладём coordinator в БД явно.
        const string email = "tickets@example.com";
        const string userId = "tickets-test-staff";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!await db.UserAccounts.AnyAsync(u => u.Email == email))
            {
                db.UserAccounts.Add(new UserAccount
                {
                    UserId = userId,
                    Email = email,
                    FullName = "Test User",
                    Password = BCrypt.Net.BCrypt.HashPassword("Password123"),
                    Role = "coordinator",
                });
                db.Employees.Add(new Employee
                {
                    UserId = userId,
                    FullName = "Test User",
                    Role = "Координатор",
                    Login = "tickets",
                    Email = email,
                    AvatarUrl = string.Empty,
                    WorkSchedule = string.Empty,
                    WorkScheduleGridJson = string.Empty,
                });
                await db.SaveChangesAsync();
            }
        }

        var loginRequest = new { email, password = "Password123" };
        var response = await _client.PostAsJsonAsync("/api/Auth/login", loginRequest);
        var result = await response.Content.ReadFromJsonAsync<Api.Dtos.Auth.AuthResponse>();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result!.Token);
    }

    [Fact]
    public async Task GetTickets_ShouldReturnEmptyList_Initially()
    {
        var response = await _client.GetAsync("/api/Tickets");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<TicketDto>>();
        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public async Task CreateTicket_ShouldReturnCreatedTicket()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Companies.Add(new Company { Name = "Test Company" });
        db.SaveChanges();

        var request = new CreateTicketRequest(
            "Test Ticket",
            "Другое",
            null,
            "Средний",
            "Координатор",
            null,
            null,
            db.Companies.First().Id,
            null,
            null,
            null
        );

        var response = await _client.PostAsJsonAsync("/api/Tickets", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Ticket>();
        Assert.NotNull(result);
        Assert.Equal("Test Ticket", result!.Title);
    }

    [Fact]
    public async Task UpdateStatus_ShouldUpdateTicketStatus()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Companies.Add(new Company { Name = "Test Company 2" });
        db.SaveChanges();

        var createRequest = new CreateTicketRequest(
            "Status Test",
            "Другое",
            null,
            "Средний",
            "Координатор",
            null,
            null,
            db.Companies.First().Id,
            null,
            null,
            null
        );
        var createResponse = await _client.PostAsJsonAsync("/api/Tickets", createRequest);
        var ticket = await createResponse.Content.ReadFromJsonAsync<Ticket>();

        var patchResponse = await _client.PatchAsJsonAsync($"/api/Tickets/{ticket!.Id}/status", new { status = "В работе" });
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/Tickets/{ticket.Id}");
        var updated = await getResponse.Content.ReadFromJsonAsync<TicketDto>();
        Assert.Equal("В работе", updated!.Status);
    }

    [Fact]
    public async Task AddComment_ShouldAddCommentToTicket()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Companies.Add(new Company { Name = "Test Company 3" });
        db.SaveChanges();

        var createRequest = new CreateTicketRequest(
            "Comment Test",
            "Другое",
            null,
            "Средний",
            "Координатор",
            null,
            null,
            db.Companies.First().Id,
            null,
            null,
            null
        );
        var createResponse = await _client.PostAsJsonAsync("/api/Tickets", createRequest);
        var ticket = await createResponse.Content.ReadFromJsonAsync<Ticket>();

        var commentRequest = new CreateCommentRequest("Author", "support", "Test comment", false);
        var commentResponse = await _client.PostAsJsonAsync($"/api/Tickets/{ticket!.Id}/comments", commentRequest);
        Assert.Equal(HttpStatusCode.Created, commentResponse.StatusCode);

        var getCommentsResponse = await _client.GetAsync($"/api/Tickets/{ticket.Id}/comments");
        var comments = await getCommentsResponse.Content.ReadFromJsonAsync<List<CommentDto>>();
        Assert.Single(comments!);
        Assert.Equal("Test comment", comments![0].Text);

        var getTicketResponse = await _client.GetAsync($"/api/Tickets/{ticket.Id}");
        var ticketAfter = await getTicketResponse.Content.ReadFromJsonAsync<TicketDto>();
        Assert.Equal("Открыт", ticket!.Status);
        Assert.Equal("В работе", ticketAfter!.Status);
    }

    /// <summary>
    /// Сценарий как у пользователя: super_admin создаёт заявку (или она уже «Открыт»), комментарий → статус «В работе».
    /// </summary>
    [Fact]
    public async Task AddComment_AsSuperAdmin_ShouldBumpOpenToInProgress()
    {
        const string saEmail = "superadmin-status-test@example.com";
        const string saPassword = "Password123!";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Companies.Add(new Company { Name = "Company SuperAdmin Flow" });
            db.UserAccounts.Add(new UserAccount
            {
                UserId = "superadmin-flow-uid",
                Email = saEmail,
                FullName = "Андрей",
                Password = BCrypt.Net.BCrypt.HashPassword(saPassword),
                Role = "super_admin",
            });
            await db.SaveChangesAsync();
        }

        var loginResp = await _client.PostAsJsonAsync("/api/Auth/login", new { email = saEmail, password = saPassword });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var loginBody = await loginResp.Content.ReadFromJsonAsync<Api.Dtos.Auth.AuthResponse>();
        Assert.NotNull(loginBody?.Token);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginBody!.Token);

        int clientId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            clientId = db.Companies.First(c => c.Name == "Company SuperAdmin Flow").Id;
        }

        var createRequest = new CreateTicketRequest(
            "API test bump status",
            "Другое",
            null,
            "Средний",
            "Координатор",
            null,
            null,
            clientId,
            null,
            null,
            null
        );
        var createResponse = await _client.PostAsJsonAsync("/api/Tickets", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var ticket = await createResponse.Content.ReadFromJsonAsync<Ticket>();
        Assert.NotNull(ticket);
        Assert.Equal("Открыт", ticket!.Status);

        var commentResponse = await _client.PostAsJsonAsync(
            $"/api/Tickets/{ticket.Id}/comments",
            new CreateCommentRequest("Андрей", "super_admin", "комментарий из теста API", false));
        Assert.Equal(HttpStatusCode.Created, commentResponse.StatusCode);

        var getTicketResponse = await _client.GetAsync($"/api/Tickets/{ticket.Id}");
        Assert.Equal(HttpStatusCode.OK, getTicketResponse.StatusCode);
        var ticketAfter = await getTicketResponse.Content.ReadFromJsonAsync<TicketDto>();
        Assert.Equal("В работе", ticketAfter!.Status);
    }
}

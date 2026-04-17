using Microsoft.EntityFrameworkCore;
using ITCafe.Api.Models;

namespace ITCafe.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<ServiceObject> ServiceObjects => Set<ServiceObject>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<FieldReport> FieldReports => Set<FieldReport>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<TicketSubtask> TicketSubtasks => Set<TicketSubtask>();
    public DbSet<Spreadsheet> Spreadsheets => Set<Spreadsheet>();
    public DbSet<SystemStatus> SystemStatuses => Set<SystemStatus>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<TelegramBotSetting> TelegramBotSettings => Set<TelegramBotSetting>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMember> ChatMembers => Set<ChatMember>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<UserTicketReadState> UserTicketReadStates => Set<UserTicketReadState>();
    public DbSet<UserChatReadState> UserChatReadStates => Set<UserChatReadState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Assignee);
            entity.HasIndex(e => e.OkdeskId);
            entity.HasIndex(e => new { e.IsRepair, e.CreatedAt });
            entity.HasIndex(e => e.ClientId);
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.ExternalCode);
            entity.HasIndex(e => e.OkdeskId);
        });

        modelBuilder.Entity<ServiceObject>(entity =>
        {
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.ExternalCode);
            entity.HasIndex(e => e.OkdeskId);
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.Login);
            entity.HasIndex(e => e.OkdeskId);
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.UserId).IsUnique();
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasIndex(e => e.CompanyId);
            entity.HasIndex(e => e.OkdeskId);
        });

        modelBuilder.Entity<TicketComment>(entity =>
        {
            entity.HasIndex(e => e.TicketId);
            entity.HasIndex(e => e.OkdeskId);
        });

        modelBuilder.Entity<TicketAttachment>(entity =>
        {
            entity.HasIndex(e => e.TicketId);
            entity.HasIndex(e => e.CommentId);
        });

        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.HasIndex(e => e.Tab);
            entity.HasIndex(e => e.EquipmentType);
            entity.HasIndex(e => new { e.Tab, e.EquipmentType });
        });

        modelBuilder.Entity<FieldReport>(entity =>
        {
            entity.HasIndex(e => e.TicketId);
        });

        modelBuilder.Entity<TicketSubtask>(entity =>
        {
            entity.HasIndex(e => e.TicketId);
        });

        modelBuilder.Entity<SystemStatus>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.SortOrder);
            entity.HasIndex(e => e.IsActive);
        });

        modelBuilder.Entity<SlaPolicy>(entity =>
        {
            entity.HasIndex(e => e.IsActive);
        });

        modelBuilder.Entity<TelegramBotSetting>(entity =>
        {
            entity.HasIndex(e => e.EventType).IsUnique();
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.Key);
        });

        modelBuilder.Entity<ChatConversation>(entity =>
        {
            entity.HasIndex(e => e.LastMessageAtUtc);
        });

        modelBuilder.Entity<ChatMember>(entity =>
        {
            entity.HasKey(e => new { e.ConversationId, e.UserId });
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.Conversation)
                .WithMany(c => c.Members)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasIndex(e => new { e.ConversationId, e.CreatedAtUtc });
            entity.Property(e => e.Body).HasMaxLength(8000);
            entity.Property(e => e.AttachmentUrl).HasMaxLength(512);
            entity.Property(e => e.AttachmentMimeType).HasMaxLength(128);
            entity.Property(e => e.AttachmentFileName).HasMaxLength(260);
            entity.HasOne(e => e.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserTicketReadState>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.TicketId }).IsUnique();
            entity.HasIndex(e => e.TicketId);
        });

        modelBuilder.Entity<UserChatReadState>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.ConversationId }).IsUnique();
            entity.HasIndex(e => e.ConversationId);
        });
    }
}

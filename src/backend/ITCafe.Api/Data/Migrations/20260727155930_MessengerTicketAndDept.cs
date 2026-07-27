using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITCafe.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MessengerTicketAndDept : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DepartmentSlug",
                table: "ChatConversations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TicketId",
                table: "ChatConversations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_DepartmentSlug",
                table: "ChatConversations",
                column: "DepartmentSlug");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_TicketId",
                table: "ChatConversations",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatConversations_DepartmentSlug",
                table: "ChatConversations");

            migrationBuilder.DropIndex(
                name: "IX_ChatConversations_TicketId",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "DepartmentSlug",
                table: "ChatConversations");

            migrationBuilder.DropColumn(
                name: "TicketId",
                table: "ChatConversations");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompanyCopilot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChatSessionOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chat_sessions_UpdatedAtUtc",
                table: "chat_sessions");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "chat_sessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_sessions_OwnerId_UpdatedAtUtc",
                table: "chat_sessions",
                columns: new[] { "OwnerId", "UpdatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chat_sessions_OwnerId_UpdatedAtUtc",
                table: "chat_sessions");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "chat_sessions");

            migrationBuilder.CreateIndex(
                name: "IX_chat_sessions_UpdatedAtUtc",
                table: "chat_sessions",
                column: "UpdatedAtUtc");
        }
    }
}

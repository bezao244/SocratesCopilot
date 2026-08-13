using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompanyCopilot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChatMessagesOrdinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chat_messages_SessionId_CreatedAtUtc",
                table: "chat_messages");

            migrationBuilder.AddColumn<int>(
                name: "Ordinal",
                table: "chat_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_SessionId_Ordinal",
                table: "chat_messages",
                columns: new[] { "SessionId", "Ordinal" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_chat_messages_SessionId_Ordinal",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "Ordinal",
                table: "chat_messages");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_SessionId_CreatedAtUtc",
                table: "chat_messages",
                columns: new[] { "SessionId", "CreatedAtUtc" });
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClanRatingTracker.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClanMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PersonalClanRating = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClanMembers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClanMembers_RecordedAt",
                table: "ClanMembers",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClanMembers_Username_RecordedAt",
                table: "ClanMembers",
                columns: new[] { "Username", "RecordedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClanMembers");
        }
    }
}

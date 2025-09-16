using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClanRatingTracker.Migrations
{
    /// <inheritdoc />
    public partial class SeparateMembersAndRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClanMembers_RecordedAt",
                table: "ClanMembers");

            migrationBuilder.DropIndex(
                name: "IX_ClanMembers_Username_RecordedAt",
                table: "ClanMembers");

            migrationBuilder.DropColumn(
                name: "PersonalClanRating",
                table: "ClanMembers");

            migrationBuilder.RenameColumn(
                name: "RecordedAt",
                table: "ClanMembers",
                newName: "LastSeen");

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstSeen",
                table: "ClanMembers",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ClanMembers",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "ClanRatings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClanMemberId = table.Column<int>(type: "INTEGER", nullable: false),
                    PersonalClanRating = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClanRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClanRatings_ClanMembers_ClanMemberId",
                        column: x => x.ClanMemberId,
                        principalTable: "ClanMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClanMembers_Username",
                table: "ClanMembers",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClanRatings_MemberId_RecordedAt",
                table: "ClanRatings",
                columns: new[] { "ClanMemberId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClanRatings_RecordedAt",
                table: "ClanRatings",
                column: "RecordedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClanRatings");

            migrationBuilder.DropIndex(
                name: "IX_ClanMembers_Username",
                table: "ClanMembers");

            migrationBuilder.DropColumn(
                name: "FirstSeen",
                table: "ClanMembers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ClanMembers");

            migrationBuilder.RenameColumn(
                name: "LastSeen",
                table: "ClanMembers",
                newName: "RecordedAt");

            migrationBuilder.AddColumn<int>(
                name: "PersonalClanRating",
                table: "ClanMembers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ClanMembers_RecordedAt",
                table: "ClanMembers",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClanMembers_Username_RecordedAt",
                table: "ClanMembers",
                columns: new[] { "Username", "RecordedAt" });
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class CatalogsAndSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Created",
                table: "Sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Device",
                table: "Sessions",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Não há data de criação nos registros 0.1. Usa o momento da migração.
            migrationBuilder.Sql("UPDATE \"Sessions\" SET \"Created\" = NOW() WHERE \"Created\" = '-infinity'::timestamptz;");

            migrationBuilder.CreateTable(
                name: "DataReleases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    Season = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    License = table.Column<string>(type: "text", nullable: false),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataReleases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubData",
                columns: table => new
                {
                    ReleaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Short = table.Column<string>(type: "text", nullable: false),
                    Color = table.Column<string>(type: "text", nullable: false),
                    Strength = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubData", x => new { x.ReleaseId, x.Id });
                    table.ForeignKey(
                        name: "FK_ClubData_DataReleases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "DataReleases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerData",
                columns: table => new
                {
                    ReleaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ClubId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<string>(type: "text", nullable: false),
                    Age = table.Column<int>(type: "integer", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    Wage = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerData", x => new { x.ReleaseId, x.Id });
                    table.ForeignKey(
                        name: "FK_PlayerData_DataReleases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalTable: "DataReleases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataReleases_Version",
                table: "DataReleases",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerData_ReleaseId_ClubId",
                table: "PlayerData",
                columns: new[] { "ReleaseId", "ClubId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubData");

            migrationBuilder.DropTable(
                name: "PlayerData");

            migrationBuilder.DropTable(
                name: "DataReleases");

            migrationBuilder.DropColumn(
                name: "Created",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "Device",
                table: "Sessions");
        }
    }
}

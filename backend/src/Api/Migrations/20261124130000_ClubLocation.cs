using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    [DbContext(typeof(Brasileirao.GameDb))]
    [Migration("20261124130000_ClubLocation")]
    public partial class ClubLocation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "City", table: "ClubData", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "Uf", table: "ClubData", type: "text", nullable: false, defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "City", table: "ClubData");
            migrationBuilder.DropColumn(name: "Uf", table: "ClubData");
        }
    }
}

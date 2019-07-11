using Microsoft.EntityFrameworkCore.Migrations;

namespace GaiaPins.Migrations
{
    public partial class NSFW : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludeNSFW",
                table: "Guilds",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludeNSFW",
                table: "Guilds");
        }
    }
}

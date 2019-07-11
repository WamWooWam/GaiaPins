using Microsoft.EntityFrameworkCore.Migrations;

namespace GaiaPins.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PinsChannelId = table.Column<long>(nullable: false),
                    WebhookId = table.Column<long>(nullable: false),
                    WebhookToken = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PinnedMessage",
                columns: table => new
                {
                    Id = table.Column<long>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(nullable: false),
                    NewMessageId = table.Column<long>(nullable: false),
                    GuildInfoId = table.Column<long>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PinnedMessage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PinnedMessage_Guilds_GuildInfoId",
                        column: x => x.GuildInfoId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PinnedMessage_GuildInfoId",
                table: "PinnedMessage",
                column: "GuildInfoId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PinnedMessage");

            migrationBuilder.DropTable(
                name: "Guilds");
        }
    }
}

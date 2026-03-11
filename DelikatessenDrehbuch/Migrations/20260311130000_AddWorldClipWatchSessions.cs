using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    public partial class AddWorldClipWatchSessions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorldClipWatchSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorldUserPostingId = table.Column<int>(type: "int", nullable: false),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    ViewerUserHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatorUserHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    WatchedSeconds = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    IsQualifiedView = table.Column<bool>(type: "bit", nullable: false),
                    SessionStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SessionEndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldClipWatchSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorldClipWatchSessions_WorldUserPosting_WorldUserPostingId",
                        column: x => x.WorldUserPostingId,
                        principalTable: "WorldUserPosting",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorldClipWatchSessions_CreatorUserHash_IsQualifiedView_CreatedAtUtc",
                table: "WorldClipWatchSessions",
                columns: new[] { "CreatorUserHash", "IsQualifiedView", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorldClipWatchSessions_ViewerUserHash_CreatedAtUtc",
                table: "WorldClipWatchSessions",
                columns: new[] { "ViewerUserHash", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorldClipWatchSessions_WorldUserPostingId",
                table: "WorldClipWatchSessions",
                column: "WorldUserPostingId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorldClipWatchSessions");
        }
    }
}


using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldUserBookmark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorldUserBookmark",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    WorldAppUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldUserBookmark", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorldUserBookmark_RecipeBaseData_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "RecipeBaseData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorldUserBookmark_WorldAppUser_WorldAppUserId",
                        column: x => x.WorldAppUserId,
                        principalTable: "WorldAppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorldUserBookmark_RecipeId",
                table: "WorldUserBookmark",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldUserBookmark_WorldAppUserId",
                table: "WorldUserBookmark",
                column: "WorldAppUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorldUserBookmark");
        }
    }
}

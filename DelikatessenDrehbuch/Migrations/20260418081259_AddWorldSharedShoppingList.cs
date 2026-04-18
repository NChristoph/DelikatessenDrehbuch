using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldSharedShoppingList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorldSharedShoppingList",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShareToken = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheckedJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldSharedShoppingList", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorldSharedShoppingList_ShareToken",
                table: "WorldSharedShoppingList",
                column: "ShareToken");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorldSharedShoppingList");
        }
    }
}

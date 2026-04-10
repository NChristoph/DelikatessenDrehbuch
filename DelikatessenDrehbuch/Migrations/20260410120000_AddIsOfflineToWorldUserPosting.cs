using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260410120000_AddIsOfflineToWorldUserPosting")]
    public partial class AddIsOfflineToWorldUserPosting : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOffline",
                table: "WorldUserPosting",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOffline",
                table: "WorldUserPosting");
        }
    }
}

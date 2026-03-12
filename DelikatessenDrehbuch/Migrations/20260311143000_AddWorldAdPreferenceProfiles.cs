using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    public partial class AddWorldAdPreferenceProfiles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorldAdPreferenceProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AllowPersonalizedAds = table.Column<bool>(type: "bit", nullable: false),
                    AllowCategoryTargeting = table.Column<bool>(type: "bit", nullable: false),
                    AllowKeywordTargeting = table.Column<bool>(type: "bit", nullable: false),
                    AllowCreatorTargeting = table.Column<bool>(type: "bit", nullable: false),
                    PreferredLanguage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TopCategoriesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TopKeywordsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TopCreatorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SegmentLabelsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QualifiedWatchCount = table.Column<int>(type: "int", nullable: false),
                    LikedRecipeCount = table.Column<int>(type: "int", nullable: false),
                    FollowedCreatorCount = table.Column<int>(type: "int", nullable: false),
                    LastProfileRefreshUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldAdPreferenceProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorldAdPreferenceInterests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InterestType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InterestKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayLabel = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Score = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    SignalCount = table.Column<int>(type: "int", nullable: false),
                    LastSignalAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldAdPreferenceInterests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorldAdPreferenceInterests_UserHash_InterestType_Score",
                table: "WorldAdPreferenceInterests",
                columns: new[] { "UserHash", "InterestType", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_WorldAdPreferenceProfiles_UserHash",
                table: "WorldAdPreferenceProfiles",
                column: "UserHash",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorldAdPreferenceInterests");

            migrationBuilder.DropTable(
                name: "WorldAdPreferenceProfiles");
        }
    }
}

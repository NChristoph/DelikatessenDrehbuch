using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    public partial class AddSmartRecipeSteps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmartRecipeSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MasterStepKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    VariablesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: true),
                    Equipment = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmartRecipeSteps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecipeJoinSmartSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    SmartRecipeStepId = table.Column<int>(type: "int", nullable: false),
                    StepIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeJoinSmartSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeJoinSmartSteps_RecipeBaseData_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "RecipeBaseData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeJoinSmartSteps_SmartRecipeSteps_SmartRecipeStepId",
                        column: x => x.SmartRecipeStepId,
                        principalTable: "SmartRecipeSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeJoinSmartSteps_RecipeId_StepIndex",
                table: "RecipeJoinSmartSteps",
                columns: new[] { "RecipeId", "StepIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeJoinSmartSteps_SmartRecipeStepId",
                table: "RecipeJoinSmartSteps",
                column: "SmartRecipeStepId");

            migrationBuilder.CreateIndex(
                name: "IX_SmartRecipeSteps_MasterStepKey_Phase_Equipment",
                table: "SmartRecipeSteps",
                columns: new[] { "MasterStepKey", "Phase", "Equipment" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeJoinSmartSteps");

            migrationBuilder.DropTable(
                name: "SmartRecipeSteps");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Data.Migrations
{
    public partial class nullableImagePath : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quantities_Measure_MeasureId",
                table: "Quantities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Measure",
                table: "Measure");

            migrationBuilder.RenameTable(
                name: "Measure",
                newName: "Metrics");

            migrationBuilder.AlterColumn<string>(
                name: "ImagePath",
                table: "Recipes",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "MeasureId",
                table: "Quantities",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Metrics",
                table: "Metrics",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Quantities_Metrics_MeasureId",
                table: "Quantities",
                column: "MeasureId",
                principalTable: "Metrics",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quantities_Metrics_MeasureId",
                table: "Quantities");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Metrics",
                table: "Metrics");

            migrationBuilder.RenameTable(
                name: "Metrics",
                newName: "Measure");

            migrationBuilder.AlterColumn<string>(
                name: "ImagePath",
                table: "Recipes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "MeasureId",
                table: "Quantities",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Measure",
                table: "Measure",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Quantities_Measure_MeasureId",
                table: "Quantities",
                column: "MeasureId",
                principalTable: "Measure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

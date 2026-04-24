using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldUserCommentIsPinned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'[dbo].[WorldUserComments]', N'IsPinned') IS NULL
BEGIN
    ALTER TABLE [dbo].[WorldUserComments]
        ADD [IsPinned] BIT NOT NULL CONSTRAINT [DF_WorldUserComments_IsPinned] DEFAULT (0);
END;

IF COL_LENGTH(N'[dbo].[RecipeAiBaseRecipes]', N'AcceptCount') IS NULL
BEGIN
    ALTER TABLE [dbo].[RecipeAiBaseRecipes] ADD [AcceptCount] INT NOT NULL DEFAULT (0);
END;

IF COL_LENGTH(N'[dbo].[RecipeAiBaseRecipes]', N'ServedCount') IS NULL
BEGIN
    ALTER TABLE [dbo].[RecipeAiBaseRecipes] ADD [ServedCount] INT NOT NULL DEFAULT (0);
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "WorldUserComments");

            migrationBuilder.DropColumn(
                name: "AcceptCount",
                table: "RecipeAiBaseRecipes");

            migrationBuilder.DropColumn(
                name: "ServedCount",
                table: "RecipeAiBaseRecipes");
        }
    }
}

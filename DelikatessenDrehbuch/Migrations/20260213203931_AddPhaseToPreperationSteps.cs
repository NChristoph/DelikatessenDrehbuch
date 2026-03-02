using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DelikatessenDrehbuch.Migrations
{
    /// <inheritdoc />
    public partial class AddPhaseToPreperationSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Group",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Group", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Keywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Word_DE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Word_EN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Word_ESP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Word_PRT = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Keywords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MealplanFilter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Filter = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealplanFilter", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Metrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitOfMeasurement = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MyMealModel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MealCategory = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyMealModel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nutrients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nutrient = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nutrients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Quantities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Quantitys = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quantities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Querys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Query = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Querys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecipeBaseData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PersonCount = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Preferences = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreperationTime = table.Column<int>(type: "int", nullable: false),
                    LikeCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeBaseData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecipePreperationSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Step_DE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Step_EN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Step_PRT = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Step_ESP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipePreperationSteps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Recipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Preparation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreparationTime = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Calories = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LikeCount = table.Column<int>(type: "int", nullable: true),
                    RecipePersonCount = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recipes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavedMealPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserMail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MealPlanJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PersonCount = table.Column<int>(type: "int", nullable: false),
                    ShoppingList = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Token = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserSettingJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedMealPlan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportMessage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TypeId = table.Column<int>(type: "int", nullable: false),
                    SenderEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportMessage", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferencesQuerys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Query = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferencesQuerys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorldAppUser",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsVerified = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Lastlogin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RememberLogin = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldAppUser", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorldSharedMealPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShareToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PersonCount = table.Column<int>(type: "int", nullable: false),
                    MealPlanJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShoppingListJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldSharedMealPlan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorldUserMealPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Settings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MealPlan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldUserMealPlan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AverageWeight = table.Column<float>(type: "real", nullable: true),
                    Calories = table.Column<float>(type: "real", nullable: true),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    GrammOnly = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ingredients_Group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Group",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientsAndNutrients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name_DE = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name_EN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name_PRT = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name_ESP = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Calories_a_100g = table.Column<int>(type: "int", nullable: false),
                    Weight_per_piece = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    Fat_a_100g = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Saturated_fat_a_100g = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Carbohydrates_a_100g = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Sugar_a_100g = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Salt_a_100g = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Protein_a_100g = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Fiber_a_100g = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientsAndNutrients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientsAndNutrients_Group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Group",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MealPlan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Preperation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MyMealModelId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlan_MyMealModel_MyMealModelId",
                        column: x => x.MyMealModelId,
                        principalTable: "MyMealModel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeBaseDataImage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    Image = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorldAppImage = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeBaseDataImage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeBaseDataImage_RecipeBaseData_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "RecipeBaseData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeBaseKeywords",
                columns: table => new
                {
                    RecipeBaseDataId = table.Column<int>(type: "int", nullable: false),
                    KeywordId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeBaseKeywords", x => new { x.RecipeBaseDataId, x.KeywordId });
                    table.ForeignKey(
                        name: "FK_RecipeBaseKeywords_Keywords_KeywordId",
                        column: x => x.KeywordId,
                        principalTable: "Keywords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeBaseKeywords_RecipeBaseData_RecipeBaseDataId",
                        column: x => x.RecipeBaseDataId,
                        principalTable: "RecipeBaseData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorldUserPosting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatorId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatorName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecipeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldUserPosting", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorldUserPosting_RecipeBaseData_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "RecipeBaseData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeJoinPreperationSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    RecipePreperationStepId = table.Column<int>(type: "int", nullable: false),
                    StepIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeJoinPreperationSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeJoinPreperationSteps_RecipeBaseData_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "RecipeBaseData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeJoinPreperationSteps_RecipePreperationSteps_RecipePreperationStepId",
                        column: x => x.RecipePreperationStepId,
                        principalTable: "RecipePreperationSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Likes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserMail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecipeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Likes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Likes_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QueryHandler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    QueryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueryHandler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QueryHandler_Querys_QueryId",
                        column: x => x.QueryId,
                        principalTable: "Querys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QueryHandler_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Recessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecipesId = table.Column<int>(type: "int", nullable: false),
                    Assessment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recessions_Recipes_RecipesId",
                        column: x => x.RecipesId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferencesRecipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecipesId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferencesRecipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPreferencesRecipes_Recipes_RecipesId",
                        column: x => x.RecipesId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorldUserAbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorldUserId = table.Column<int>(type: "int", nullable: false),
                    CreatorId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldUserAbo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorldUserAbo_WorldAppUser_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "WorldAppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorldUserAbo_WorldAppUser_WorldUserId",
                        column: x => x.WorldUserId,
                        principalTable: "WorldAppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorldUserLike",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    WorldAppUserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldUserLike", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorldUserLike_RecipeBaseData_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "RecipeBaseData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorldUserLike_WorldAppUser_WorldAppUserId",
                        column: x => x.WorldAppUserId,
                        principalTable: "WorldAppUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientHandlers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    QuantityId = table.Column<int>(type: "int", nullable: false),
                    MeasureId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientHandlers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientHandlers_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientHandlers_Metrics_MeasureId",
                        column: x => x.MeasureId,
                        principalTable: "Metrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientHandlers_Quantities_QuantityId",
                        column: x => x.QuantityId,
                        principalTable: "Quantities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NutrienHandler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    NutrientsId = table.Column<int>(type: "int", nullable: false),
                    QuantityId = table.Column<int>(type: "int", nullable: false),
                    MetricsId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NutrienHandler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NutrienHandler_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NutrienHandler_Metrics_MetricsId",
                        column: x => x.MetricsId,
                        principalTable: "Metrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NutrienHandler_Nutrients_NutrientsId",
                        column: x => x.NutrientsId,
                        principalTable: "Nutrients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NutrienHandler_Quantities_QuantityId",
                        column: x => x.QuantityId,
                        principalTable: "Quantities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngredientMeasureQuantity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IngredientsAndNutrientsId = table.Column<int>(type: "int", nullable: false),
                    QuantityId = table.Column<int>(type: "int", nullable: false),
                    MeasureId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientMeasureQuantity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientMeasureQuantity_IngredientsAndNutrients_IngredientsAndNutrientsId",
                        column: x => x.IngredientsAndNutrientsId,
                        principalTable: "IngredientsAndNutrients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientMeasureQuantity_Metrics_MeasureId",
                        column: x => x.MeasureId,
                        principalTable: "Metrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IngredientMeasureQuantity_Quantities_QuantityId",
                        column: x => x.QuantityId,
                        principalTable: "Quantities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JoinIngredientPreperationStep",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PreperationId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinIngredientPreperationStep", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JoinIngredientPreperationStep_IngredientsAndNutrients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "IngredientsAndNutrients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JoinIngredientPreperationStep_RecipePreperationSteps_PreperationId",
                        column: x => x.PreperationId,
                        principalTable: "RecipePreperationSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MealPlanHandler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipesId = table.Column<int>(type: "int", nullable: true),
                    MealPlanId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealPlanHandler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealPlanHandler_MealPlan_MealPlanId",
                        column: x => x.MealPlanId,
                        principalTable: "MealPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MealPlanHandler_Recipes_RecipesId",
                        column: x => x.RecipesId,
                        principalTable: "Recipes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RecipesHandlers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    IngredientHandlerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipesHandlers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipesHandlers_IngredientHandlers_IngredientHandlerId",
                        column: x => x.IngredientHandlerId,
                        principalTable: "IngredientHandlers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RecipesHandlers_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecipeJoinIngredientMeasureQuantity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeJoinIngredientMeasureQuantity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeJoinIngredientMeasureQuantity_IngredientMeasureQuantity_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "IngredientMeasureQuantity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecipeJoinIngredientMeasureQuantity_RecipeBaseData_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "RecipeBaseData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Phase heuristic: set Phase based on Step_DE keywords
            // Phase 1 = Vorbereitung
            migrationBuilder.Sql(@"
                UPDATE RecipePreperationSteps SET Phase = 1
                WHERE Phase = 0 AND (
                    Step_DE LIKE '%schneid%' OR Step_DE LIKE N'%schäl%' OR Step_DE LIKE '%wasch%'
                    OR Step_DE LIKE N'%würfel%' OR Step_DE LIKE '%reib%' OR Step_DE LIKE '%einweich%'
                    OR Step_DE LIKE '%hack%' OR Step_DE LIKE '%zerklein%' OR Step_DE LIKE '%hobel%'
                    OR Step_DE LIKE '%pell%' OR Step_DE LIKE '%entkern%' OR Step_DE LIKE '%filetier%'
                    OR Step_DE LIKE '%vorbereite%' OR Step_DE LIKE '%halbier%' OR Step_DE LIKE '%vierteln%'
                )");

            // Phase 2 = Kochen
            migrationBuilder.Sql(@"
                UPDATE RecipePreperationSteps SET Phase = 2
                WHERE Phase = 0 AND (
                    Step_DE LIKE '%anbrat%' OR Step_DE LIKE '%koch%' OR Step_DE LIKE N'%dünst%'
                    OR Step_DE LIKE '%back%' OR Step_DE LIKE '%grill%' OR Step_DE LIKE '%frittier%'
                    OR Step_DE LIKE '%blanchier%' OR Step_DE LIKE '%brat%' OR Step_DE LIKE '%schmo%'
                    OR Step_DE LIKE '%gart%' OR Step_DE LIKE '%garen%' OR Step_DE LIKE '%simmern%'
                    OR Step_DE LIKE '%aufkochen%' OR Step_DE LIKE N'%röst%' OR Step_DE LIKE '%dampf%'
                    OR Step_DE LIKE '%reduzier%' OR Step_DE LIKE '%karamellis%'
                )");

            // Phase 3 = Würzen
            migrationBuilder.Sql(@"
                UPDATE RecipePreperationSteps SET Phase = 3
                WHERE Phase = 0 AND (
                    Step_DE LIKE '%salz%' OR Step_DE LIKE '%pfeff%' OR Step_DE LIKE N'%würz%'
                    OR Step_DE LIKE '%abschmeck%' OR Step_DE LIKE '%marinier%'
                    OR Step_DE LIKE '%beträufel%' OR Step_DE LIKE '%bestreu%'
                )");

            // Phase 4 = Finish
            migrationBuilder.Sql(@"
                UPDATE RecipePreperationSteps SET Phase = 4
                WHERE Phase = 0 AND (
                    Step_DE LIKE '%anricht%' OR Step_DE LIKE '%garnier%' OR Step_DE LIKE '%servier%'
                    OR Step_DE LIKE N'%auskühlen%' OR Step_DE LIKE '%dekorier%'
                    OR Step_DE LIKE '%portionier%'
                )");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientHandlers_IngredientId",
                table: "IngredientHandlers",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientHandlers_MeasureId",
                table: "IngredientHandlers",
                column: "MeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientHandlers_QuantityId",
                table: "IngredientHandlers",
                column: "QuantityId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMeasureQuantity_IngredientsAndNutrientsId",
                table: "IngredientMeasureQuantity",
                column: "IngredientsAndNutrientsId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMeasureQuantity_MeasureId",
                table: "IngredientMeasureQuantity",
                column: "MeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMeasureQuantity_QuantityId",
                table: "IngredientMeasureQuantity",
                column: "QuantityId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_GroupId",
                table: "Ingredients",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientsAndNutrients_GroupId",
                table: "IngredientsAndNutrients",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_JoinIngredientPreperationStep_IngredientId",
                table: "JoinIngredientPreperationStep",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_JoinIngredientPreperationStep_PreperationId",
                table: "JoinIngredientPreperationStep",
                column: "PreperationId");

            migrationBuilder.CreateIndex(
                name: "IX_Likes_RecipeId",
                table: "Likes",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlan_MyMealModelId",
                table: "MealPlan",
                column: "MyMealModelId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanHandler_MealPlanId",
                table: "MealPlanHandler",
                column: "MealPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MealPlanHandler_RecipesId",
                table: "MealPlanHandler",
                column: "RecipesId");

            migrationBuilder.CreateIndex(
                name: "IX_NutrienHandler_IngredientId",
                table: "NutrienHandler",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_NutrienHandler_MetricsId",
                table: "NutrienHandler",
                column: "MetricsId");

            migrationBuilder.CreateIndex(
                name: "IX_NutrienHandler_NutrientsId",
                table: "NutrienHandler",
                column: "NutrientsId");

            migrationBuilder.CreateIndex(
                name: "IX_NutrienHandler_QuantityId",
                table: "NutrienHandler",
                column: "QuantityId");

            migrationBuilder.CreateIndex(
                name: "IX_QueryHandler_QueryId",
                table: "QueryHandler",
                column: "QueryId");

            migrationBuilder.CreateIndex(
                name: "IX_QueryHandler_RecipeId",
                table: "QueryHandler",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_Recessions_RecipesId",
                table: "Recessions",
                column: "RecipesId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeBaseDataImage_RecipeId",
                table: "RecipeBaseDataImage",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeBaseKeywords_KeywordId",
                table: "RecipeBaseKeywords",
                column: "KeywordId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeJoinIngredientMeasureQuantity_IngredientId",
                table: "RecipeJoinIngredientMeasureQuantity",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeJoinIngredientMeasureQuantity_RecipeId",
                table: "RecipeJoinIngredientMeasureQuantity",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeJoinPreperationSteps_RecipeId",
                table: "RecipeJoinPreperationSteps",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeJoinPreperationSteps_RecipePreperationStepId",
                table: "RecipeJoinPreperationSteps",
                column: "RecipePreperationStepId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipesHandlers_IngredientHandlerId",
                table: "RecipesHandlers",
                column: "IngredientHandlerId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipesHandlers_RecipeId",
                table: "RecipesHandlers",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferencesRecipes_RecipesId",
                table: "UserPreferencesRecipes",
                column: "RecipesId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldUserAbo_CreatorId",
                table: "WorldUserAbo",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldUserAbo_WorldUserId",
                table: "WorldUserAbo",
                column: "WorldUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldUserLike_RecipeId",
                table: "WorldUserLike",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldUserLike_WorldAppUserId",
                table: "WorldUserLike",
                column: "WorldAppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldUserPosting_RecipeId",
                table: "WorldUserPosting",
                column: "RecipeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "JoinIngredientPreperationStep");

            migrationBuilder.DropTable(
                name: "Likes");

            migrationBuilder.DropTable(
                name: "MealplanFilter");

            migrationBuilder.DropTable(
                name: "MealPlanHandler");

            migrationBuilder.DropTable(
                name: "NutrienHandler");

            migrationBuilder.DropTable(
                name: "QueryHandler");

            migrationBuilder.DropTable(
                name: "Recessions");

            migrationBuilder.DropTable(
                name: "RecipeBaseDataImage");

            migrationBuilder.DropTable(
                name: "RecipeBaseKeywords");

            migrationBuilder.DropTable(
                name: "RecipeJoinIngredientMeasureQuantity");

            migrationBuilder.DropTable(
                name: "RecipeJoinPreperationSteps");

            migrationBuilder.DropTable(
                name: "RecipesHandlers");

            migrationBuilder.DropTable(
                name: "SavedMealPlan");

            migrationBuilder.DropTable(
                name: "SupportMessage");

            migrationBuilder.DropTable(
                name: "UserPreferencesQuerys");

            migrationBuilder.DropTable(
                name: "UserPreferencesRecipes");

            migrationBuilder.DropTable(
                name: "WorldSharedMealPlan");

            migrationBuilder.DropTable(
                name: "WorldUserAbo");

            migrationBuilder.DropTable(
                name: "WorldUserLike");

            migrationBuilder.DropTable(
                name: "WorldUserMealPlan");

            migrationBuilder.DropTable(
                name: "WorldUserPosting");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "MealPlan");

            migrationBuilder.DropTable(
                name: "Nutrients");

            migrationBuilder.DropTable(
                name: "Querys");

            migrationBuilder.DropTable(
                name: "Keywords");

            migrationBuilder.DropTable(
                name: "IngredientMeasureQuantity");

            migrationBuilder.DropTable(
                name: "RecipePreperationSteps");

            migrationBuilder.DropTable(
                name: "IngredientHandlers");

            migrationBuilder.DropTable(
                name: "Recipes");

            migrationBuilder.DropTable(
                name: "WorldAppUser");

            migrationBuilder.DropTable(
                name: "RecipeBaseData");

            migrationBuilder.DropTable(
                name: "MyMealModel");

            migrationBuilder.DropTable(
                name: "IngredientsAndNutrients");

            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "Metrics");

            migrationBuilder.DropTable(
                name: "Quantities");

            migrationBuilder.DropTable(
                name: "Group");
        }
    }
}

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Group] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Group] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Keywords] (
    [Id] int NOT NULL IDENTITY,
    [Word_DE] nvarchar(max) NOT NULL,
    [Word_EN] nvarchar(max) NOT NULL,
    [Word_ESP] nvarchar(max) NOT NULL,
    [Word_PRT] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Keywords] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [MealplanFilter] (
    [Id] int NOT NULL IDENTITY,
    [Filter] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_MealplanFilter] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Metrics] (
    [Id] int NOT NULL IDENTITY,
    [UnitOfMeasurement] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Metrics] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [MyMealModel] (
    [Id] int NOT NULL IDENTITY,
    [Category] nvarchar(max) NOT NULL,
    [MealCategory] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_MyMealModel] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Nutrients] (
    [Id] int NOT NULL IDENTITY,
    [Nutrient] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Nutrients] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Quantities] (
    [Id] int NOT NULL IDENTITY,
    [Quantitys] float NOT NULL,
    CONSTRAINT [PK_Quantities] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Querys] (
    [Id] int NOT NULL IDENTITY,
    [Query] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Querys] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [RecipeBaseData] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(max) NOT NULL,
    [PersonCount] int NOT NULL,
    [Category] nvarchar(max) NOT NULL,
    [Preferences] nvarchar(max) NOT NULL,
    [PreperationTime] int NOT NULL,
    [LikeCount] int NOT NULL,
    CONSTRAINT [PK_RecipeBaseData] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [RecipePreperationSteps] (
    [Id] int NOT NULL IDENTITY,
    [Step_DE] nvarchar(max) NOT NULL,
    [Step_EN] nvarchar(max) NOT NULL,
    [Step_PRT] nvarchar(max) NOT NULL,
    [Step_ESP] nvarchar(max) NOT NULL,
    [Phase] int NOT NULL,
    CONSTRAINT [PK_RecipePreperationSteps] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [Recipes] (
    [Id] int NOT NULL IDENTITY,
    [Category] nvarchar(max) NULL,
    [OwnerEmail] nvarchar(max) NULL,
    [Name] nvarchar(max) NOT NULL,
    [Preparation] nvarchar(max) NOT NULL,
    [ImagePath] nvarchar(max) NULL,
    [PreparationTime] int NULL,
    [Description] nvarchar(max) NULL,
    [Calories] nvarchar(max) NULL,
    [LikeCount] int NULL,
    [RecipePersonCount] int NULL,
    CONSTRAINT [PK_Recipes] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [SavedMealPlan] (
    [Id] int NOT NULL IDENTITY,
    [UserMail] nvarchar(max) NOT NULL,
    [MealPlanJson] nvarchar(max) NOT NULL,
    [CreationTime] datetime2 NOT NULL,
    [PersonCount] int NOT NULL,
    [ShoppingList] nvarchar(max) NULL,
    [Token] nvarchar(max) NULL,
    [UserSettingJson] nvarchar(max) NULL,
    CONSTRAINT [PK_SavedMealPlan] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [SupportMessage] (
    [Id] int NOT NULL IDENTITY,
    [Type] nvarchar(max) NOT NULL,
    [TypeId] int NOT NULL,
    [SenderEmail] nvarchar(max) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_SupportMessage] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [UserPreferencesQuerys] (
    [Id] int NOT NULL IDENTITY,
    [UserEmail] nvarchar(max) NOT NULL,
    [Query] nvarchar(max) NOT NULL,
    [Count] int NOT NULL,
    CONSTRAINT [PK_UserPreferencesQuerys] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [WorldAppUser] (
    [Id] int NOT NULL IDENTITY,
    [UserHash] nvarchar(max) NULL,
    [UserName] nvarchar(max) NULL,
    [IsVerified] nvarchar(max) NULL,
    [Lastlogin] datetime2 NULL,
    [RememberLogin] bit NOT NULL,
    CONSTRAINT [PK_WorldAppUser] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [WorldSharedMealPlan] (
    [Id] int NOT NULL IDENTITY,
    [ShareToken] nvarchar(max) NOT NULL,
    [UserHash] nvarchar(max) NULL,
    [Title] nvarchar(max) NOT NULL,
    [PersonCount] int NOT NULL,
    [MealPlanJson] nvarchar(max) NOT NULL,
    [ShoppingListJson] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_WorldSharedMealPlan] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [WorldUserMealPlan] (
    [Id] int NOT NULL IDENTITY,
    [UserHash] nvarchar(max) NOT NULL,
    [Settings] nvarchar(max) NULL,
    [CreationTime] datetime2 NOT NULL,
    [MealPlan] nvarchar(max) NULL,
    [Title] nvarchar(max) NULL,
    CONSTRAINT [PK_WorldUserMealPlan] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(128) NOT NULL,
    [ProviderKey] nvarchar(128) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(128) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Ingredients] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [AverageWeight] real NULL,
    [Calories] real NULL,
    [GroupId] int NOT NULL,
    [GrammOnly] bit NULL,
    CONSTRAINT [PK_Ingredients] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Ingredients_Group_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Group] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [IngredientsAndNutrients] (
    [Id] int NOT NULL IDENTITY,
    [Name_DE] nvarchar(max) NOT NULL,
    [Name_EN] nvarchar(max) NOT NULL,
    [Name_PRT] nvarchar(max) NOT NULL,
    [Name_ESP] nvarchar(max) NOT NULL,
    [Calories_a_100g] int NOT NULL,
    [Weight_per_piece] int NOT NULL,
    [GroupId] int NULL,
    [Fat_a_100g] decimal(18,2) NOT NULL,
    [Saturated_fat_a_100g] decimal(18,2) NOT NULL,
    [Carbohydrates_a_100g] decimal(18,2) NOT NULL,
    [Sugar_a_100g] decimal(18,2) NOT NULL,
    [Salt_a_100g] decimal(18,2) NOT NULL,
    [Protein_a_100g] decimal(18,2) NOT NULL,
    [Fiber_a_100g] decimal(18,2) NOT NULL,
    CONSTRAINT [PK_IngredientsAndNutrients] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IngredientsAndNutrients_Group_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [Group] ([Id])
);
GO

CREATE TABLE [MealPlan] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [Preperation] nvarchar(max) NULL,
    [MyMealModelId] int NOT NULL,
    CONSTRAINT [PK_MealPlan] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MealPlan_MyMealModel_MyMealModelId] FOREIGN KEY ([MyMealModelId]) REFERENCES [MyMealModel] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [RecipeBaseDataImage] (
    [Id] int NOT NULL IDENTITY,
    [RecipeId] int NOT NULL,
    [Image] nvarchar(max) NOT NULL,
    [WorldAppImage] bit NOT NULL,
    CONSTRAINT [PK_RecipeBaseDataImage] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecipeBaseDataImage_RecipeBaseData_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [RecipeBaseData] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [RecipeBaseKeywords] (
    [RecipeBaseDataId] int NOT NULL,
    [KeywordId] int NOT NULL,
    CONSTRAINT [PK_RecipeBaseKeywords] PRIMARY KEY ([RecipeBaseDataId], [KeywordId]),
    CONSTRAINT [FK_RecipeBaseKeywords_Keywords_KeywordId] FOREIGN KEY ([KeywordId]) REFERENCES [Keywords] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RecipeBaseKeywords_RecipeBaseData_RecipeBaseDataId] FOREIGN KEY ([RecipeBaseDataId]) REFERENCES [RecipeBaseData] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorldUserPosting] (
    [Id] int NOT NULL IDENTITY,
    [CreatorId] nvarchar(max) NOT NULL,
    [CreatorName] nvarchar(max) NOT NULL,
    [Source] nvarchar(max) NOT NULL,
    [ThumbnailUrl] nvarchar(max) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [CreationTime] datetime2 NOT NULL,
    [RecipeId] int NOT NULL,
    CONSTRAINT [PK_WorldUserPosting] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorldUserPosting_RecipeBaseData_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [RecipeBaseData] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [RecipeJoinPreperationSteps] (
    [Id] int NOT NULL IDENTITY,
    [RecipeId] int NOT NULL,
    [RecipePreperationStepId] int NOT NULL,
    [StepIndex] int NOT NULL,
    CONSTRAINT [PK_RecipeJoinPreperationSteps] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecipeJoinPreperationSteps_RecipeBaseData_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [RecipeBaseData] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RecipeJoinPreperationSteps_RecipePreperationSteps_RecipePreperationStepId] FOREIGN KEY ([RecipePreperationStepId]) REFERENCES [RecipePreperationSteps] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Likes] (
    [Id] int NOT NULL IDENTITY,
    [UserMail] nvarchar(max) NOT NULL,
    [RecipeId] int NOT NULL,
    CONSTRAINT [PK_Likes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Likes_Recipes_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [Recipes] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [QueryHandler] (
    [Id] int NOT NULL IDENTITY,
    [RecipeId] int NOT NULL,
    [QueryId] int NOT NULL,
    CONSTRAINT [PK_QueryHandler] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_QueryHandler_Querys_QueryId] FOREIGN KEY ([QueryId]) REFERENCES [Querys] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_QueryHandler_Recipes_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [Recipes] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [Recessions] (
    [Id] int NOT NULL IDENTITY,
    [UserEmail] nvarchar(max) NOT NULL,
    [RecipesId] int NOT NULL,
    [Assessment] nvarchar(max) NOT NULL,
    [CreationDate] datetime2 NOT NULL,
    CONSTRAINT [PK_Recessions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Recessions_Recipes_RecipesId] FOREIGN KEY ([RecipesId]) REFERENCES [Recipes] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [UserPreferencesRecipes] (
    [Id] int NOT NULL IDENTITY,
    [UserEmail] nvarchar(max) NOT NULL,
    [RecipesId] int NOT NULL,
    CONSTRAINT [PK_UserPreferencesRecipes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserPreferencesRecipes_Recipes_RecipesId] FOREIGN KEY ([RecipesId]) REFERENCES [Recipes] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorldUserAbo] (
    [Id] int NOT NULL IDENTITY,
    [WorldUserId] int NOT NULL,
    [CreatorId] int NOT NULL,
    CONSTRAINT [PK_WorldUserAbo] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorldUserAbo_WorldAppUser_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [WorldAppUser] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WorldUserAbo_WorldAppUser_WorldUserId] FOREIGN KEY ([WorldUserId]) REFERENCES [WorldAppUser] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [WorldUserLike] (
    [Id] int NOT NULL IDENTITY,
    [RecipeId] int NOT NULL,
    [WorldAppUserId] int NOT NULL,
    CONSTRAINT [PK_WorldUserLike] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorldUserLike_RecipeBaseData_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [RecipeBaseData] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WorldUserLike_WorldAppUser_WorldAppUserId] FOREIGN KEY ([WorldAppUserId]) REFERENCES [WorldAppUser] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [IngredientHandlers] (
    [Id] int NOT NULL IDENTITY,
    [IngredientId] int NOT NULL,
    [QuantityId] int NOT NULL,
    [MeasureId] int NOT NULL,
    CONSTRAINT [PK_IngredientHandlers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IngredientHandlers_Ingredients_IngredientId] FOREIGN KEY ([IngredientId]) REFERENCES [Ingredients] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_IngredientHandlers_Metrics_MeasureId] FOREIGN KEY ([MeasureId]) REFERENCES [Metrics] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_IngredientHandlers_Quantities_QuantityId] FOREIGN KEY ([QuantityId]) REFERENCES [Quantities] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [NutrienHandler] (
    [Id] int NOT NULL IDENTITY,
    [IngredientId] int NOT NULL,
    [NutrientsId] int NOT NULL,
    [QuantityId] int NOT NULL,
    [MetricsId] int NOT NULL,
    CONSTRAINT [PK_NutrienHandler] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_NutrienHandler_Ingredients_IngredientId] FOREIGN KEY ([IngredientId]) REFERENCES [Ingredients] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_NutrienHandler_Metrics_MetricsId] FOREIGN KEY ([MetricsId]) REFERENCES [Metrics] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_NutrienHandler_Nutrients_NutrientsId] FOREIGN KEY ([NutrientsId]) REFERENCES [Nutrients] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_NutrienHandler_Quantities_QuantityId] FOREIGN KEY ([QuantityId]) REFERENCES [Quantities] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [IngredientMeasureQuantity] (
    [Id] int NOT NULL IDENTITY,
    [IngredientsAndNutrientsId] int NOT NULL,
    [QuantityId] int NOT NULL,
    [MeasureId] int NOT NULL,
    CONSTRAINT [PK_IngredientMeasureQuantity] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_IngredientMeasureQuantity_IngredientsAndNutrients_IngredientsAndNutrientsId] FOREIGN KEY ([IngredientsAndNutrientsId]) REFERENCES [IngredientsAndNutrients] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_IngredientMeasureQuantity_Metrics_MeasureId] FOREIGN KEY ([MeasureId]) REFERENCES [Metrics] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_IngredientMeasureQuantity_Quantities_QuantityId] FOREIGN KEY ([QuantityId]) REFERENCES [Quantities] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [JoinIngredientPreperationStep] (
    [Id] int NOT NULL IDENTITY,
    [PreperationId] int NOT NULL,
    [IngredientId] int NOT NULL,
    CONSTRAINT [PK_JoinIngredientPreperationStep] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_JoinIngredientPreperationStep_IngredientsAndNutrients_IngredientId] FOREIGN KEY ([IngredientId]) REFERENCES [IngredientsAndNutrients] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_JoinIngredientPreperationStep_RecipePreperationSteps_PreperationId] FOREIGN KEY ([PreperationId]) REFERENCES [RecipePreperationSteps] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [MealPlanHandler] (
    [Id] int NOT NULL IDENTITY,
    [RecipesId] int NULL,
    [MealPlanId] int NOT NULL,
    CONSTRAINT [PK_MealPlanHandler] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_MealPlanHandler_MealPlan_MealPlanId] FOREIGN KEY ([MealPlanId]) REFERENCES [MealPlan] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_MealPlanHandler_Recipes_RecipesId] FOREIGN KEY ([RecipesId]) REFERENCES [Recipes] ([Id])
);
GO

CREATE TABLE [RecipesHandlers] (
    [Id] int NOT NULL IDENTITY,
    [RecipeId] int NOT NULL,
    [IngredientHandlerId] int NULL,
    CONSTRAINT [PK_RecipesHandlers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecipesHandlers_IngredientHandlers_IngredientHandlerId] FOREIGN KEY ([IngredientHandlerId]) REFERENCES [IngredientHandlers] ([Id]),
    CONSTRAINT [FK_RecipesHandlers_Recipes_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [Recipes] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [RecipeJoinIngredientMeasureQuantity] (
    [Id] int NOT NULL IDENTITY,
    [RecipeId] int NOT NULL,
    [IngredientId] int NOT NULL,
    CONSTRAINT [PK_RecipeJoinIngredientMeasureQuantity] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecipeJoinIngredientMeasureQuantity_IngredientMeasureQuantity_IngredientId] FOREIGN KEY ([IngredientId]) REFERENCES [IngredientMeasureQuantity] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RecipeJoinIngredientMeasureQuantity_RecipeBaseData_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [RecipeBaseData] ([Id]) ON DELETE CASCADE
);
GO


                UPDATE RecipePreperationSteps SET Phase = 1
                WHERE Phase = 0 AND (
                    Step_DE LIKE '%schneid%' OR Step_DE LIKE N'%schäl%' OR Step_DE LIKE '%wasch%'
                    OR Step_DE LIKE N'%würfel%' OR Step_DE LIKE '%reib%' OR Step_DE LIKE '%einweich%'
                    OR Step_DE LIKE '%hack%' OR Step_DE LIKE '%zerklein%' OR Step_DE LIKE '%hobel%'
                    OR Step_DE LIKE '%pell%' OR Step_DE LIKE '%entkern%' OR Step_DE LIKE '%filetier%'
                    OR Step_DE LIKE '%vorbereite%' OR Step_DE LIKE '%halbier%' OR Step_DE LIKE '%vierteln%'
                )
GO


                UPDATE RecipePreperationSteps SET Phase = 2
                WHERE Phase = 0 AND (
                    Step_DE LIKE '%anbrat%' OR Step_DE LIKE '%koch%' OR Step_DE LIKE N'%dünst%'
                    OR Step_DE LIKE '%back%' OR Step_DE LIKE '%grill%' OR Step_DE LIKE '%frittier%'
                    OR Step_DE LIKE '%blanchier%' OR Step_DE LIKE '%brat%' OR Step_DE LIKE '%schmo%'
                    OR Step_DE LIKE '%gart%' OR Step_DE LIKE '%garen%' OR Step_DE LIKE '%simmern%'
                    OR Step_DE LIKE '%aufkochen%' OR Step_DE LIKE N'%röst%' OR Step_DE LIKE '%dampf%'
                    OR Step_DE LIKE '%reduzier%' OR Step_DE LIKE '%karamellis%'
                )
GO


                UPDATE RecipePreperationSteps SET Phase = 3
                WHERE Phase = 0 AND (
                    Step_DE LIKE '%salz%' OR Step_DE LIKE '%pfeff%' OR Step_DE LIKE N'%würz%'
                    OR Step_DE LIKE '%abschmeck%' OR Step_DE LIKE '%marinier%'
                    OR Step_DE LIKE '%beträufel%' OR Step_DE LIKE '%bestreu%'
                )
GO


                UPDATE RecipePreperationSteps SET Phase = 4
                WHERE Phase = 0 AND (
                    Step_DE LIKE '%anricht%' OR Step_DE LIKE '%garnier%' OR Step_DE LIKE '%servier%'
                    OR Step_DE LIKE N'%auskühlen%' OR Step_DE LIKE '%dekorier%'
                    OR Step_DE LIKE '%portionier%'
                )
GO

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
GO

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO

CREATE INDEX [IX_IngredientHandlers_IngredientId] ON [IngredientHandlers] ([IngredientId]);
GO

CREATE INDEX [IX_IngredientHandlers_MeasureId] ON [IngredientHandlers] ([MeasureId]);
GO

CREATE INDEX [IX_IngredientHandlers_QuantityId] ON [IngredientHandlers] ([QuantityId]);
GO

CREATE INDEX [IX_IngredientMeasureQuantity_IngredientsAndNutrientsId] ON [IngredientMeasureQuantity] ([IngredientsAndNutrientsId]);
GO

CREATE INDEX [IX_IngredientMeasureQuantity_MeasureId] ON [IngredientMeasureQuantity] ([MeasureId]);
GO

CREATE INDEX [IX_IngredientMeasureQuantity_QuantityId] ON [IngredientMeasureQuantity] ([QuantityId]);
GO

CREATE INDEX [IX_Ingredients_GroupId] ON [Ingredients] ([GroupId]);
GO

CREATE INDEX [IX_IngredientsAndNutrients_GroupId] ON [IngredientsAndNutrients] ([GroupId]);
GO

CREATE INDEX [IX_JoinIngredientPreperationStep_IngredientId] ON [JoinIngredientPreperationStep] ([IngredientId]);
GO

CREATE INDEX [IX_JoinIngredientPreperationStep_PreperationId] ON [JoinIngredientPreperationStep] ([PreperationId]);
GO

CREATE INDEX [IX_Likes_RecipeId] ON [Likes] ([RecipeId]);
GO

CREATE INDEX [IX_MealPlan_MyMealModelId] ON [MealPlan] ([MyMealModelId]);
GO

CREATE INDEX [IX_MealPlanHandler_MealPlanId] ON [MealPlanHandler] ([MealPlanId]);
GO

CREATE INDEX [IX_MealPlanHandler_RecipesId] ON [MealPlanHandler] ([RecipesId]);
GO

CREATE INDEX [IX_NutrienHandler_IngredientId] ON [NutrienHandler] ([IngredientId]);
GO

CREATE INDEX [IX_NutrienHandler_MetricsId] ON [NutrienHandler] ([MetricsId]);
GO

CREATE INDEX [IX_NutrienHandler_NutrientsId] ON [NutrienHandler] ([NutrientsId]);
GO

CREATE INDEX [IX_NutrienHandler_QuantityId] ON [NutrienHandler] ([QuantityId]);
GO

CREATE INDEX [IX_QueryHandler_QueryId] ON [QueryHandler] ([QueryId]);
GO

CREATE INDEX [IX_QueryHandler_RecipeId] ON [QueryHandler] ([RecipeId]);
GO

CREATE INDEX [IX_Recessions_RecipesId] ON [Recessions] ([RecipesId]);
GO

CREATE INDEX [IX_RecipeBaseDataImage_RecipeId] ON [RecipeBaseDataImage] ([RecipeId]);
GO

CREATE INDEX [IX_RecipeBaseKeywords_KeywordId] ON [RecipeBaseKeywords] ([KeywordId]);
GO

CREATE INDEX [IX_RecipeJoinIngredientMeasureQuantity_IngredientId] ON [RecipeJoinIngredientMeasureQuantity] ([IngredientId]);
GO

CREATE INDEX [IX_RecipeJoinIngredientMeasureQuantity_RecipeId] ON [RecipeJoinIngredientMeasureQuantity] ([RecipeId]);
GO

CREATE INDEX [IX_RecipeJoinPreperationSteps_RecipeId] ON [RecipeJoinPreperationSteps] ([RecipeId]);
GO

CREATE INDEX [IX_RecipeJoinPreperationSteps_RecipePreperationStepId] ON [RecipeJoinPreperationSteps] ([RecipePreperationStepId]);
GO

CREATE INDEX [IX_RecipesHandlers_IngredientHandlerId] ON [RecipesHandlers] ([IngredientHandlerId]);
GO

CREATE INDEX [IX_RecipesHandlers_RecipeId] ON [RecipesHandlers] ([RecipeId]);
GO

CREATE INDEX [IX_UserPreferencesRecipes_RecipesId] ON [UserPreferencesRecipes] ([RecipesId]);
GO

CREATE INDEX [IX_WorldUserAbo_CreatorId] ON [WorldUserAbo] ([CreatorId]);
GO

CREATE INDEX [IX_WorldUserAbo_WorldUserId] ON [WorldUserAbo] ([WorldUserId]);
GO

CREATE INDEX [IX_WorldUserLike_RecipeId] ON [WorldUserLike] ([RecipeId]);
GO

CREATE INDEX [IX_WorldUserLike_WorldAppUserId] ON [WorldUserLike] ([WorldAppUserId]);
GO

CREATE INDEX [IX_WorldUserPosting_RecipeId] ON [WorldUserPosting] ([RecipeId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260213203931_AddPhaseToPreperationSteps', N'8.0.23');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [IngredientsAndNutrients] ADD [Icon] nvarchar(max) NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260316104500_AddIconToIngredientsAndNutrients', N'8.0.23');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [SmartRecipeSteps] (
    [Id] int NOT NULL IDENTITY,
    [MasterStepKey] nvarchar(128) NOT NULL,
    [VariablesJson] nvarchar(max) NOT NULL,
    [Phase] int NULL,
    [Equipment] nvarchar(256) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_SmartRecipeSteps] PRIMARY KEY ([Id])
);
GO

CREATE TABLE [RecipeJoinSmartSteps] (
    [Id] int NOT NULL IDENTITY,
    [RecipeId] int NOT NULL,
    [SmartRecipeStepId] int NOT NULL,
    [StepIndex] int NOT NULL,
    CONSTRAINT [PK_RecipeJoinSmartSteps] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecipeJoinSmartSteps_RecipeBaseData_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [RecipeBaseData] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RecipeJoinSmartSteps_SmartRecipeSteps_SmartRecipeStepId] FOREIGN KEY ([SmartRecipeStepId]) REFERENCES [SmartRecipeSteps] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_RecipeJoinSmartSteps_RecipeId_StepIndex] ON [RecipeJoinSmartSteps] ([RecipeId], [StepIndex]);
GO

CREATE INDEX [IX_RecipeJoinSmartSteps_SmartRecipeStepId] ON [RecipeJoinSmartSteps] ([SmartRecipeStepId]);
GO

CREATE INDEX [IX_SmartRecipeSteps_MasterStepKey_Phase_Equipment] ON [SmartRecipeSteps] ([MasterStepKey], [Phase], [Equipment]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260321232233_AddSmartRecipeSteps', N'8.0.23');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [WorldUserPosting] ADD [IsOffline] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260410120000_AddIsOfflineToWorldUserPosting', N'8.0.23');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO


                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_peelable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_peelable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_cuttable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_cuttable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_grateable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_grateable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_fryable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_fryable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_roastable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_roastable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_grillable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_grillable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_steamable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_steamable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_boilable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_boilable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_searable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_searable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_poachable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_poachable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_smokable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_smokable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_flambeable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_flambeable] bit NOT NULL DEFAULT CAST(0 AS bit);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IngredientsAndNutrients') AND name = 'is_blendable')
                    ALTER TABLE [IngredientsAndNutrients] ADD [is_blendable] bit NOT NULL DEFAULT CAST(0 AS bit);
            
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260411150052_AddIngredientTags', N'8.0.23');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [IngredientsAndNutrients] ADD [is_powder] bit NOT NULL DEFAULT CAST(0 AS bit);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260413105351_AddIsPowderToIngredients', N'8.0.23');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [WorldUserBookmark] (
    [Id] int NOT NULL IDENTITY,
    [RecipeId] int NOT NULL,
    [WorldAppUserId] int NOT NULL,
    CONSTRAINT [PK_WorldUserBookmark] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorldUserBookmark_RecipeBaseData_RecipeId] FOREIGN KEY ([RecipeId]) REFERENCES [RecipeBaseData] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WorldUserBookmark_WorldAppUser_WorldAppUserId] FOREIGN KEY ([WorldAppUserId]) REFERENCES [WorldAppUser] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_WorldUserBookmark_RecipeId] ON [WorldUserBookmark] ([RecipeId]);
GO

CREATE INDEX [IX_WorldUserBookmark_WorldAppUserId] ON [WorldUserBookmark] ([WorldAppUserId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260417071725_AddWorldUserBookmark', N'8.0.23');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [RecipeAiVariants] (
    [Id] int NOT NULL IDENTITY,
    [BaseRecipeId] int NOT NULL,
    [ParentVariantId] int NULL,
    [VariantType] nvarchar(64) NOT NULL,
    [Language] nvarchar(12) NOT NULL,
    [CreatedByUserHash] nvarchar(256) NULL,
    [AppliedChangeCount] int NOT NULL,
    [LatestUserNote] nvarchar(1000) NULL,
    [IsSharedCanonical] bit NOT NULL,
    [StepPlanJson] nvarchar(max) NOT NULL,
    [RenderedStepsJson] nvarchar(max) NOT NULL,
    [RenderedIngredientsJson] nvarchar(max) NOT NULL,
    [RenderedHighlightsJson] nvarchar(max) NOT NULL,
    [RenderedTitle] nvarchar(256) NOT NULL,
    [RenderedSummary] nvarchar(4000) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_RecipeAiVariants] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecipeAiVariants_RecipeAiVariants_ParentVariantId] FOREIGN KEY ([ParentVariantId]) REFERENCES [RecipeAiVariants] ([Id]),
    CONSTRAINT [FK_RecipeAiVariants_RecipeBaseData_BaseRecipeId] FOREIGN KEY ([BaseRecipeId]) REFERENCES [RecipeBaseData] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_RecipeAiVariants_BaseRecipeId_CreatedByUserHash_VariantType_Language] ON [RecipeAiVariants] ([BaseRecipeId], [CreatedByUserHash], [VariantType], [Language]);
GO

CREATE INDEX [IX_RecipeAiVariants_BaseRecipeId_VariantType_Language_IsSharedCanonical] ON [RecipeAiVariants] ([BaseRecipeId], [VariantType], [Language], [IsSharedCanonical]);
GO

CREATE INDEX [IX_RecipeAiVariants_ParentVariantId] ON [RecipeAiVariants] ([ParentVariantId]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260418070001_AddRecipeAiVariants', N'8.0.23');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

CREATE TABLE [WorldSharedShoppingList] (
    [Id] int NOT NULL IDENTITY,
    [ShareToken] nvarchar(450) NOT NULL,
    [UserHash] nvarchar(max) NULL,
    [ItemsJson] nvarchar(max) NOT NULL,
    [CheckedJson] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_WorldSharedShoppingList] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_WorldSharedShoppingList_ShareToken] ON [WorldSharedShoppingList] ([ShareToken]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260418081259_AddWorldSharedShoppingList', N'8.0.23');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [IngredientsAndNutrients] ADD [FoodCategoryId] int NULL;
GO

CREATE TABLE [food_categories] (
    [Id] int NOT NULL IDENTITY,
    [Category_Key] nvarchar(100) NOT NULL,
    [Name_DE] nvarchar(255) NULL,
    [Name_EN] nvarchar(255) NULL,
    [Name_PRT] nvarchar(255) NULL,
    [Name_ESP] nvarchar(255) NULL,
    [Name_ID] nvarchar(255) NULL,
    [Name_NL] nvarchar(255) NULL,
    [Name_SE] nvarchar(255) NULL,
    [Name_DK] nvarchar(255) NULL,
    [Name_NO] nvarchar(255) NULL,
    [Name_MS] nvarchar(255) NULL,
    CONSTRAINT [PK_food_categories] PRIMARY KEY ([Id])
);
GO

CREATE INDEX [IX_IngredientsAndNutrients_FoodCategoryId] ON [IngredientsAndNutrients] ([FoodCategoryId]);
GO

CREATE UNIQUE INDEX [IX_food_categories_Category_Key] ON [food_categories] ([Category_Key]);
GO

ALTER TABLE [IngredientsAndNutrients] ADD CONSTRAINT [FK_Ingredients_FoodCategories] FOREIGN KEY ([FoodCategoryId]) REFERENCES [food_categories] ([Id]) ON DELETE SET NULL;
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260419135642_AddRecipeAiVariantSelections', N'8.0.23');
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

ALTER TABLE [RecipeAiVariants] ADD [AiProvider] nvarchar(32) NOT NULL DEFAULT N'openai';
GO

ALTER TABLE [RecipeAiVariants] ADD [IngredientsText] nvarchar(max) NOT NULL DEFAULT N'';
GO

ALTER TABLE [RecipeAiVariants] ADD [PreparationText] nvarchar(max) NOT NULL DEFAULT N'';
GO

ALTER TABLE [RecipeAiVariants] ADD [SelectedIngredientKey] nvarchar(400) NOT NULL DEFAULT N'';
GO

CREATE TABLE [RecipeAiVariantIngredientRows] (
    [Id] int NOT NULL IDENTITY,
    [RecipeAiVariantId] int NOT NULL,
    [IngredientMeasureQuantityId] int NULL,
    [IngredientId] int NULL,
    [MeasureId] int NULL,
    [DisplayName] nvarchar(256) NOT NULL,
    [QuantityText] nvarchar(128) NOT NULL,
    [IsAiGenerated] bit NOT NULL,
    [SortOrder] int NOT NULL,
    CONSTRAINT [PK_RecipeAiVariantIngredientRows] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecipeAiVariantIngredientRows_IngredientMeasureQuantity_IngredientMeasureQuantityId] FOREIGN KEY ([IngredientMeasureQuantityId]) REFERENCES [IngredientMeasureQuantity] ([Id]),
    CONSTRAINT [FK_RecipeAiVariantIngredientRows_IngredientsAndNutrients_IngredientId] FOREIGN KEY ([IngredientId]) REFERENCES [IngredientsAndNutrients] ([Id]),
    CONSTRAINT [FK_RecipeAiVariantIngredientRows_Metrics_MeasureId] FOREIGN KEY ([MeasureId]) REFERENCES [Metrics] ([Id]),
    CONSTRAINT [FK_RecipeAiVariantIngredientRows_RecipeAiVariants_RecipeAiVariantId] FOREIGN KEY ([RecipeAiVariantId]) REFERENCES [RecipeAiVariants] ([Id]) ON DELETE CASCADE
);
GO

CREATE TABLE [RecipeAiVariantSelectedIngredients] (
    [Id] int NOT NULL IDENTITY,
    [RecipeAiVariantId] int NOT NULL,
    [IngredientId] int NOT NULL,
    [SortOrder] int NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_RecipeAiVariantSelectedIngredients] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RecipeAiVariantSelectedIngredients_IngredientsAndNutrients_IngredientId] FOREIGN KEY ([IngredientId]) REFERENCES [IngredientsAndNutrients] ([Id]),
    CONSTRAINT [FK_RecipeAiVariantSelectedIngredients_RecipeAiVariants_RecipeAiVariantId] FOREIGN KEY ([RecipeAiVariantId]) REFERENCES [RecipeAiVariants] ([Id]) ON DELETE CASCADE
);
GO

CREATE INDEX [IX_RecipeAiVariants_BaseRecipeId_CreatedByUserHash_VariantType_Language_AiProvider_SelectedIngredientKey] ON [RecipeAiVariants] ([BaseRecipeId], [CreatedByUserHash], [VariantType], [Language], [AiProvider], [SelectedIngredientKey]);
GO

CREATE INDEX [IX_RecipeAiVariants_BaseRecipeId_VariantType_Language_AiProvider_SelectedIngredientKey_IsSharedCanonical] ON [RecipeAiVariants] ([BaseRecipeId], [VariantType], [Language], [AiProvider], [SelectedIngredientKey], [IsSharedCanonical]);
GO

CREATE INDEX [IX_RecipeAiVariantIngredientRows_IngredientId] ON [RecipeAiVariantIngredientRows] ([IngredientId]);
GO

CREATE INDEX [IX_RecipeAiVariantIngredientRows_IngredientMeasureQuantityId] ON [RecipeAiVariantIngredientRows] ([IngredientMeasureQuantityId]);
GO

CREATE INDEX [IX_RecipeAiVariantIngredientRows_MeasureId] ON [RecipeAiVariantIngredientRows] ([MeasureId]);
GO

CREATE INDEX [IX_RecipeAiVariantIngredientRows_RecipeAiVariantId_SortOrder] ON [RecipeAiVariantIngredientRows] ([RecipeAiVariantId], [SortOrder]);
GO

CREATE INDEX [IX_RecipeAiVariantSelectedIngredients_IngredientId] ON [RecipeAiVariantSelectedIngredients] ([IngredientId]);
GO

CREATE UNIQUE INDEX [IX_RecipeAiVariantSelectedIngredients_RecipeAiVariantId_IngredientId] ON [RecipeAiVariantSelectedIngredients] ([RecipeAiVariantId], [IngredientId]);
GO

CREATE INDEX [IX_RecipeAiVariantSelectedIngredients_RecipeAiVariantId_SortOrder] ON [RecipeAiVariantSelectedIngredients] ([RecipeAiVariantId], [SortOrder]);
GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260420212742_AddIngredientMeasureQuantityLinkToAiVariantRows', N'8.0.23');
GO

COMMIT;
GO


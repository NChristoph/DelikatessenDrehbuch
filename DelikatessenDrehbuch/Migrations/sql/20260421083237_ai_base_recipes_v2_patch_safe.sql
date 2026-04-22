/*
  Safe patch script for production/Azure.
  Introduces new canonical AI recipe storage:
    - RecipeAiBaseRecipes
    - RecipeAiBaseRecipeIngredients (bridge to IngredientMeasureQuantity)
    - RecipeAiBaseRecipeSteps
    - RecipeAiBaseRecipeSelectedIngredients
*/

SET NOCOUNT ON;

/* === RecipeAiBaseRecipes === */
IF OBJECT_ID('dbo.RecipeAiBaseRecipes', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeAiBaseRecipes (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecipeAiBaseRecipes PRIMARY KEY,
        BaseRecipeId int NOT NULL,
        ParentAiRecipeId int NULL,
        VariantType nvarchar(64) NOT NULL,
        Language nvarchar(12) NOT NULL,
        AiProvider nvarchar(32) NOT NULL,
        SelectedIngredientKey nvarchar(400) NOT NULL,
        CreatedByUserHash nvarchar(256) NULL,
        AppliedChangeCount int NOT NULL,
        LatestUserNote nvarchar(1000) NULL,
        IsSharedCanonical bit NOT NULL,
        Title nvarchar(256) NOT NULL,
        Summary nvarchar(4000) NOT NULL,
        PersonCount int NOT NULL,
        PreparationTimeMinutes int NOT NULL,
        PreparationText nvarchar(max) NOT NULL,
        CreatedAtUtc datetime2 NOT NULL,
        UpdatedAtUtc datetime2 NOT NULL
    );

    ALTER TABLE dbo.RecipeAiBaseRecipes
        ADD CONSTRAINT FK_RecipeAiBaseRecipes_RecipeBaseData_BaseRecipeId
        FOREIGN KEY (BaseRecipeId) REFERENCES dbo.RecipeBaseData(Id) ON DELETE CASCADE;

    ALTER TABLE dbo.RecipeAiBaseRecipes
        ADD CONSTRAINT FK_RecipeAiBaseRecipes_RecipeAiBaseRecipes_ParentAiRecipeId
        FOREIGN KEY (ParentAiRecipeId) REFERENCES dbo.RecipeAiBaseRecipes(Id);
END

/* Indexes */
IF OBJECT_ID('dbo.RecipeAiBaseRecipes', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeAiBaseRecipes_ScopeShared' AND object_id = OBJECT_ID('dbo.RecipeAiBaseRecipes'))
        CREATE INDEX IX_RecipeAiBaseRecipes_ScopeShared
        ON dbo.RecipeAiBaseRecipes(BaseRecipeId, Language, VariantType, AiProvider, SelectedIngredientKey, IsSharedCanonical);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeAiBaseRecipes_ScopeUser' AND object_id = OBJECT_ID('dbo.RecipeAiBaseRecipes'))
        CREATE INDEX IX_RecipeAiBaseRecipes_ScopeUser
        ON dbo.RecipeAiBaseRecipes(BaseRecipeId, Language, VariantType, AiProvider, SelectedIngredientKey, CreatedByUserHash);
END

/* === Ingredients Bridge === */
IF OBJECT_ID('dbo.RecipeAiBaseRecipeIngredients', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeAiBaseRecipeIngredients (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecipeAiBaseRecipeIngredients PRIMARY KEY,
        RecipeAiBaseRecipeId int NOT NULL,
        IngredientMeasureQuantityId int NOT NULL,
        SortOrder int NOT NULL,
        IsModified bit NOT NULL,
        ChangeHint nvarchar(256) NULL
    );

    ALTER TABLE dbo.RecipeAiBaseRecipeIngredients
        ADD CONSTRAINT FK_RecipeAiBaseRecipeIngredients_RecipeAiBaseRecipes
        FOREIGN KEY (RecipeAiBaseRecipeId) REFERENCES dbo.RecipeAiBaseRecipes(Id) ON DELETE CASCADE;

    ALTER TABLE dbo.RecipeAiBaseRecipeIngredients
        ADD CONSTRAINT FK_RecipeAiBaseRecipeIngredients_IngredientMeasureQuantity
        FOREIGN KEY (IngredientMeasureQuantityId) REFERENCES dbo.IngredientMeasureQuantity(Id);
END

IF OBJECT_ID('dbo.RecipeAiBaseRecipeIngredients', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeAiBaseRecipeIngredients_Order' AND object_id = OBJECT_ID('dbo.RecipeAiBaseRecipeIngredients'))
        CREATE INDEX IX_RecipeAiBaseRecipeIngredients_Order
        ON dbo.RecipeAiBaseRecipeIngredients(RecipeAiBaseRecipeId, SortOrder);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeAiBaseRecipeIngredients_Imq' AND object_id = OBJECT_ID('dbo.RecipeAiBaseRecipeIngredients'))
        CREATE INDEX IX_RecipeAiBaseRecipeIngredients_Imq
        ON dbo.RecipeAiBaseRecipeIngredients(IngredientMeasureQuantityId);
END

/* === Steps === */
IF OBJECT_ID('dbo.RecipeAiBaseRecipeSteps', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeAiBaseRecipeSteps (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecipeAiBaseRecipeSteps PRIMARY KEY,
        RecipeAiBaseRecipeId int NOT NULL,
        StepIndex int NOT NULL,
        Text nvarchar(4000) NOT NULL
    );

    ALTER TABLE dbo.RecipeAiBaseRecipeSteps
        ADD CONSTRAINT FK_RecipeAiBaseRecipeSteps_RecipeAiBaseRecipes
        FOREIGN KEY (RecipeAiBaseRecipeId) REFERENCES dbo.RecipeAiBaseRecipes(Id) ON DELETE CASCADE;
END

IF OBJECT_ID('dbo.RecipeAiBaseRecipeSteps', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeAiBaseRecipeSteps_Order' AND object_id = OBJECT_ID('dbo.RecipeAiBaseRecipeSteps'))
        CREATE INDEX IX_RecipeAiBaseRecipeSteps_Order
        ON dbo.RecipeAiBaseRecipeSteps(RecipeAiBaseRecipeId, StepIndex);
END

/* === Selected Ingredients (what user picked from DB candidates) === */
IF OBJECT_ID('dbo.RecipeAiBaseRecipeSelectedIngredients', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RecipeAiBaseRecipeSelectedIngredients (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RecipeAiBaseRecipeSelectedIngredients PRIMARY KEY,
        RecipeAiBaseRecipeId int NOT NULL,
        IngredientId int NOT NULL,
        SortOrder int NOT NULL,
        CreatedAtUtc datetime2 NOT NULL
    );

    ALTER TABLE dbo.RecipeAiBaseRecipeSelectedIngredients
        ADD CONSTRAINT FK_RecipeAiBaseRecipeSelectedIngredients_RecipeAiBaseRecipes
        FOREIGN KEY (RecipeAiBaseRecipeId) REFERENCES dbo.RecipeAiBaseRecipes(Id) ON DELETE CASCADE;

    ALTER TABLE dbo.RecipeAiBaseRecipeSelectedIngredients
        ADD CONSTRAINT FK_RecipeAiBaseRecipeSelectedIngredients_IngredientsAndNutrients
        FOREIGN KEY (IngredientId) REFERENCES dbo.IngredientsAndNutrients(Id);
END

IF OBJECT_ID('dbo.RecipeAiBaseRecipeSelectedIngredients', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeAiBaseRecipeSelectedIngredients_Order' AND object_id = OBJECT_ID('dbo.RecipeAiBaseRecipeSelectedIngredients'))
        CREATE INDEX IX_RecipeAiBaseRecipeSelectedIngredients_Order
        ON dbo.RecipeAiBaseRecipeSelectedIngredients(RecipeAiBaseRecipeId, SortOrder);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_RecipeAiBaseRecipeSelectedIngredients_Unique' AND object_id = OBJECT_ID('dbo.RecipeAiBaseRecipeSelectedIngredients'))
        CREATE UNIQUE INDEX UX_RecipeAiBaseRecipeSelectedIngredients_Unique
        ON dbo.RecipeAiBaseRecipeSelectedIngredients(RecipeAiBaseRecipeId, IngredientId);
END


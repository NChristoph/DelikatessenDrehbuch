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


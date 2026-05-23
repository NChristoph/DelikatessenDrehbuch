-- Migration: Add WorldSharedFeedTodos table for group todo list functionality
-- Created: 2026-05-22

USE [DelikatessenDrehbuch]
GO

-- Create WorldSharedFeedTodos table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WorldSharedFeedTodos]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[WorldSharedFeedTodos](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [WorldSharedFeedId] [int] NOT NULL,
        [Title] [nvarchar](200) NOT NULL,
        [IsCompleted] [bit] NOT NULL DEFAULT 0,
        [CreatedByUserHash] [nvarchar](450) NOT NULL,
        [CompletedByUserHash] [nvarchar](450) NULL,
        [CreatedAtUtc] [datetime2](7) NOT NULL,
        [CompletedAtUtc] [datetime2](7) NULL,
        [SortOrder] [int] NOT NULL DEFAULT 0,
        CONSTRAINT [PK_WorldSharedFeedTodos] PRIMARY KEY CLUSTERED ([Id] ASC)
            WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF,
                  ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY]

    PRINT 'Table WorldSharedFeedTodos created successfully.'
END
ELSE
BEGIN
    PRINT 'Table WorldSharedFeedTodos already exists.'
END
GO

-- Add Foreign Key constraint to WorldSharedFeeds
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_WorldSharedFeedTodos_WorldSharedFeeds_WorldSharedFeedId]') AND parent_object_id = OBJECT_ID(N'[dbo].[WorldSharedFeedTodos]'))
BEGIN
    ALTER TABLE [dbo].[WorldSharedFeedTodos]  WITH CHECK
    ADD CONSTRAINT [FK_WorldSharedFeedTodos_WorldSharedFeeds_WorldSharedFeedId]
    FOREIGN KEY([WorldSharedFeedId])
    REFERENCES [dbo].[WorldSharedFeeds] ([Id])
    ON DELETE CASCADE

    PRINT 'Foreign Key constraint added successfully.'
END
ELSE
BEGIN
    PRINT 'Foreign Key constraint already exists.'
END
GO

-- Create index on WorldSharedFeedId for better query performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[WorldSharedFeedTodos]') AND name = N'IX_WorldSharedFeedTodos_WorldSharedFeedId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_WorldSharedFeedTodos_WorldSharedFeedId]
    ON [dbo].[WorldSharedFeedTodos] ([WorldSharedFeedId] ASC)
    INCLUDE ([IsCompleted], [SortOrder])
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF,
          DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)

    PRINT 'Index IX_WorldSharedFeedTodos_WorldSharedFeedId created successfully.'
END
ELSE
BEGIN
    PRINT 'Index IX_WorldSharedFeedTodos_WorldSharedFeedId already exists.'
END
GO

-- Create index on SortOrder for ordering
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[WorldSharedFeedTodos]') AND name = N'IX_WorldSharedFeedTodos_SortOrder')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_WorldSharedFeedTodos_SortOrder]
    ON [dbo].[WorldSharedFeedTodos] ([WorldSharedFeedId] ASC, [SortOrder] ASC, [CreatedAtUtc] ASC)

    PRINT 'Index IX_WorldSharedFeedTodos_SortOrder created successfully.'
END
ELSE
BEGIN
    PRINT 'Index IX_WorldSharedFeedTodos_SortOrder already exists.'
END
GO

-- Add default constraint for CreatedAtUtc
IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE object_id = OBJECT_ID(N'[dbo].[DF_WorldSharedFeedTodos_CreatedAtUtc]') AND parent_object_id = OBJECT_ID(N'[dbo].[WorldSharedFeedTodos]'))
BEGIN
    ALTER TABLE [dbo].[WorldSharedFeedTodos]
    ADD CONSTRAINT [DF_WorldSharedFeedTodos_CreatedAtUtc]
    DEFAULT (GETUTCDATE()) FOR [CreatedAtUtc]

    PRINT 'Default constraint for CreatedAtUtc added successfully.'
END
ELSE
BEGIN
    PRINT 'Default constraint for CreatedAtUtc already exists.'
END
GO

PRINT '========================================='
PRINT 'Migration completed successfully!'
PRINT '========================================='
GO

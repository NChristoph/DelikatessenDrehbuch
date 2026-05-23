-- Migration: Add WorldSharedFeedMessages and WorldSharedFeedTodos tables
-- Created: 2026-05-22
-- Description: Adds group chat and todo list functionality to shared feeds

USE [DelikatessenDrehbuch]
GO

PRINT '========================================='
PRINT 'Starting migration...'
PRINT '========================================='

-- =============================================
-- 1. Create WorldSharedFeedMessages table
-- =============================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WorldSharedFeedMessages]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[WorldSharedFeedMessages](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [WorldSharedFeedId] [int] NOT NULL,
        [UserHash] [nvarchar](450) NOT NULL,
        [Message] [nvarchar](1000) NOT NULL,
        [CreatedAtUtc] [datetime2](7) NOT NULL,
        CONSTRAINT [PK_WorldSharedFeedMessages] PRIMARY KEY CLUSTERED ([Id] ASC)
            WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF,
                  ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY]

    PRINT '✓ Table WorldSharedFeedMessages created'
END
ELSE
BEGIN
    PRINT '- Table WorldSharedFeedMessages already exists'
END
GO

-- Foreign Key for Messages
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_WorldSharedFeedMessages_WorldSharedFeeds_WorldSharedFeedId]'))
BEGIN
    ALTER TABLE [dbo].[WorldSharedFeedMessages]  WITH CHECK
    ADD CONSTRAINT [FK_WorldSharedFeedMessages_WorldSharedFeeds_WorldSharedFeedId]
    FOREIGN KEY([WorldSharedFeedId])
    REFERENCES [dbo].[WorldSharedFeeds] ([Id])
    ON DELETE CASCADE

    PRINT '✓ Foreign Key for Messages added'
END
GO

-- Indexes for Messages
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[WorldSharedFeedMessages]') AND name = N'IX_WorldSharedFeedMessages_WorldSharedFeedId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_WorldSharedFeedMessages_WorldSharedFeedId]
    ON [dbo].[WorldSharedFeedMessages] ([WorldSharedFeedId] ASC)
    INCLUDE ([CreatedAtUtc])

    PRINT '✓ Index on WorldSharedFeedId created'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[WorldSharedFeedMessages]') AND name = N'IX_WorldSharedFeedMessages_CreatedAtUtc')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_WorldSharedFeedMessages_CreatedAtUtc]
    ON [dbo].[WorldSharedFeedMessages] ([CreatedAtUtc] DESC)

    PRINT '✓ Index on CreatedAtUtc created'
END
GO

-- Default constraint for Messages
IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE object_id = OBJECT_ID(N'[dbo].[DF_WorldSharedFeedMessages_CreatedAtUtc]'))
BEGIN
    ALTER TABLE [dbo].[WorldSharedFeedMessages]
    ADD CONSTRAINT [DF_WorldSharedFeedMessages_CreatedAtUtc]
    DEFAULT (GETUTCDATE()) FOR [CreatedAtUtc]

    PRINT '✓ Default constraint for Messages.CreatedAtUtc added'
END
GO

PRINT ''
PRINT 'WorldSharedFeedMessages setup complete!'
PRINT ''

-- =============================================
-- 2. Create WorldSharedFeedTodos table
-- =============================================

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

    PRINT '✓ Table WorldSharedFeedTodos created'
END
ELSE
BEGIN
    PRINT '- Table WorldSharedFeedTodos already exists'
END
GO

-- Foreign Key for Todos
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_WorldSharedFeedTodos_WorldSharedFeeds_WorldSharedFeedId]'))
BEGIN
    ALTER TABLE [dbo].[WorldSharedFeedTodos]  WITH CHECK
    ADD CONSTRAINT [FK_WorldSharedFeedTodos_WorldSharedFeeds_WorldSharedFeedId]
    FOREIGN KEY([WorldSharedFeedId])
    REFERENCES [dbo].[WorldSharedFeeds] ([Id])
    ON DELETE CASCADE

    PRINT '✓ Foreign Key for Todos added'
END
GO

-- Indexes for Todos
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[WorldSharedFeedTodos]') AND name = N'IX_WorldSharedFeedTodos_WorldSharedFeedId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_WorldSharedFeedTodos_WorldSharedFeedId]
    ON [dbo].[WorldSharedFeedTodos] ([WorldSharedFeedId] ASC)
    INCLUDE ([IsCompleted], [SortOrder])

    PRINT '✓ Index on WorldSharedFeedId created'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[WorldSharedFeedTodos]') AND name = N'IX_WorldSharedFeedTodos_SortOrder')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_WorldSharedFeedTodos_SortOrder]
    ON [dbo].[WorldSharedFeedTodos] ([WorldSharedFeedId] ASC, [SortOrder] ASC, [CreatedAtUtc] ASC)

    PRINT '✓ Index on SortOrder created'
END
GO

-- Default constraint for Todos
IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE object_id = OBJECT_ID(N'[dbo].[DF_WorldSharedFeedTodos_CreatedAtUtc]'))
BEGIN
    ALTER TABLE [dbo].[WorldSharedFeedTodos]
    ADD CONSTRAINT [DF_WorldSharedFeedTodos_CreatedAtUtc]
    DEFAULT (GETUTCDATE()) FOR [CreatedAtUtc]

    PRINT '✓ Default constraint for Todos.CreatedAtUtc added'
END
GO

PRINT ''
PRINT 'WorldSharedFeedTodos setup complete!'
PRINT ''

PRINT '========================================='
PRINT '✅ All migrations completed successfully!'
PRINT '========================================='
PRINT ''
PRINT 'Summary:'
PRINT '- WorldSharedFeedMessages table (Group Chat)'
PRINT '- WorldSharedFeedTodos table (Todo List)'
PRINT ''
PRINT 'You can now use:'
PRINT '- Live group chat in shared feeds'
PRINT '- Shared todo lists with completion tracking'
PRINT ''
GO

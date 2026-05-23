-- Migration: Add WorldSharedFeedMessages table for group chat functionality
-- Created: 2026-05-22

USE [DelikatessenDrehbuch]
GO

-- Create WorldSharedFeedMessages table
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

    PRINT 'Table WorldSharedFeedMessages created successfully.'
END
ELSE
BEGIN
    PRINT 'Table WorldSharedFeedMessages already exists.'
END
GO

-- Add Foreign Key constraint to WorldSharedFeeds
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(N'[dbo].[FK_WorldSharedFeedMessages_WorldSharedFeeds_WorldSharedFeedId]') AND parent_object_id = OBJECT_ID(N'[dbo].[WorldSharedFeedMessages]'))
BEGIN
    ALTER TABLE [dbo].[WorldSharedFeedMessages]  WITH CHECK
    ADD CONSTRAINT [FK_WorldSharedFeedMessages_WorldSharedFeeds_WorldSharedFeedId]
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
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[WorldSharedFeedMessages]') AND name = N'IX_WorldSharedFeedMessages_WorldSharedFeedId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_WorldSharedFeedMessages_WorldSharedFeedId]
    ON [dbo].[WorldSharedFeedMessages] ([WorldSharedFeedId] ASC)
    INCLUDE ([CreatedAtUtc])
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF,
          DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)

    PRINT 'Index IX_WorldSharedFeedMessages_WorldSharedFeedId created successfully.'
END
ELSE
BEGIN
    PRINT 'Index IX_WorldSharedFeedMessages_WorldSharedFeedId already exists.'
END
GO

-- Create index on CreatedAtUtc for time-based queries
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[WorldSharedFeedMessages]') AND name = N'IX_WorldSharedFeedMessages_CreatedAtUtc')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_WorldSharedFeedMessages_CreatedAtUtc]
    ON [dbo].[WorldSharedFeedMessages] ([CreatedAtUtc] DESC)

    PRINT 'Index IX_WorldSharedFeedMessages_CreatedAtUtc created successfully.'
END
ELSE
BEGIN
    PRINT 'Index IX_WorldSharedFeedMessages_CreatedAtUtc already exists.'
END
GO

-- Add default constraint for CreatedAtUtc
IF NOT EXISTS (SELECT * FROM sys.default_constraints WHERE object_id = OBJECT_ID(N'[dbo].[DF_WorldSharedFeedMessages_CreatedAtUtc]') AND parent_object_id = OBJECT_ID(N'[dbo].[WorldSharedFeedMessages]'))
BEGIN
    ALTER TABLE [dbo].[WorldSharedFeedMessages]
    ADD CONSTRAINT [DF_WorldSharedFeedMessages_CreatedAtUtc]
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

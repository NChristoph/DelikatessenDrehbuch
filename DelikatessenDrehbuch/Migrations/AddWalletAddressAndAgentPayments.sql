-- Add WalletAddress column to WorldAppUser table
IF NOT EXISTS (
    SELECT * FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[WorldAppUser]')
    AND name = 'WalletAddress'
)
BEGIN
    ALTER TABLE [dbo].[WorldAppUser]
    ADD [WalletAddress] NVARCHAR(100) NULL;
END
GO

-- Create AgentPaymentRequests table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AgentPaymentRequests]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AgentPaymentRequests] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [RecipientUserHash] NVARCHAR(100) NOT NULL,
        [RecipientWalletAddress] NVARCHAR(100) NULL,
        [TokenSymbol] NVARCHAR(10) NOT NULL,
        [Amount] DECIMAL(18,6) NOT NULL,
        [Description] NVARCHAR(500) NOT NULL,
        [Reference] NVARCHAR(100) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL DEFAULT 'pending',
        [TransactionHash] NVARCHAR(100) NULL,
        [InitiatedBy] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [ConfirmedAt] DATETIME2 NULL,
        [ExpiresAt] DATETIME2 NULL,
        CONSTRAINT [PK_AgentPaymentRequests] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    -- Create index on RecipientUserHash for faster queries
    CREATE NONCLUSTERED INDEX [IX_AgentPaymentRequests_RecipientUserHash]
    ON [dbo].[AgentPaymentRequests] ([RecipientUserHash]);

    -- Create index on Reference for faster lookups
    CREATE NONCLUSTERED INDEX [IX_AgentPaymentRequests_Reference]
    ON [dbo].[AgentPaymentRequests] ([Reference]);

    -- Create index on Status for pending payment queries
    CREATE NONCLUSTERED INDEX [IX_AgentPaymentRequests_Status]
    ON [dbo].[AgentPaymentRequests] ([Status]);
END
GO

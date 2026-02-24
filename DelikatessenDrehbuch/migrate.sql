-- =============================================
-- Migration: AddPhaseToPreperationSteps
-- Adds Phase column and classifies existing steps
-- =============================================

-- 1) Add Phase column (default 0 = Sonstige/Unbekannt)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'RecipePreperationSteps' AND COLUMN_NAME = 'Phase'
)
BEGIN
    ALTER TABLE [RecipePreperationSteps] ADD [Phase] int NOT NULL DEFAULT 0;
END
GO

-- 2) Phase 1 = Vorbereitung
UPDATE RecipePreperationSteps SET Phase = 1
WHERE Phase = 0 AND (
    Step_DE LIKE '%schneid%' OR Step_DE LIKE N'%schäl%' OR Step_DE LIKE '%wasch%'
    OR Step_DE LIKE N'%würfel%' OR Step_DE LIKE '%reib%' OR Step_DE LIKE '%einweich%'
    OR Step_DE LIKE '%hack%' OR Step_DE LIKE '%zerklein%' OR Step_DE LIKE '%hobel%'
    OR Step_DE LIKE '%pell%' OR Step_DE LIKE '%entkern%' OR Step_DE LIKE '%filetier%'
    OR Step_DE LIKE '%vorbereite%' OR Step_DE LIKE '%halbier%' OR Step_DE LIKE '%vierteln%'
);
GO

-- 3) Phase 2 = Kochen
UPDATE RecipePreperationSteps SET Phase = 2
WHERE Phase = 0 AND (
    Step_DE LIKE '%anbrat%' OR Step_DE LIKE '%koch%' OR Step_DE LIKE N'%dünst%'
    OR Step_DE LIKE '%back%' OR Step_DE LIKE '%grill%' OR Step_DE LIKE '%frittier%'
    OR Step_DE LIKE '%blanchier%' OR Step_DE LIKE '%brat%' OR Step_DE LIKE '%schmo%'
    OR Step_DE LIKE '%gart%' OR Step_DE LIKE '%garen%' OR Step_DE LIKE '%simmern%'
    OR Step_DE LIKE '%aufkochen%' OR Step_DE LIKE N'%röst%' OR Step_DE LIKE '%dampf%'
    OR Step_DE LIKE '%reduzier%' OR Step_DE LIKE '%karamellis%'
);
GO

-- 4) Phase 3 = Würzen
UPDATE RecipePreperationSteps SET Phase = 3
WHERE Phase = 0 AND (
    Step_DE LIKE '%salz%' OR Step_DE LIKE '%pfeff%' OR Step_DE LIKE N'%würz%'
    OR Step_DE LIKE '%abschmeck%' OR Step_DE LIKE '%marinier%'
    OR Step_DE LIKE N'%beträufel%' OR Step_DE LIKE '%bestreu%'
);
GO

-- 5) Phase 4 = Finish
UPDATE RecipePreperationSteps SET Phase = 4
WHERE Phase = 0 AND (
    Step_DE LIKE '%anricht%' OR Step_DE LIKE '%garnier%' OR Step_DE LIKE '%servier%'
    OR Step_DE LIKE N'%auskühlen%' OR Step_DE LIKE '%dekorier%'
    OR Step_DE LIKE '%portionier%'
);
GO

-- 6) Verify
SELECT Phase, COUNT(*) AS Anzahl FROM RecipePreperationSteps GROUP BY Phase ORDER BY Phase;
GO

-- =============================================
-- Migration: AddEquipmentToPreperationSteps
-- Adds Equipment column and classifies steps by
-- kitchen equipment needed (for unbound-step filtering)
-- Equipment: 0=Keins, 1=Backofen, 2=Pfanne, 3=Topf, 4=Bräter, 5=Kochfeld
-- =============================================

-- 1) Add Equipment column (default 0 = Kein spezielles Gerät)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'RecipePreperationSteps' AND COLUMN_NAME = 'Equipment'
)
BEGIN
    ALTER TABLE [RecipePreperationSteps] ADD [Equipment] int NOT NULL DEFAULT 0;
END
GO

-- 2) Equipment 1 = Backofen
UPDATE RecipePreperationSteps SET Equipment = 1
WHERE Equipment = 0 AND (
    Step_DE LIKE '%backofen%' OR Step_DE LIKE '%ofen%'
    OR Step_DE LIKE N'%überback%' OR Step_DE LIKE '%gratini%'
    OR Step_DE LIKE '%vorheiz%' OR Step_DE LIKE '%auflaufform%'
    OR Step_DE LIKE '%backblech%' OR Step_DE LIKE '%backform%'
    OR Step_DE LIKE '%umluft%' OR Step_DE LIKE '%oberhitze%'
    OR Step_DE LIKE '%unterhitze%' OR Step_DE LIKE '%grillfunktion%'
);
GO

-- 3) Equipment 2 = Pfanne
UPDATE RecipePreperationSteps SET Equipment = 2
WHERE Equipment = 0 AND (
    Step_DE LIKE '%pfanne%' OR Step_DE LIKE '%anbrat%'
    OR Step_DE LIKE '%anbraten%' OR Step_DE LIKE '%scharf anbrat%'
    OR Step_DE LIKE N'%schwenk%' OR Step_DE LIKE '%wok%'
    OR Step_DE LIKE '%bratpfanne%'
);
GO

-- 4) Equipment 3 = Topf
UPDATE RecipePreperationSteps SET Equipment = 3
WHERE Equipment = 0 AND (
    Step_DE LIKE '%topf%' OR Step_DE LIKE '%aufkochen%'
    OR Step_DE LIKE '%simmern%' OR Step_DE LIKE '%kochwasser%'
    OR Step_DE LIKE '%abseihen%' OR Step_DE LIKE '%abgie%'
    OR Step_DE LIKE '%blanchier%' OR Step_DE LIKE '%siedend%'
);
GO

-- 5) Equipment 4 = Bräter
UPDATE RecipePreperationSteps SET Equipment = 4
WHERE Equipment = 0 AND (
    Step_DE LIKE N'%bräter%' OR Step_DE LIKE '%schmortopf%'
    OR Step_DE LIKE '%schmoren%' OR Step_DE LIKE '%schmore%'
    OR Step_DE LIKE N'%dutch%oven%'
);
GO

-- 6) Equipment 5 = Kochfeld
UPDATE RecipePreperationSteps SET Equipment = 5
WHERE Equipment = 0 AND (
    Step_DE LIKE '%kochfeld%' OR Step_DE LIKE '%herdplatte%'
    OR Step_DE LIKE '%herd%' OR Step_DE LIKE '%ceranfeld%'
    OR Step_DE LIKE '%induktion%' OR Step_DE LIKE '%kochstelle%'
);
GO

-- 7) Verify Equipment
SELECT Equipment, COUNT(*) AS Anzahl FROM RecipePreperationSteps GROUP BY Equipment ORDER BY Equipment;
GO

-- =====================================================
-- World Chain Purchase Fields - Azure SQL Migration Script
-- Run against your Azure SQL Database
-- =====================================================

-- 1) Add optional chain transaction hash to MealPlanPurchases
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases'
      AND COLUMN_NAME = 'ReferenceTxHash'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [ReferenceTxHash] NVARCHAR(130) NULL;
END
GO

-- 2) Add optional buyer wallet address to MealPlanPurchases
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases'
      AND COLUMN_NAME = 'BuyerWalletAddress'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [BuyerWalletAddress] NVARCHAR(64) NULL;
END
GO

-- 3) Speed up lookup and duplicate protection by listing/buyer/tx
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_MealPlanPurchases_ListingBuyerTx'
      AND object_id = OBJECT_ID('MealPlanPurchases')
)
BEGIN
    CREATE INDEX [IX_MealPlanPurchases_ListingBuyerTx]
        ON [MealPlanPurchases]([ListingId], [BuyerHash], [ReferenceTxHash]);
END
GO

-- 4) Optional uniqueness for non-null chain tx hashes
-- Prevents same transaction hash being inserted twice
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'UX_MealPlanPurchases_ReferenceTxHash_NotNull'
      AND object_id = OBJECT_ID('MealPlanPurchases')
)
BEGIN
    CREATE UNIQUE INDEX [UX_MealPlanPurchases_ReferenceTxHash_NotNull]
        ON [MealPlanPurchases]([ReferenceTxHash])
        WHERE [ReferenceTxHash] IS NOT NULL;
END
GO

-- =====================================================
-- Migration: Marketplace 80/20 Revenue Split
-- Adds seller wallet to listings, split tracking to purchases
-- =====================================================

-- 5) SellerWalletAddress auf MealPlanListings (Wallet des Creators)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanListings' AND COLUMN_NAME = 'SellerWalletAddress'
)
BEGIN
    ALTER TABLE [MealPlanListings]
    ADD [SellerWalletAddress] NVARCHAR(64) NULL;
END
GO

-- 6) SellerHash auf MealPlanPurchases (NullifierHash des Sellers)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'SellerHash'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [SellerHash] NVARCHAR(256) NOT NULL DEFAULT '';
END
GO

-- 7) SellerWalletAddress auf MealPlanPurchases (Wallet des Sellers zum Zeitpunkt des Kaufs)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'SellerWalletAddress'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [SellerWalletAddress] NVARCHAR(64) NULL;
END
GO

-- 8) CreatorAmount - wie viel der Creator bekommen hat (80%)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'CreatorAmount'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [CreatorAmount] DECIMAL(18,6) NOT NULL DEFAULT 0;
END
GO

-- 9) PlatformFee - wie viel die Platform bekommen hat (20%)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'PlatformFee'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [PlatformFee] DECIMAL(18,6) NOT NULL DEFAULT 0;
END
GO

-- 10) Index fuer schnelle Abfrage: Alle Verkaeufe eines Sellers
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_MealPlanPurchases_SellerHash'
      AND object_id = OBJECT_ID('MealPlanPurchases')
)
BEGIN
    CREATE INDEX [IX_MealPlanPurchases_SellerHash]
        ON [MealPlanPurchases]([SellerHash]);
END
GO

-- 11) Bestehende Purchases nachtraeglich mit SellerHash befuellen
UPDATE p
SET p.SellerHash = l.SellerHash,
    p.SellerWalletAddress = l.SellerWalletAddress,
    p.CreatorAmount = p.PricePaid * 0.8,
    p.PlatformFee = p.PricePaid * 0.2
FROM MealPlanPurchases p
INNER JOIN MealPlanListings l ON p.ListingId = l.Id
WHERE p.SellerHash = '';
GO

-- =====================================================
-- Migration: PaymentToken + CreatedMealPlanId
-- Neue Spalten fuer Multi-Token Support und Kauf-Zuordnung
-- =====================================================

-- 12) PaymentToken auf MealPlanPurchases (WLD, USDCE, etc.)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'PaymentToken'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [PaymentToken] NVARCHAR(20) NOT NULL DEFAULT 'WLD';
END
GO

-- 13) CreatedMealPlanId - Verweis auf den kopierten Essensplan des Kaeufers
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MealPlanPurchases' AND COLUMN_NAME = 'CreatedMealPlanId'
)
BEGIN
    ALTER TABLE [MealPlanPurchases]
    ADD [CreatedMealPlanId] INT NOT NULL DEFAULT 0;
END
GO

-- 14) Index fuer schnelle Abfrage: Alle Kaeufe eines Buyers (fuer Profil "Meine Kaeufe" Tab)
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_MealPlanPurchases_BuyerHash'
      AND object_id = OBJECT_ID('MealPlanPurchases')
)
BEGIN
    CREATE INDEX [IX_MealPlanPurchases_BuyerHash]
        ON [MealPlanPurchases]([BuyerHash])
        INCLUDE ([ListingId], [PurchasedAt]);
END
GO

-- =====================================================
-- Migration: MealPlanPurchases Umbau -> WorldMealplanPurcase
-- Neue Logik: zentrale Kauf-/Cashout-Dokumentation direkt auf der Purchase-Tabelle
-- =====================================================

-- 15) Alte Ledger-Tabelle wieder entfernen (falls vorhanden)
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'MealPlanSalesTransactions'
)
BEGIN
    DROP TABLE [dbo].[MealPlanSalesTransactions];
END
GO

-- 16) Vor Umbau alle Bestandsdaten loeschen und Tabelle umbenennen
IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'MealPlanPurchases'
)
AND NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = 'WorldMealplanPurcase'
)
BEGIN
    DELETE FROM [MealPlanPurchases];
    EXEC sp_rename 'MealPlanPurchases', 'WorldMealplanPurcase';
END
GO

-- 17) Sender- / Receiver-Referenzen (optional auf WorldAppUser)
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'SenderUserId'
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase] ADD [SenderUserId] INT NULL;
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'ReceiverUserId'
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase] ADD [ReceiverUserId] INT NULL;
END
GO

-- 18) Sender / Receiver Hashes
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'SenderUserHash'
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase] ADD [SenderUserHash] NVARCHAR(256) NOT NULL DEFAULT '';
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'ReceiverUserHash'
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase] ADD [ReceiverUserHash] NVARCHAR(256) NOT NULL DEFAULT '';
END
GO

-- 19) Auftragszeit / Status / Gutschrift-Flags
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'OrderTimestampUtc'
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase] ADD [OrderTimestampUtc] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'Status'
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase] ADD [Status] NVARCHAR(20) NOT NULL DEFAULT 'ok';
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'SellerCredited'
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase] ADD [SellerCredited] BIT NOT NULL DEFAULT 0;
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'SellerCreditedAtUtc'
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase] ADD [SellerCreditedAtUtc] DATETIME2 NULL;
END
GO

-- 20) Kauf- und Cashout-Transaktionsreferenzen
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'PurchaseTransactionId'
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase] ADD [PurchaseTransactionId] NVARCHAR(130) NULL;
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'CashoutTransactionId'
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase] ADD [CashoutTransactionId] NVARCHAR(130) NULL;
END
GO

-- 21) Constraints und FK-Relationen
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_WorldMealplanPurcase_Status'
      AND parent_object_id = OBJECT_ID('WorldMealplanPurcase')
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase]
    ADD CONSTRAINT [CK_WorldMealplanPurcase_Status]
    CHECK ([Status] IN ('ok', 'fehlgeschlagen'));
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_WorldMealplanPurcase_SenderUser'
      AND parent_object_id = OBJECT_ID('WorldMealplanPurcase')
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase]
    ADD CONSTRAINT [FK_WorldMealplanPurcase_SenderUser]
    FOREIGN KEY ([SenderUserId]) REFERENCES [WorldAppUser]([Id]);
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_WorldMealplanPurcase_ReceiverUser'
      AND parent_object_id = OBJECT_ID('WorldMealplanPurcase')
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase]
    ADD CONSTRAINT [FK_WorldMealplanPurcase_ReceiverUser]
    FOREIGN KEY ([ReceiverUserId]) REFERENCES [WorldAppUser]([Id]);
END
GO

-- 22) Such- und Duplikat-Indexes
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_WorldMealplanPurcase_OrderTimestampUtc'
      AND object_id = OBJECT_ID('WorldMealplanPurcase')
)
BEGIN
    CREATE INDEX [IX_WorldMealplanPurcase_OrderTimestampUtc]
        ON [WorldMealplanPurcase]([OrderTimestampUtc]);
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_WorldMealplanPurcase_SenderUserHash'
      AND object_id = OBJECT_ID('WorldMealplanPurcase')
)
BEGIN
    CREATE INDEX [IX_WorldMealplanPurcase_SenderUserHash]
        ON [WorldMealplanPurcase]([SenderUserHash]);
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_WorldMealplanPurcase_ReceiverUserHash'
      AND object_id = OBJECT_ID('WorldMealplanPurcase')
)
BEGIN
    CREATE INDEX [IX_WorldMealplanPurcase_ReceiverUserHash]
        ON [WorldMealplanPurcase]([ReceiverUserHash]);
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_WorldMealplanPurcase_PurchaseTransactionId_NotNull'
      AND object_id = OBJECT_ID('WorldMealplanPurcase')
)
BEGIN
    CREATE UNIQUE INDEX [UX_WorldMealplanPurcase_PurchaseTransactionId_NotNull]
        ON [WorldMealplanPurcase]([PurchaseTransactionId])
        WHERE [PurchaseTransactionId] IS NOT NULL;
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_WorldMealplanPurcase_CashoutTransactionId'
      AND object_id = OBJECT_ID('WorldMealplanPurcase')
)
BEGIN
    CREATE INDEX [IX_WorldMealplanPurcase_CashoutTransactionId]
        ON [WorldMealplanPurcase]([CashoutTransactionId]);
END
GO

-- =====================================================
-- Migration-Fix: Doppelte Hash-Spalten entfernen
-- BuyerHash == SenderUserHash, SellerHash == ReceiverUserHash
-- =====================================================

-- 23) Alte (doppelte) Hash-Indizes entfernen
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_WorldMealplanPurcase_SenderUserHash'
      AND object_id = OBJECT_ID('WorldMealplanPurcase')
)
BEGIN
    DROP INDEX [IX_WorldMealplanPurcase_SenderUserHash] ON [WorldMealplanPurcase];
END
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_WorldMealplanPurcase_ReceiverUserHash'
      AND object_id = OBJECT_ID('WorldMealplanPurcase')
)
BEGIN
    DROP INDEX [IX_WorldMealplanPurcase_ReceiverUserHash] ON [WorldMealplanPurcase];
END
GO

-- 24) Doppelte Hash-Spalten entfernen, wenn vorhanden
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'SenderUserHash'
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase] DROP COLUMN [SenderUserHash];
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'ReceiverUserHash'
)
BEGIN
    ALTER TABLE [WorldMealplanPurcase] DROP COLUMN [ReceiverUserHash];
END
GO

-- 25) Stattdessen Indizes auf bestehende Hash-Spalten BuyerHash/SellerHash sicherstellen
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'BuyerHash'
)
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_WorldMealplanPurcase_BuyerHash'
      AND object_id = OBJECT_ID('WorldMealplanPurcase')
)
BEGIN
    CREATE INDEX [IX_WorldMealplanPurcase_BuyerHash]
        ON [WorldMealplanPurcase]([BuyerHash]);
END
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'WorldMealplanPurcase')
AND EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'WorldMealplanPurcase' AND COLUMN_NAME = 'SellerHash'
)
AND NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_WorldMealplanPurcase_SellerHash'
      AND object_id = OBJECT_ID('WorldMealplanPurcase')
)
BEGIN
    CREATE INDEX [IX_WorldMealplanPurcase_SellerHash]
        ON [WorldMealplanPurcase]([SellerHash]);
END
GO

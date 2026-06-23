-- =============================================================================
-- Marktplatz: Angebotstypen (Objekt / Digitales Produkt / Einzelrezept /
-- Dienstleistung) zusätzlich zum bestehenden Essensplan.
--
-- Erweitert dbo.MealPlanListings und dbo.WorldMealplanPurcase um die nötigen
-- Spalten. Einmalig auf der Azure-SQL-DB ausführen. Idempotent. Kein GO (Azure-safe).
--
-- HINWEIS: Diese Spalten werden manuell ergänzt (keine EF-Migration). Beim nächsten
-- Scaffold einer EF-Migration müssen sie dort bereits berücksichtigt sein, sonst
-- versucht die Migration sie erneut anzulegen ("column already exists").
-- =============================================================================

-- ---- dbo.MealPlanListings --------------------------------------------------

-- Discriminator: deckt Bestandsdaten via Default 'MealPlan' ab.
IF COL_LENGTH(N'dbo.MealPlanListings', N'ListingType') IS NULL
    ALTER TABLE dbo.MealPlanListings
        ADD ListingType NVARCHAR(32) NOT NULL
            CONSTRAINT DF_MealPlanListings_ListingType DEFAULT (N'MealPlan');

IF COL_LENGTH(N'dbo.MealPlanListings', N'RecipeId') IS NULL
    ALTER TABLE dbo.MealPlanListings ADD RecipeId INT NULL;

IF COL_LENGTH(N'dbo.MealPlanListings', N'DigitalFileUrl') IS NULL
    ALTER TABLE dbo.MealPlanListings ADD DigitalFileUrl NVARCHAR(1024) NULL;

IF COL_LENGTH(N'dbo.MealPlanListings', N'StockQuantity') IS NULL
    ALTER TABLE dbo.MealPlanListings ADD StockQuantity INT NULL;

IF COL_LENGTH(N'dbo.MealPlanListings', N'RequiresShipping') IS NULL
    ALTER TABLE dbo.MealPlanListings
        ADD RequiresShipping BIT NOT NULL
            CONSTRAINT DF_MealPlanListings_RequiresShipping DEFAULT (0);

IF COL_LENGTH(N'dbo.MealPlanListings', N'CoverImageUrl') IS NULL
    ALTER TABLE dbo.MealPlanListings ADD CoverImageUrl NVARCHAR(1024) NULL;

IF COL_LENGTH(N'dbo.MealPlanListings', N'ImagesJson') IS NULL
    ALTER TABLE dbo.MealPlanListings ADD ImagesJson NVARCHAR(MAX) NULL;

-- MealPlanId muss für Nicht-Essensplan-Typen NULL erlaubt sein.
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.MealPlanListings')
      AND name = N'MealPlanId'
      AND is_nullable = 0
)
    ALTER TABLE dbo.MealPlanListings ALTER COLUMN MealPlanId INT NULL;

-- Index: aktive Listings nach Angebotstyp filtern.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_MealPlanListings_Type_Active'
      AND object_id = OBJECT_ID(N'dbo.MealPlanListings')
)
    CREATE INDEX IX_MealPlanListings_Type_Active
        ON dbo.MealPlanListings (ListingType, IsActive, CreatedAt DESC);

-- ---- dbo.WorldMealplanPurcase (Käufe; Legacy-Tabellenname mit Tippfehler) ---

IF COL_LENGTH(N'dbo.WorldMealplanPurcase', N'ListingType') IS NULL
    ALTER TABLE dbo.WorldMealplanPurcase ADD ListingType NVARCHAR(32) NULL;

IF COL_LENGTH(N'dbo.WorldMealplanPurcase', N'BuyerShippingAddress') IS NULL
    ALTER TABLE dbo.WorldMealplanPurcase ADD BuyerShippingAddress NVARCHAR(MAX) NULL;

-- CreatedMealPlanId muss für Digital/Objekt/Dienstleistung NULL erlaubt sein.
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.WorldMealplanPurcase')
      AND name = N'CreatedMealPlanId'
      AND is_nullable = 0
)
    ALTER TABLE dbo.WorldMealplanPurcase ALTER COLUMN CreatedMealPlanId INT NULL;

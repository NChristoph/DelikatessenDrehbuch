using System.Text.Json;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Extensions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Models;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    // NOTE: We intentionally store jobs in SQL (not Redis) so Azure instances/recycles don't lose state.
    public sealed class RecipeAiVariantJobService : IRecipeAiVariantJobService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IBackgroundTaskQueue _queue;
        private readonly ILogger<RecipeAiVariantJobService> _logger;

        private static readonly SemaphoreSlim EnsureSchemaLock = new(1, 1);
        private static volatile bool SchemaEnsured = false;

        private static readonly SemaphoreSlim EnsureNotificationsSchemaLock = new(1, 1);
        private static volatile bool NotificationsSchemaEnsured = false;

        // Keep this Azure-safe (no GO). This runs only when the table is missing (or once per process).
        private const string EnsureSchemaSql = @"
IF OBJECT_ID(N'[dbo].[RecipeAiVariantJobs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[RecipeAiVariantJobs](
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_RecipeAiVariantJobs] PRIMARY KEY,
        [JobId] NVARCHAR(64) NOT NULL,
        [BaseRecipeId] INT NOT NULL,
        [VariantType] NVARCHAR(64) NOT NULL,
        [Language] NVARCHAR(12) NOT NULL,
        [AiProvider] NVARCHAR(32) NOT NULL,
        [CreatedByUserHash] NVARCHAR(256) NULL,
        [State] NVARCHAR(32) NOT NULL,
        [Message] NVARCHAR(400) NULL,
        [Title] NVARCHAR(256) NULL,
        [RequestJson] NVARCHAR(MAX) NOT NULL,
        [ResultJson] NVARCHAR(MAX) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_RecipeAiVariantJobs_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_RecipeAiVariantJobs_UpdatedAtUtc] DEFAULT (SYSUTCDATETIME())
    );

    ALTER TABLE [dbo].[RecipeAiVariantJobs] WITH CHECK
    ADD CONSTRAINT [FK_RecipeAiVariantJobs_RecipeBaseData_BaseRecipeId]
        FOREIGN KEY([BaseRecipeId]) REFERENCES [dbo].[RecipeBaseData]([Id])
        ON DELETE CASCADE;

    CREATE UNIQUE INDEX [IX_RecipeAiVariantJobs_JobId] ON [dbo].[RecipeAiVariantJobs]([JobId]);
    CREATE INDEX [IX_RecipeAiVariantJobs_BaseRecipeId_UpdatedAtUtc] ON [dbo].[RecipeAiVariantJobs]([BaseRecipeId], [UpdatedAtUtc]);
END;
";

        // Lightweight notification table used by the feed bell overlay.
        private const string EnsureNotificationsSchemaSql = @"
IF OBJECT_ID(N'[dbo].[WorldUserNotifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WorldUserNotifications](
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_WorldUserNotifications] PRIMARY KEY,
        [UserHash] NVARCHAR(256) NOT NULL,
        [Icon] NVARCHAR(64) NOT NULL CONSTRAINT [DF_WorldUserNotifications_Icon] DEFAULT (N'bi-bell'),
        [Sender] NVARCHAR(128) NOT NULL CONSTRAINT [DF_WorldUserNotifications_Sender] DEFAULT (N'system'),
        [Description] NVARCHAR(1000) NOT NULL,
        [Href] NVARCHAR(600) NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_WorldUserNotifications_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [IsSeen] BIT NOT NULL CONSTRAINT [DF_WorldUserNotifications_IsSeen] DEFAULT (0),
        [SeenAtUtc] DATETIME2 NULL
    );

    CREATE INDEX [IX_WorldUserNotifications_UserHash_IsSeen_CreatedAtUtc]
        ON [dbo].[WorldUserNotifications]([UserHash], [IsSeen], [CreatedAtUtc]);
END;
";

        public RecipeAiVariantJobService(
            IServiceScopeFactory scopeFactory,
            IBackgroundTaskQueue queue,
            ILogger<RecipeAiVariantJobService> logger)
        {
            _scopeFactory = scopeFactory;
            _queue = queue;
            _logger = logger;
        }

        public async Task<RecipeAiVariantJobStartResponse> StartJobAsync(
            RecipeAiTransformRequest request,
            string language,
            string? userHash,
            CancellationToken cancellationToken)
        {
            if (request == null || request.RecipeId <= 0 || string.IsNullOrWhiteSpace(request.VariantType))
            {
                throw new InvalidOperationException("Rezept oder AI-Variante fehlt.");
            }

            var normalizedLanguage = (language ?? "de").Trim().ToLowerInvariant();
            var normalizedProvider = string.IsNullOrWhiteSpace(request.AiProvider) ? "openai" : request.AiProvider.Trim().ToLowerInvariant();
            var jobId = Guid.NewGuid().ToString("N");

            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await EnsureSchemaAsync(db, cancellationToken);
                await EnsureNotificationsSchemaAsync(db, cancellationToken);
                var recipeExists = await db.RecipeBaseData.AsNoTracking().AnyAsync(r => r.Id == request.RecipeId, cancellationToken);
                if (!recipeExists)
                {
                    throw new InvalidOperationException("Rezept wurde nicht gefunden.");
                }

                var job = new RecipeAiVariantJob
                {
                    JobId = jobId,
                    BaseRecipeId = request.RecipeId,
                    VariantType = request.VariantType.Trim().ToLowerInvariant(),
                    Language = normalizedLanguage,
                    AiProvider = normalizedProvider,
                    CreatedByUserHash = string.IsNullOrWhiteSpace(userHash) ? null : userHash,
                    State = RecipeAiVariantJobState.Queued,
                    Message = "Wird vorbereitet...",
                    RequestJson = JsonSerializer.Serialize(request, JsonOptions),
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                try
                {
                    await db.RecipeAiVariantJobs.AddAsync(job, cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex) when (LooksLikeMissingJobTable(ex))
                {
                    // Attempt self-heal (e.g. Azure deploy without migrations), then retry once.
                    await EnsureSchemaAsync(db, cancellationToken);
                    await db.RecipeAiVariantJobs.AddAsync(job, cancellationToken);
                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            // Run in background with its own DI scope.
            await _queue.QueueAsync(ct => new ValueTask(RunJobAsync(jobId, ct)));

            return new RecipeAiVariantJobStartResponse { JobId = jobId };
        }

        public async Task<RecipeAiVariantJobStatusResponse?> GetStatusAsync(string jobId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return null;
            jobId = jobId.Trim();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await EnsureSchemaAsync(db, cancellationToken);

            try
            {
                var job = await db.RecipeAiVariantJobs
                    .AsNoTracking()
                    .Where(x => x.JobId == jobId)
                    .Select(x => new { x.JobId, x.State, x.Message, x.BaseRecipeId, x.VariantType, x.AiProvider, x.Title })
                    .FirstOrDefaultAsync(cancellationToken);

                if (job == null) return null;

                return new RecipeAiVariantJobStatusResponse
                {
                    JobId = job.JobId,
                    State = job.State,
                    Message = job.Message,
                    RecipeId = job.BaseRecipeId,
                    VariantType = job.VariantType,
                    AiProvider = job.AiProvider,
                    Title = job.Title
                };
            }
            catch (Exception ex) when (LooksLikeMissingJobTable(ex))
            {
                throw new InvalidOperationException(
                    "Datenbank-Update fehlt: Tabelle dbo.RecipeAiVariantJobs existiert nicht. " +
                    "Bitte das SQL-Skript Sql/RecipeAiVariantJobs.sql in deiner DB ausfuehren.");
            }
        }

        public async Task<RecipeAiTransformPreview?> GetResultAsync(string jobId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return null;
            jobId = jobId.Trim();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await EnsureSchemaAsync(db, cancellationToken);

            try
            {
                var job = await db.RecipeAiVariantJobs
                    .AsNoTracking()
                    .Where(x => x.JobId == jobId && x.State == RecipeAiVariantJobState.Ready)
                    .Select(x => new { x.ResultJson })
                    .FirstOrDefaultAsync(cancellationToken);

                if (job == null || string.IsNullOrWhiteSpace(job.ResultJson))
                {
                    return null;
                }

                try
                {
                    return JsonSerializer.Deserialize<RecipeAiTransformPreview>(job.ResultJson, JsonOptions);
                }
                catch
                {
                    return null;
                }
            }
            catch (Exception ex) when (LooksLikeMissingJobTable(ex))
            {
                throw new InvalidOperationException(
                    "Datenbank-Update fehlt: Tabelle dbo.RecipeAiVariantJobs existiert nicht. " +
                    "Bitte das SQL-Skript Sql/RecipeAiVariantJobs.sql in deiner DB ausfuehren.");
            }
        }

        private async Task RunJobAsync(string jobId, CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await EnsureSchemaAsync(db, cancellationToken);
                await EnsureNotificationsSchemaAsync(db, cancellationToken);
                var transform = scope.ServiceProvider.GetRequiredService<IRecipeAiTransformService>();
                var nutrition = scope.ServiceProvider.GetRequiredService<IRecipeAiNutritionService>();

                var job = await db.RecipeAiVariantJobs.FirstOrDefaultAsync(x => x.JobId == jobId, cancellationToken);
                if (job == null)
                {
                    return;
                }

                job.State = RecipeAiVariantJobState.Running;
                job.Message = "AI generiert Vorschau...";
                job.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                var request = JsonSerializer.Deserialize<RecipeAiTransformRequest>(job.RequestJson ?? "{}", JsonOptions)
                              ?? new RecipeAiTransformRequest { RecipeId = job.BaseRecipeId, VariantType = job.VariantType, AiProvider = job.AiProvider };

                var recipe = await db.RecipeBaseData
                    .AsNoTracking()
                    .IncludeFullRecipeDetails()
                    .FirstOrDefaultAsync(r => r.Id == job.BaseRecipeId, cancellationToken);

                if (recipe == null)
                {
                    job.State = RecipeAiVariantJobState.Error;
                    job.Message = "Rezept wurde nicht gefunden.";
                    job.UpdatedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                    await TryAddAiNotificationAsync(db, job, isError: true, cancellationToken);
                    return;
                }

                if (recipe == null)
                {
                    job.State = RecipeAiVariantJobState.Error;
                    job.Message = "Rezept wurde nicht gefunden.";
                    job.UpdatedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                    return;
                }

                List<RecipeAiIngredientSuggestionItem>? selectedIngredients = null;
                var selectedIngredientIds = request.SelectedIngredientIds?.Where(x => x > 0).Distinct().ToList() ?? new List<int>();
                if (selectedIngredientIds.Count > 0)
                {
                    var ingredients = await db.IngredientsAndNutrients
                        .AsNoTracking()
                        .Include(x => x.FoodCategory)
                        .Where(x => selectedIngredientIds.Contains(x.Id))
                        .ToListAsync(cancellationToken);

                    selectedIngredients = ingredients
                        .OrderBy(x => selectedIngredientIds.IndexOf(x.Id))
                        .Select(ingredient =>
                        {
                            var ingredientName = job.Language switch
                            {
                                "en" => ingredient.Name_EN,
                                "pt" => ingredient.Name_PRT,
                                "es" => ingredient.Name_ESP,
                                _ => ingredient.Name_DE
                            };

                            var categoryLabel = job.Language switch
                            {
                                "en" => ingredient.FoodCategory?.Name_EN,
                                "pt" => ingredient.FoodCategory?.Name_PRT,
                                "es" => ingredient.FoodCategory?.Name_ESP,
                                _ => ingredient.FoodCategory?.Name_DE
                            };

                            return new RecipeAiIngredientSuggestionItem
                            {
                                IngredientId = ingredient.Id,
                                Name = ingredientName ?? ingredient.Name_DE ?? ingredient.Name_EN ?? "Zutat",
                                CategoryKey = ingredient.FoodCategory?.CategoryKey ?? string.Empty,
                                CategoryLabel = categoryLabel ?? ingredient.FoodCategory?.Name_DE ?? string.Empty,
                                ProteinPer100g = ingredient.Protein_a_100g,
                                CarbsPer100g = ingredient.Carbohydrates_a_100g,
                                FatPer100g = ingredient.Fat_a_100g,
                                FiberPer100g = ingredient.Fiber_a_100g,
                                CaloriesPer100g = ingredient.Calories_a_100g,
                                AiReason = "user-selected"
                            };
                        })
                        .ToList();
                }

                var selectedConcept = string.IsNullOrWhiteSpace(request.SelectedConceptKey)
                    ? null
                    : new RecipeAiIngredientSuggestionItem
                    {
                        ConceptKey = request.SelectedConceptKey?.Trim() ?? string.Empty,
                        ConceptTitle = request.SelectedConceptTitle?.Trim() ?? string.Empty,
                        ConceptSummary = request.SelectedConceptSummary?.Trim() ?? string.Empty,
                        ConceptApproach = request.SelectedConceptApproach?.Trim() ?? string.Empty,
                        ConceptIngredientPlan = request.SelectedConceptIngredientPlan ?? new List<RecipeAiConceptIngredientPlanItem>()
                    };

                var preview = await transform.BuildPreviewAsync(
                    recipe,
                    request.VariantType,
                    request.AiProvider ?? "openai",
                    job.Language,
                    request.UserNote,
                    request.AppliedChangeCount,
                    selectedConcept: selectedConcept,
                    selectedIngredients: selectedIngredients,
                    cancellationToken: cancellationToken);

                preview.AiProvider = request.AiProvider ?? "openai";
                preview.LanguageCode = job.Language;
                preview.Nutrition = await nutrition.BuildAiPreviewNutritionAsync(preview, job.Language, cancellationToken);

                job.State = RecipeAiVariantJobState.Ready;
                job.Message = "Fertig";
                job.Title = string.IsNullOrWhiteSpace(preview.Title) ? null : preview.Title.Trim();
                job.ResultJson = JsonSerializer.Serialize(preview, JsonOptions);
                job.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);

                await TryAddAiNotificationAsync(db, job, isError: false, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI background job failed jobId={JobId}", jobId);

                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await EnsureNotificationsSchemaAsync(db, cancellationToken);
                    var job = await db.RecipeAiVariantJobs.FirstOrDefaultAsync(x => x.JobId == jobId, cancellationToken);
                    if (job != null)
                    {
                        job.State = RecipeAiVariantJobState.Error;
                        job.Message = ex.Message;
                        job.UpdatedAtUtc = DateTime.UtcNow;
                        await db.SaveChangesAsync(cancellationToken);
                        await TryAddAiNotificationAsync(db, job, isError: true, cancellationToken);
                    }
                }
                catch
                {
                    // swallow
                }
            }
        }

        private static string BuildAiNotificationHref(RecipeAiVariantJob job)
        {
            var recipeId = job.BaseRecipeId;
            var variantType = Uri.EscapeDataString(job.VariantType ?? string.Empty);
            var aiProvider = Uri.EscapeDataString(job.AiProvider ?? "openai");
            var jobId = Uri.EscapeDataString(job.JobId ?? string.Empty);
            return $"wm-ai://open?jobId={jobId}&recipeId={recipeId}&variantType={variantType}&aiProvider={aiProvider}";
        }

        private async Task TryAddAiNotificationAsync(ApplicationDbContext db, RecipeAiVariantJob job, bool isError, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(job.CreatedByUserHash)) return;

                var sender = "ai";
                var baseText = isError ? "AI ist fehlgeschlagen." : "AI Vorschau ist fertig.";
                var title = string.IsNullOrWhiteSpace(job.Title) ? string.Empty : job.Title!.Trim();
                var desc = string.IsNullOrWhiteSpace(title) ? baseText : $"{baseText}\n{title}";
                var href = BuildAiNotificationHref(job);

                var exists = await db.WorldUserNotifications.AsNoTracking()
                    .AnyAsync(x => x.UserHash == job.CreatedByUserHash && x.Sender == sender && x.Href == href, cancellationToken);
                if (exists) return;

                db.WorldUserNotifications.Add(new WorldUserNotification
                {
                    UserHash = job.CreatedByUserHash,
                    Icon = isError ? "bi-exclamation-triangle" : "bi-stars",
                    Sender = sender,
                    Description = desc,
                    Href = href,
                    CreatedAtUtc = DateTime.UtcNow,
                    IsSeen = false,
                    SeenAtUtc = null
                });

                await db.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // best-effort
            }
        }

        private static bool LooksLikeMissingJobTable(Exception ex)
        {
            // Avoid hard dependency on a specific SQL client package; string match is enough for the UX hint.
            var text = ex.ToString();
            if (string.IsNullOrWhiteSpace(text)) return false;

            return text.Contains("RecipeAiVariantJobs", StringComparison.OrdinalIgnoreCase)
                && (text.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("Cannot find the object", StringComparison.OrdinalIgnoreCase));
        }

        private static async Task EnsureSchemaAsync(ApplicationDbContext db, CancellationToken cancellationToken)
        {
            if (SchemaEnsured) return;

            await EnsureSchemaLock.WaitAsync(cancellationToken);
            try
            {
                if (SchemaEnsured) return;
                await db.Database.ExecuteSqlRawAsync(EnsureSchemaSql, cancellationToken);
                SchemaEnsured = true;
            }
            finally
            {
                EnsureSchemaLock.Release();
            }
        }

        private static async Task EnsureNotificationsSchemaAsync(ApplicationDbContext db, CancellationToken cancellationToken)
        {
            if (NotificationsSchemaEnsured) return;

            await EnsureNotificationsSchemaLock.WaitAsync(cancellationToken);
            try
            {
                if (NotificationsSchemaEnsured) return;
                await db.Database.ExecuteSqlRawAsync(EnsureNotificationsSchemaSql, cancellationToken);
                NotificationsSchemaEnsured = true;
            }
            finally
            {
                EnsureNotificationsSchemaLock.Release();
            }
        }
    }
}

using DelikatessenDrehbuch.Areas.WorldMiniApp.Exceptions;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using DelikatessenDrehbuch.Email;
using DelikatessenDrehbuch.MealPlaner.MealPlanerServices;
using DelikatessenDrehbuch.MealPlaner.MealPlanerServices.Interfaces;
using DelikatessenDrehbuch.Middleware;
using DelikatessenDrehbuch.MyExceptions;
using DelikatessenDrehbuch.Services;
using DelikatessenDrehbuch.Services.Interfaces;
using DelikatessenDrehbuch.ShoppingList.Services.Interfaces;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;
using Stripe;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Kestrel-Limits für große Video-Uploads erhöhen
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 250_000_000; // 250 MB
});

// ================================================================================
// 1. Localization Services registrieren
// ================================================================================
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("de"),    // Deutsch
        new CultureInfo("en"),    // English
        new CultureInfo("es"),    // Español (Cookie: esp → Culture: es)
        new CultureInfo("pt"),    // Português (Cookie: prt → Culture: pt)
        new CultureInfo("id"),    // Indonesian
        new CultureInfo("nl"),    // Dutch
        new CultureInfo("sv"),    // Swedish
        new CultureInfo("da"),    // Danish
        new CultureInfo("nb"),    // Norwegian Bokmål (Cookie: no → Culture: nb)
        new CultureInfo("ms")     // Malay
    };

    options.DefaultRequestCulture = new RequestCulture("de");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // Cookie-Provider hat höchste Priorität (für Abwärtskompatibilität mit deli-lang Cookie)
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CustomCookieRequestCultureProvider(), // Custom Provider für deli-lang Cookie
        new QueryStringRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

var defaultCulture = new CultureInfo("de-AT");

// Optional, aber hilfreich (setzt auch Server-Default)
CultureInfo.DefaultThreadCurrentCulture = defaultCulture;
CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;


var retryPolicy = Policy
    .Handle<SqlException>()
    .WaitAndRetry(
        retryCount: 5,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(2),
        onRetry: (exception, sleepDuration, attempt, context) =>
        {
            // Logging, falls gewünscht
            Console.WriteLine($"Retry {attempt} due to {exception}");
        });
var configuration = builder.Configuration;
// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Environment Variable Override für Production
var envConnectionString = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTIONSTRING");
if (!string.IsNullOrWhiteSpace(envConnectionString))
{
    connectionString = envConnectionString;
}

var bunnyStoragePassword = Environment.GetEnvironmentVariable("BUNNY_STORAGE_PASSWORD");
if (!string.IsNullOrWhiteSpace(bunnyStoragePassword))
    builder.Configuration["Bunny_Net_Passwort_Lager"] = bunnyStoragePassword;

var bunnyStreamApi = Environment.GetEnvironmentVariable("BUNNY_STREAM_API_KEY");
if (!string.IsNullOrWhiteSpace(bunnyStreamApi))
    builder.Configuration["Bunny_Net_Api_Stream"] = bunnyStreamApi;

var emailPassword = Environment.GetEnvironmentVariable("EMAIL_PASSWORD");
if (!string.IsNullOrWhiteSpace(emailPassword))
    builder.Configuration["EmailSettings:Password"] = emailPassword;

var superUserHash = Environment.GetEnvironmentVariable("WORLDMINIAPP_SUPERUSER_HASH");
if (!string.IsNullOrWhiteSpace(superUserHash))
    builder.Configuration["WorldMiniApp:SuperUserHash"] = superUserHash;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddScoped<IMealPlanService, MealPlanService>();
builder.Services.AddScoped<IRecipesService, RecipesService>();
builder.Services.AddScoped<IIngredientService, IngredientService>();
builder.Services.AddScoped<IFoodCategoryService, FoodCategoryService>();
builder.Services.AddScoped<IIngredientResolverService, IngredientResolverService>();
builder.Services.AddScoped<IAdminControllerModelService, AdminControllerModelService>();
builder.Services.AddScoped<IBlobAzureService, BlobAzureService>();
builder.Services.AddScoped<IQueryService, QueryService>();
builder.Services.AddScoped<IRecipesHandlerService, RecipesHandlerService>();
builder.Services.AddScoped<IQuantityService, QuantityService>();
builder.Services.AddScoped<IMeasureService, MeasureService>();
builder.Services.AddScoped<INutrientService, NutrientService>();
builder.Services.AddScoped<ISupportTicketService, SupportTicketService>();
builder.Services.AddScoped<IRecessionsService, RecessionsService>();
builder.Services.AddScoped<ILikeService, LikeService>();
builder.Services.AddScoped<IUtilityService, UtilityService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IMealPlanSortByFilters, MealPlanSortByFilters>();
builder.Services.AddScoped<IFullRecipeDataService, FullRecipeDataService>();
builder.Services.AddScoped<IIngredientScaleService, IngredientScaleService>();
builder.Services.AddScoped<IMealPlanEditorService, MealPlanEditorService>();
builder.Services.AddScoped<IShoppingListService, ShoppingListService>();
builder.Services.AddScoped<ISearchRecipeService, SearchRecipeService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserManager, UserManager>();
builder.Services.AddScoped<IBlobUploadService, BunnyUploadService>();
builder.Services.AddScoped<IWorldAppMealPlanService, WorldAppMealPlanService>();
builder.Services.AddScoped<ISaveNewRecipeService, SaveNewRecipeService>();
builder.Services.AddScoped<CaptionGenerationService>();
builder.Services.AddScoped<WorldMiniApp.Services.IFeedAlgorithmService, WorldMiniApp.Services.FeedAlgorithmService>();
builder.Services.AddScoped<IAdInjectionService, AdInjectionService>();
builder.Services.AddScoped<TradingAgentService>();
builder.Services.AddScoped<BlockchainService>();
builder.Services.AddScoped<DexService>();
builder.Services.AddScoped<TradingStrategyService>();
builder.Services.AddHostedService<TradingAgentBackgroundService>();

// Recipe Translation System (NEW) - OLD service removed, using RecipeTranslationService now

// HttpClient für Bunny Storage API
builder.Services.AddHttpClient("BunnyStorage", client =>
{
    // 15 Minuten Timeout für große Video-Uploads (bis zu 200MB)
    // Bei 2 Mbit/s Upload: ~13 Minuten für 200MB
    client.Timeout = TimeSpan.FromMinutes(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("DelikatessenDrehbuch/1.0");
});
builder.Services.AddHttpClient<IIngredientSwapAiService, IngredientSwapAiService>(client =>
{
    // 2 Minuten für AI-Rezept-Transformationen (OpenAI API)
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddHttpClient<IRecipeSwapStepAiService, RecipeSwapStepAiService>(client =>
{
    // 2 Minuten für AI-Rezept-Schritte (OpenAI API)
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddHttpClient<IRecipeVariantSummaryAiService, RecipeVariantSummaryAiService>(client =>
{
    // 2 Minuten für AI-Varianten-Zusammenfassung (OpenAI API)
    client.Timeout = TimeSpan.FromMinutes(2);
});
// Marketplace Service (formerly WildCoinService - renamed for clarity)
builder.Services.AddScoped<IMarketplaceService, MarketplaceService>();
// DEPRECATED: IWildCoinService still available for backwards compatibility
builder.Services.AddScoped<IWildCoinService, MarketplaceService>();
builder.Services.AddScoped<IWorldClipWatchService, WorldClipWatchService>();
builder.Services.AddScoped<IWorldAdPreferenceService, WorldAdPreferenceService>();
builder.Services.AddScoped<IMarketplaceRankingService, MarketplaceRankingService>();
builder.Services.AddHttpClient<IMissingIngredientAiService, MissingIngredientAiService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddHttpClient<IRecipeAiTransformService, RecipeAiTransformService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddSingleton<IBackgroundTaskQueue>(_ => new BackgroundTaskQueue(capacity: 300));
builder.Services.AddHostedService<WorldMiniAppQueuedHostedService>();
builder.Services.AddSingleton<IRecipeAiVariantJobService, RecipeAiVariantJobService>();
builder.Services.AddScoped<IRecipeAiNutritionService, RecipeAiNutritionService>();
builder.Services.AddHttpClient<IRecipeStepGeneratorService, RecipeStepGeneratorService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// NEW: Simple Recipe Translation Service (replaces complex step system)
builder.Services.AddHttpClient<RecipeTranslationService>(client =>
{
    // 3 Minuten Timeout für OpenAI API-Übersetzungen
    // Lange Rezepte mit vielen Schritten können länger dauern
    client.Timeout = TimeSpan.FromMinutes(3);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.Password.RequireDigit = false; // Keine Zahl erforderlich
    options.Password.RequireLowercase = false; // Kein Kleinbuchstabe erforderlich
    options.Password.RequireNonAlphanumeric = false; // Kein Sonderzeichen erforderlich
    options.Password.RequireUppercase = false; // Kein Großbuchstabe erforderlich
    options.Password.RequiredLength = 6; // Mindestlänge des Passworts
    options.Password.RequiredUniqueChars = 1; // Anzahl der erforderlichen eindeutigen Zeichen
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Add MVC with View Localization and Data Annotations Localization
builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();
builder.Services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<EmailSender>();
builder.Services.AddTransient<HelpfulMethods>();
builder.Services.AddTransient<AddRecipeException>();
builder.Services.AddMemoryCache();


builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});



// Füge den Session-Service hinzu
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(365 * 10); // Zeit, bis die Session abläuft
    options.Cookie.MaxAge = TimeSpan.FromDays(365 * 10); // Lebensdauer des Session-Cookies
    options.Cookie.HttpOnly = true; // Sicherheitseinstellungen
    options.Cookie.IsEssential = true; // Erforderlich für EU-Cookie-Richtlinien
});






var stripeApiKey = Environment.GetEnvironmentVariable("STRIPE_API_KEY");

StripeConfiguration.ApiKey = stripeApiKey;

// Füge Dienste hinzu (z.B. für MVC/Controllers)
builder.Services.AddControllersWithViews().AddNewtonsoftJson(options =>
{
    // camelCase für JSON API-Endpoints (Standard für moderne Web APIs)
    options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
});

var app = builder.Build();




using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await StaticData.LoadInternal(db);   // deine Methode
}
app.UseSession();

// ================================================================================
// 2. Localization Middleware aktivieren (VOR UseRouting!)
// ================================================================================
app.UseRequestLocalization();

#region Coop xxs CSP schutz

//app.Use(async (context, next) =>
//{
//    context.Response.Headers.Add("Content-Security-Policy",
//        "default-src 'self'; script-src 'self' https://www.googletagmanager.com 'nonce-random123'; " +
//        "connect-src 'self' wss://localhost:*; style-src 'self' 'unsafe-inline';");
//    await next();
//});




app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Cross-Origin-Opener-Policy", "same-origin");
    await next();
});

#endregion


async Task CreateRolls(IServiceProvider serviceProvider, string roleName)
{
    var roleManager = serviceProvider.GetService<RoleManager<IdentityRole>>();
    var roleExist = await roleManager.RoleExistsAsync(roleName);

    if (!roleExist)
        await roleManager.CreateAsync(new IdentityRole(roleName));

}

async Task CreateDefauldUser(IServiceProvider serviceProvider, string rollName, string userName)
{

    var userManager = serviceProvider.GetService<UserManager<IdentityUser>>();
    var user = await userManager.FindByNameAsync(userName);
    await userManager.AddToRoleAsync(user, rollName);

}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseHsts();
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
            var error = exceptionHandlerFeature?.Error;

            if (error != null)
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

                if (error is WorldMiniAppException worldEx)
                {
                    logger.LogWarning(worldEx, "WorldMiniApp exception: {Message}", worldEx.Message);

                    context.Response.StatusCode = worldEx.StatusCode;
                    context.Response.ContentType = "application/problem+json";

                    await Results.Problem(
                            title: worldEx.Message,
                            statusCode: worldEx.StatusCode)
                        .ExecuteAsync(context);
                    return;
                }

                logger.LogError(error, "Unhandled exception.");
            }

            if (context.Request.Headers.Accept.Any(accept => accept.Contains("text/html", StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.Redirect("/Home/Error");
                return;
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            await Results.Problem(
                    title: "Ein unerwarteter Fehler ist aufgetreten.",
                    statusCode: StatusCodes.Status500InternalServerError)
                .ExecuteAsync(context);
        });
    });
}


app.UseHttpsRedirection();
var staticFileOptions = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");

        // JSON-Dateien explizit mit charset=utf-8 ausliefern (Umlaute ö/ü/ß/ä)
        var contentType = ctx.Context.Response.ContentType;
        if (contentType != null && contentType.Contains("application/json") && !contentType.Contains("charset"))
        {
            ctx.Context.Response.ContentType = "application/json; charset=utf-8";
        }
    }
};

app.UseStaticFiles(staticFileOptions);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "Recipes",
    pattern: "SelectedRecipe/Index/{id}/{name?}",
    defaults: new { controller = "SelectedRecipe", action = "Index" }
);
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapRazorPages();

app.Run();

public partial class Program { }

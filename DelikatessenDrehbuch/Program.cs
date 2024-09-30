using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Identity;
using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Configuration;
using System.Drawing.Text;
using DelikatessenDrehbuch.Email;
using Polly;
using Polly.Retry;
using DelikatessenDrehbuch.StaticScripts;
using Microsoft.Data.SqlClient;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => {
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
builder.Services.AddControllersWithViews();
builder.Services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<EmailSender>();
builder.Services.AddTransient<HelpfulMethods>();
builder.Services.AddMemoryCache();

// Füge den Session-Service hinzu
builder.Services.AddDistributedMemoryCache(); // Für die Nutzung von Sessions im Speicher
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60); // Zeit, bis die Session abläuft
    options.Cookie.HttpOnly = true; // Sicherheitseinstellungen
    options.Cookie.IsEssential = true; // Erforderlich für EU-Cookie-Richtlinien
});


var stripeApiKey = Environment.GetEnvironmentVariable("STRIPE_API_KEY");

//var stripeApiKey = "sk_test_51Pudp8FspvIIGBtcg4p4V0mIFyxOlA81LVgXVmEwLDvL9wC4K81SqHQHX5ORN870vQMPJwwodHFTvD1kL6JksKU600PJAdirM2";

if (string.IsNullOrEmpty(stripeApiKey))
{
    throw new Exception("Stripe API Key is missing. Please set STRIPE_API_KEY environment variable.");
}

// Stripe API-Schlüssel festlegen
StripeConfiguration.ApiKey = stripeApiKey;

// Füge Dienste hinzu (z.B. für MVC/Controllers)
builder.Services.AddControllersWithViews().AddNewtonsoftJson();

var app = builder.Build();
app.UseSession();
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
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}




app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();



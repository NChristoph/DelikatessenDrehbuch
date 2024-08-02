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

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();
builder.Services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<EmailSender>();
builder.Services.AddTransient<HelpfulMethods>();
var app = builder.Build();


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



//using (var scope = app.Services.CreateScope())
//{
//    var serviceProvider = scope.ServiceProvider;
//    CreateRolls(serviceProvider, "Admin").Wait();
//    CreateDefauldUser(serviceProvider, "Admin", "Delikatessen.Drehbuch@outlook.com").Wait();
//}
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



using DelikatessenDrehbuch.Areas.WorldMiniApp.Services;
using DelikatessenDrehbuch.Areas.WorldMiniApp.Services.Interfaces;
using DelikatessenDrehbuch.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DelikatessenDrehbuch.Tests.Helpers;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<IBlobUploadService> MockBlobUploadService { get; } = new();
    public Mock<IAuthService> MockAuthService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add InMemory database
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));

            // Replace external services with mocks
            ReplaceService<IBlobUploadService>(services, MockBlobUploadService.Object);
            ReplaceService<IAuthService>(services, MockAuthService.Object);
        });
    }

    private static void ReplaceService<T>(IServiceCollection services, T implementation) where T : class
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddScoped(_ => implementation);
    }
}

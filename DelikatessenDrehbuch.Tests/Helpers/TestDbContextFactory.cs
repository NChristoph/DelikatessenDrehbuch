using DelikatessenDrehbuch.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DelikatessenDrehbuch.Tests.Helpers;

public static class TestDbContextFactory
{
    /// <summary>
    /// Creates an InMemory DbContext — fast, no transaction support.
    /// Use for services that do NOT call BeginTransactionAsync.
    /// </summary>
    public static ApplicationDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Creates a SQLite in-memory DbContext — supports transactions.
    /// Use for services that call BeginTransactionAsync (WildCoinService, SaveNewRecipeService).
    /// The returned connection must be disposed by the caller after the test.
    /// </summary>
    public static (ApplicationDbContext Context, SqliteConnection Connection) CreateSqlite()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return (context, connection);
    }
}

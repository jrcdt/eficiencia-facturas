using InvoiceManager.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InvoiceManager.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string database = Path.Combine(Path.GetTempPath(), $"eficiencia-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Invoices"] = $"Data Source={database};Default Timeout=30" }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBusinessClock>();
            services.AddSingleton<IBusinessClock>(new FixedClock(new DateOnly(2026, 11, 9)));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { database, database + "-wal", database + "-shm" })
            if (File.Exists(path)) File.Delete(path);
    }
}

public sealed class FixedClock(DateOnly today) : IBusinessClock
{
    public DateOnly Today => today;
}

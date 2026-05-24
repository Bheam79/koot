using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Koot.Api.Data;

/// <summary>
/// Design-time factory so `dotnet ef migrations add/update` can build the
/// DbContext without spinning up the full web host (and without requiring a
/// live database connection just to scaffold a migration).
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost;Port=3306;Database=koot;User=koot;Password=koot;";

        var serverVersion = new MariaDbServerVersion(new Version(11, 4, 0));

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(connectionString, serverVersion)
            .Options;

        return new AppDbContext(options);
    }
}

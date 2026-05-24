using Microsoft.EntityFrameworkCore;

namespace Koot.Api.Data;

/// <summary>
/// Placeholder application DbContext. Entities and configuration land in KOOT-3.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}

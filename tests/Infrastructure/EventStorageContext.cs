using Microsoft.EntityFrameworkCore;

namespace EventStorage.Tests.Infrastructure;

public class EventStorageContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(TestInit.DatabaseConnectionString);
        }
        base.OnConfiguring(optionsBuilder);
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ResellManager.Infrastructure.Persistence;

public sealed class ResellManagerDbContextFactory : IDesignTimeDbContextFactory<ResellManagerDbContext>
{
    public ResellManagerDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ResellManagerDbContext>();
        optionsBuilder.UseSqlite("Data Source=resellmanager.db");

        return new ResellManagerDbContext(optionsBuilder.Options);
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Infrastructure;

/// <summary>
/// Registers infrastructure services used by the web host.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ResellManager")
            ?? throw new InvalidOperationException("Connection string 'ResellManager' was not found.");

        services.AddDbContext<ResellManagerDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<ResellManagerDbContext>()
            .AddSignInManager();

        return services;
    }
}

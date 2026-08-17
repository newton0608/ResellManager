using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;

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
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped<IClienteService, ClienteService>();
        services.AddScoped<ICategoriaService, CategoriaService>();
        services.AddScoped<IProductoService, ProductoService>();
        services.AddScoped<IProveedorService, ProveedorService>();
        services.AddScoped<ICompraService, CompraService>();
        services.AddScoped<IInventarioService, InventarioService>();
        services.AddScoped<IPedidoService, PedidoService>();
        services.AddScoped<IVentaService, VentaService>();
        services.AddScoped<IPagoService, PagoService>();
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}

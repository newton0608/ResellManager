using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;

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
        services.AddDbContext<ResellManagerDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("ResellManager")
                ?? throw new InvalidOperationException("Connection string 'ResellManager' was not found.")));

        services.AddIdentityCore<IdentityUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
            .AddEntityFrameworkStores<ResellManagerDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped<IClienteService, ClienteService>();
        services.AddScoped<ICategoriaService, CategoriaService>();
        services.AddScoped<IProductoService, ProductoService>();
        services.AddScoped<IProveedorService, ProveedorService>();
        services.AddScoped<ICompraService, CompraService>();
        services.AddScoped<IAlmacenamientoComprobantes, AlmacenamientoComprobantesLocal>();
        services.AddScoped<
            IRegistroCompraConComprobanteService,
            RegistroCompraConComprobanteService
        >();
        services.AddScoped<IInventarioService, InventarioService>();
        services.AddScoped<IPedidoService, PedidoService>();
        services.AddScoped<IVentaService, VentaService>();
        services.AddScoped<IPagoService, PagoService>();
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}

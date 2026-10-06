using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;
using ResellManager.Infrastructure.Lookup;
using ResellManager.Application.Services;

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
        services.AddScoped<IActividadClienteService, ActividadClienteService>();
        services.AddScoped<IRecepcionCompraService, RecepcionCompraService>();
        services.AddScoped<ICategoriaService, CategoriaService>();
        services.AddScoped<ProductoService>();
        services.AddScoped<IProductoService>(sp => sp.GetRequiredService<ProductoService>());
        services.AddScoped<IConsultaProductoCodigoBarras>(sp => sp.GetRequiredService<ProductoService>());
        services.AddScoped<IProductoLookupService, ProductoLookupService>();
        services.AddOptions<ProductoLookupOptions>().Bind(configuration.GetSection(ProductoLookupOptions.Seccion));
        services.AddSingleton<ProductoLookupLimites>();
        services.AddHttpClient<OpenFactsProductoLookupProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        services.AddHttpClient<UpcitemdbProductoLookupProvider>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        // El orden de registro determina el fallback, sin conocimiento de proveedores en el formulario.
        services.AddScoped<IProductoLookupProvider>(sp => sp.GetRequiredService<OpenFactsProductoLookupProvider>());
        services.AddScoped<IProductoLookupProvider>(sp => sp.GetRequiredService<UpcitemdbProductoLookupProvider>());
        services.AddHttpClient<IImagenProductoExternaService, ImagenProductoExternaService>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(DestinoImagenProductoSeguro.CrearHandler);
        services.AddScoped<ICatalogoPublicoService, CatalogoPublicoService>();
        services.AddScoped<IAlmacenamientoImagenesProducto, AlmacenamientoImagenesProductoLocal>();
        services.AddScoped<ProductoConImagenService>();
        services.AddScoped<IProductoConImagenService>(sp => sp.GetRequiredService<ProductoConImagenService>());
        services.AddScoped<IAltaProductoAsistidaService>(sp => sp.GetRequiredService<ProductoConImagenService>());
        services.AddScoped<IProveedorService, ProveedorService>();
        services.AddScoped<ICompraService, CompraService>();
        services.AddMemoryCache();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<ITipoCambioReferenciaService, TipoCambioReferenciaBanguatService>(client =>
        {
            client.Timeout = TipoCambioReferenciaBanguatService.TimeoutConsulta;
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            // Una redirección del proveedor no debe degradar HTTPS a HTTP.
            AllowAutoRedirect = false
        });
        services.AddScoped<IAlmacenamientoComprobantes, AlmacenamientoComprobantesLocal>();
        services.AddScoped<
            IRegistroCompraConComprobanteService,
            RegistroCompraConComprobanteService
        >();
        services.AddScoped<IInventarioService, InventarioService>();
        services.AddScoped<IPedidoService, PedidoService>();
        services.AddScoped<ISeleccionOperativaService, SeleccionOperativaService>();
        services.AddScoped<IRegistroPedidoConReservasService, RegistroPedidoConReservasService>();
        services.AddScoped<IVentaService, VentaService>();
        services.AddScoped<IPagoService, PagoService>();
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}

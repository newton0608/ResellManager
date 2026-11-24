using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ResellManager.Domain.Entities;

namespace ResellManager.Infrastructure.Persistence;

public sealed class ResellManagerDbContext(DbContextOptions<ResellManagerDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<ProductoImagen> ProductoImagenes => Set<ProductoImagen>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<Compra> Compras => Set<Compra>();
    public DbSet<DetalleCompra> DetallesCompra => Set<DetalleCompra>();
    public DbSet<ComprobanteCompra> ComprobantesCompra => Set<ComprobanteCompra>();
    public DbSet<UnidadInventario> UnidadesInventario => Set<UnidadInventario>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<DetallePedido> DetallesPedido => Set<DetallePedido>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetallesVenta => Set<DetalleVenta>();
    public DbSet<CobroVenta> CobrosVenta => Set<CobroVenta>();
    public DbSet<AnulacionCF> AnulacionesCF => Set<AnulacionCF>();
    public DbSet<DevolucionDineroCF> DevolucionesDineroCF => Set<DevolucionDineroCF>();
    public DbSet<RecepcionDevolucionCF> RecepcionesDevolucionCF => Set<RecepcionDevolucionCF>();
    public DbSet<GrupoVariantes> GruposVariantes => Set<GrupoVariantes>();
    public DbSet<VinculoVariante> VinculosVariantes => Set<VinculoVariante>();
    public DbSet<CambioPrecioProducto> CambiosPrecioProducto => Set<CambioPrecioProducto>();
    public DbSet<LoteEtiquetas> LotesEtiquetas => Set<LoteEtiquetas>();
    public DbSet<NotaCosto> NotasCosto => Set<NotaCosto>();
    public DbSet<CorreccionDetalleCosto> CorreccionesDetalleCosto => Set<CorreccionDetalleCosto>();
    public DbSet<MigracionCostosHistoricos> MigracionesCostosHistoricos => Set<MigracionCostosHistoricos>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<MovimientoCuenta> MovimientosCuenta => Set<MovimientoCuenta>();
    public DbSet<AperturaCartera> AperturasCartera => Set<AperturaCartera>();
    public DbSet<ComandoFinanciero> ComandosFinancieros => Set<ComandoFinanciero>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => SaveChangesAsync(true, cancellationToken);
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidarHistorial();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    public override int SaveChanges() => SaveChanges(true);
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidarHistorial();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void ValidarHistorial()
    {
        foreach (var e in ChangeTracker.Entries())
        {
            if (e.Entity is MovimientoCuenta or ComandoFinanciero or CobroVenta or DevolucionDineroCF or RecepcionDevolucionCF or CambioPrecioProducto or NotaCosto or CorreccionDetalleCosto or MigracionCostosHistoricos
                && e.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("El historial financiero es inmutable.");
            if (e.Entity is AperturaCartera && e.State is EntityState.Modified or EntityState.Deleted
                && e.OriginalValues.GetValue<DateTime?>(nameof(AperturaCartera.ConfirmadaUtc)) is not null)
                throw new InvalidOperationException("La apertura confirmada no puede modificarse.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ResellManagerDbContext).Assembly);
    }
}

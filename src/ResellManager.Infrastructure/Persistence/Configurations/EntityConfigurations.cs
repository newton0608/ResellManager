using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResellManager.Domain.Entities;

namespace ResellManager.Infrastructure.Persistence.Configurations;

internal sealed class CategoriaConfiguration : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> b)
    {
        b.ToTable("Categorias"); b.HasKey(x => x.Id);
        b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
        b.Property(x => x.Observaciones).HasMaxLength(500);
    }
}

internal sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("Clientes"); b.HasKey(x => x.Id);
        b.Property(x => x.Nombres).IsRequired().HasMaxLength(100);
        b.Property(x => x.Apellidos).HasMaxLength(100);
        b.Property(x => x.Telefono).IsRequired().HasMaxLength(30);
        b.Property(x => x.Direccion).HasMaxLength(250);
        b.Property(x => x.Observaciones).HasMaxLength(500);
    }
}

internal sealed class ProveedorConfiguration : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> b)
    {
        b.ToTable("Proveedores"); b.HasKey(x => x.Id);
        b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
        b.Property(x => x.Telefono).HasMaxLength(30);
        b.Property(x => x.CodigoPais).HasMaxLength(10);
        b.Property(x => x.Descripcion).HasMaxLength(500);
    }
}

internal sealed class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> b)
    {
        b.ToTable("Productos"); b.HasKey(x => x.Id);
        b.Property(x => x.CodigoInterno).IsRequired().HasMaxLength(50);
        b.HasIndex(x => x.CodigoInterno).IsUnique();
        b.Property(x => x.CodigoBarras).HasMaxLength(100);
        b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
        b.Property(x => x.Descripcion).HasMaxLength(500);
        b.Property(x => x.Marca).HasMaxLength(100);
        b.Property(x => x.Modelo).HasMaxLength(100);
        b.Property(x => x.Color).HasMaxLength(50);
        b.Property(x => x.Talla).HasMaxLength(30);
        b.HasOne(x => x.Categoria).WithMany(x => x.Productos).HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CompraConfiguration : IEntityTypeConfiguration<Compra>
{
    public void Configure(EntityTypeBuilder<Compra> b)
    {
        b.ToTable("Compras"); b.HasKey(x => x.Id);
        b.Property(x => x.CodigoInterno).IsRequired().HasMaxLength(50);
        b.HasIndex(x => x.CodigoInterno).IsUnique();
        b.Property(x => x.Origen).IsRequired().HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Total).HasColumnType("decimal(10,2)");
        b.Property(x => x.Observaciones).HasMaxLength(500);
        b.HasOne(x => x.Proveedor).WithMany(x => x.Compras).HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DetalleCompraConfiguration : IEntityTypeConfiguration<DetalleCompra>
{
    public void Configure(EntityTypeBuilder<DetalleCompra> b)
    {
        b.ToTable("DetallesCompra"); b.HasKey(x => x.Id);
        b.Property(x => x.CostoUnitario).HasColumnType("decimal(10,2)");
        b.HasOne(x => x.Compra).WithMany(x => x.Detalles).HasForeignKey(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Producto).WithMany(x => x.DetallesCompra).HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ComprobanteCompraConfiguration : IEntityTypeConfiguration<ComprobanteCompra>
{
    public void Configure(EntityTypeBuilder<ComprobanteCompra> b)
    {
        b.ToTable("ComprobantesCompra"); b.HasKey(x => x.Id);
        b.Property(x => x.NumeroDocumento).HasMaxLength(100);
        b.Property(x => x.RutaDocumento).IsRequired().HasMaxLength(500);
        b.Property(x => x.Observaciones).HasMaxLength(500);
        b.HasIndex(x => x.CompraId).IsUnique();
        b.HasOne(x => x.Compra).WithOne(x => x.Comprobante).HasForeignKey<ComprobanteCompra>(x => x.CompraId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UnidadInventarioConfiguration : IEntityTypeConfiguration<UnidadInventario>
{
    public void Configure(EntityTypeBuilder<UnidadInventario> b)
    {
        b.ToTable("UnidadesInventario"); b.HasKey(x => x.Id);
        b.Property(x => x.CodigoInterno).IsRequired().HasMaxLength(50);
        b.HasIndex(x => x.CodigoInterno).IsUnique();
        b.Property(x => x.Estado).IsRequired().HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Costo).HasColumnType("decimal(10,2)");
        b.Property(x => x.PrecioLista).HasColumnType("decimal(10,2)");
        b.HasOne(x => x.Producto).WithMany(x => x.UnidadesInventario).HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.DetalleCompra).WithMany(x => x.UnidadesInventario).HasForeignKey(x => x.DetalleCompraId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> b)
    {
        b.ToTable("Pedidos"); b.HasKey(x => x.Id);
        b.Property(x => x.CodigoInterno).IsRequired().HasMaxLength(50);
        b.HasIndex(x => x.CodigoInterno).IsUnique();
        b.Property(x => x.TipoPedido).IsRequired().HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Estado).IsRequired().HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Observaciones).HasMaxLength(500);
        b.HasOne(x => x.Cliente).WithMany(x => x.Pedidos).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DetallePedidoConfiguration : IEntityTypeConfiguration<DetallePedido>
{
    public void Configure(EntityTypeBuilder<DetallePedido> b)
    {
        b.ToTable("DetallesPedido"); b.HasKey(x => x.Id);
        b.Property(x => x.PrecioUnitario).HasColumnType("decimal(10,2)");
        b.Property(x => x.Observaciones).HasMaxLength(500);
        b.HasOne(x => x.Pedido).WithMany(x => x.Detalles).HasForeignKey(x => x.PedidoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Producto).WithMany(x => x.DetallesPedido).HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VentaConfiguration : IEntityTypeConfiguration<Venta>
{
    public void Configure(EntityTypeBuilder<Venta> b)
    {
        b.ToTable("Ventas"); b.HasKey(x => x.Id);
        b.Property(x => x.CodigoInterno).IsRequired().HasMaxLength(50);
        b.HasIndex(x => x.CodigoInterno).IsUnique();
        b.HasIndex(x => x.PedidoId).IsUnique();
        b.Property(x => x.Estado).IsRequired().HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Observaciones).HasMaxLength(500);
        b.HasOne(x => x.Pedido).WithOne(x => x.Venta).HasForeignKey<Venta>(x => x.PedidoId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DetalleVentaConfiguration : IEntityTypeConfiguration<DetalleVenta>
{
    public void Configure(EntityTypeBuilder<DetalleVenta> b)
    {
        b.ToTable("DetallesVenta");
        b.HasKey(x => x.Id);
        b.Property(x => x.PrecioFinal).HasColumnType("decimal(10,2)");
        b.Property(x => x.CostoUnitario).HasColumnType("decimal(10,2)");
        b.Property(x => x.Observaciones).HasMaxLength(500);
        b.HasIndex(x => x.UnidadInventarioId);
        b.HasOne(x => x.Venta)
            .WithMany(x => x.Detalles)
            .HasForeignKey(x => x.VentaId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.UnidadInventario)
            .WithMany(x => x.DetallesVenta)
            .HasForeignKey(x => x.UnidadInventarioId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Producto)
            .WithMany()
            .HasForeignKey(x => x.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PagoConfiguration : IEntityTypeConfiguration<Pago>
{
    public void Configure(EntityTypeBuilder<Pago> b)
    {
        b.ToTable("Pagos"); b.HasKey(x => x.Id);
        b.Property(x => x.Monto).HasColumnType("decimal(10,2)");
        b.Property(x => x.MetodoPago).IsRequired().HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Referencia).HasMaxLength(150);
        b.Property(x => x.Observaciones).HasMaxLength(500);
        b.HasOne(x => x.Cliente).WithMany(x => x.Pagos).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
    }
}


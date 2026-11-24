using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResellManager.Domain.Entities;
namespace ResellManager.Infrastructure.Persistence.Configurations;
internal sealed class CobroVentaConfiguration : IEntityTypeConfiguration<CobroVenta>
{
    public void Configure(EntityTypeBuilder<CobroVenta> b)
    {
        b.HasKey(x=>x.Id); b.Property(x=>x.Aplicado).HasColumnType("decimal(10,2)"); b.Property(x=>x.Entregado).HasColumnType("decimal(10,2)");
        b.HasOne(x=>x.Venta).WithMany().HasForeignKey(x=>x.VentaId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class AnulacionCFConfiguration : IEntityTypeConfiguration<AnulacionCF>
{
    public void Configure(EntityTypeBuilder<AnulacionCF> b)
    {
        b.HasKey(x=>x.Id); b.HasOne(x=>x.Venta).WithMany().HasForeignKey(x=>x.VentaId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x=>x.VentaId).IsUnique().HasFilter("Estado IN (1,2)");
    }
}
internal sealed class DevolucionDineroCFConfiguration : IEntityTypeConfiguration<DevolucionDineroCF>
{
    public void Configure(EntityTypeBuilder<DevolucionDineroCF> b)
    {
        b.HasKey(x=>x.Id); b.Property(x=>x.Importe).HasColumnType("decimal(10,2)");
        b.HasOne(x=>x.Anulacion).WithMany(x=>x.Reembolsos).HasForeignKey(x=>x.AnulacionId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class RecepcionDevolucionCFConfiguration : IEntityTypeConfiguration<RecepcionDevolucionCF>
{
    public void Configure(EntityTypeBuilder<RecepcionDevolucionCF> b)
    {
        b.HasKey(x=>x.Id); b.HasIndex(x=>new{x.AnulacionId,x.UnidadId}).IsUnique();
        b.HasOne(x=>x.Anulacion).WithMany(x=>x.Recepciones).HasForeignKey(x=>x.AnulacionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x=>x.Unidad).WithMany().HasForeignKey(x=>x.UnidadId).OnDelete(DeleteBehavior.Restrict);
    }
}

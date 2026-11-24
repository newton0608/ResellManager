using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResellManager.Domain.Entities;
namespace ResellManager.Infrastructure.Persistence.Configurations;
internal sealed class NotaCostoConfiguration:IEntityTypeConfiguration<NotaCosto>
{
    public void Configure(EntityTypeBuilder<NotaCosto> b)
    {
        b.HasKey(x=>x.Id);b.Property(x=>x.Anterior).HasColumnType("decimal(10,2)");b.Property(x=>x.Nuevo).HasColumnType("decimal(10,2)");
        b.HasOne(x=>x.Unidad).WithMany(x=>x.NotasCosto).HasForeignKey(x=>x.UnidadId).OnDelete(DeleteBehavior.Restrict);b.HasIndex(x=>new{x.UnidadId,x.RegistradoUtc});
    }
}
internal sealed class CorreccionDetalleCostoConfiguration:IEntityTypeConfiguration<CorreccionDetalleCosto>
{
    public void Configure(EntityTypeBuilder<CorreccionDetalleCosto> b)
    {
        b.HasKey(x=>new{x.NotaId,x.DetalleVentaId});b.HasOne(x=>x.Nota).WithMany(x=>x.Detalles).HasForeignKey(x=>x.NotaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x=>x.DetalleVenta).WithMany().HasForeignKey(x=>x.DetalleVentaId).OnDelete(DeleteBehavior.Restrict);
    }
}

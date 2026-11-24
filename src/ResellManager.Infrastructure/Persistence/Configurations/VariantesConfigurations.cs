using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResellManager.Domain.Entities;
namespace ResellManager.Infrastructure.Persistence.Configurations;
internal sealed class GrupoVariantesConfiguration:IEntityTypeConfiguration<GrupoVariantes>
{
    public void Configure(EntityTypeBuilder<GrupoVariantes> b){b.HasKey(x=>x.Id);b.Property(x=>x.Nombre).HasMaxLength(150);}
}
internal sealed class VinculoVarianteConfiguration:IEntityTypeConfiguration<VinculoVariante>
{
    public void Configure(EntityTypeBuilder<VinculoVariante> b)
    {
        b.HasKey(x=>x.ProductoId);b.Property(x=>x.Firma).HasMaxLength(64);b.HasIndex(x=>new{x.GrupoId,x.Firma}).IsUnique();
        b.HasOne(x=>x.Producto).WithOne().HasForeignKey<VinculoVariante>(x=>x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x=>x.Grupo).WithMany(x=>x.Variantes).HasForeignKey(x=>x.GrupoId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class CambioPrecioProductoConfiguration:IEntityTypeConfiguration<CambioPrecioProducto>
{
    public void Configure(EntityTypeBuilder<CambioPrecioProducto> b)
    {
        b.HasKey(x=>x.Id);b.HasOne(x=>x.Producto).WithMany().HasForeignKey(x=>x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x=>x.Anterior).HasColumnType("decimal(10,2)");b.Property(x=>x.Nuevo).HasColumnType("decimal(10,2)");
    }
}

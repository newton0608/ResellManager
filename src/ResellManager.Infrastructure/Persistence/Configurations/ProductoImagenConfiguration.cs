using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResellManager.Domain.Entities;

namespace ResellManager.Infrastructure.Persistence.Configurations;

internal sealed class ProductoImagenConfiguration : IEntityTypeConfiguration<ProductoImagen>
{
    public void Configure(EntityTypeBuilder<ProductoImagen> b)
    {
        b.ToTable("ProductoImagenes", table =>
            table.HasCheckConstraint("CK_ProductoImagenes_Orden", "Orden >= 0 AND Orden < 8"));
        b.HasKey(x => x.Id);
        b.Property(x => x.RutaRelativa).IsRequired().HasMaxLength(250);
        b.HasIndex(x => new { x.ProductoId, x.Orden });
        b.HasIndex(x => new { x.ProductoId, x.RutaRelativa }).IsUnique();
        b.HasOne(x => x.Producto).WithMany(x => x.Imagenes)
            .HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Cascade);
    }
}

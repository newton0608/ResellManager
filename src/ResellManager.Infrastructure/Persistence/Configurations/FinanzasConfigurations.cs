using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResellManager.Domain.Entities;

namespace ResellManager.Infrastructure.Persistence.Configurations;

internal sealed class MovimientoCuentaConfiguration : IEntityTypeConfiguration<MovimientoCuenta>
{
    public void Configure(EntityTypeBuilder<MovimientoCuenta> b)
    {
        b.HasKey(x => x.Id);
        b.Property(x => x.Importe).HasColumnType("decimal(10,2)");
        b.Property(x => x.UsuarioId).HasMaxLength(450);
        b.Property(x => x.Motivo).HasMaxLength(1000);
        b.Property(x => x.Referencia).HasMaxLength(200);
        b.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Revierte).WithMany().HasForeignKey(x => x.RevierteId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.RevierteId).IsUnique();
        b.HasIndex(x => new { x.ClienteId, x.FechaEfectiva });
        b.HasIndex(x => new { x.ClienteId, x.Tipo }).IsUnique().HasFilter("Tipo = 1");
    }
}

internal sealed class AperturaCarteraConfiguration : IEntityTypeConfiguration<AperturaCartera>
{
    public void Configure(EntityTypeBuilder<AperturaCartera> b)
    {
        b.ToTable("AperturasCartera", t => t.HasCheckConstraint("CK_Apertura_Unica", "Id = 1"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.Property(x => x.ConfirmadaUtc).IsConcurrencyToken();
    }
}

internal sealed class ComandoFinancieroConfiguration : IEntityTypeConfiguration<ComandoFinanciero>
{
    public void Configure(EntityTypeBuilder<ComandoFinanciero> b)
    {
        b.HasKey(x => x.Clave);
        b.Property(x => x.Clave).HasMaxLength(150);
        b.Property(x => x.Huella).HasMaxLength(64);
        b.Property(x => x.UsuarioId).HasMaxLength(450);
    }
}

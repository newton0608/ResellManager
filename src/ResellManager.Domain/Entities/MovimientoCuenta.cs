namespace ResellManager.Domain.Entities;

public enum TipoMovimientoCuenta { Apertura = 1, Ajuste = 2, Reversion = 3 }

public class MovimientoCuenta
{
    public Guid Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    public TipoMovimientoCuenta Tipo { get; set; }
    public decimal Importe { get; set; }
    public DateOnly FechaEfectiva { get; set; }
    public DateTime RegistradoUtc { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
    public Guid? RevierteId { get; set; }
    public MovimientoCuenta? Revierte { get; set; }
    public string? Referencia { get; set; }
}

public class AperturaCartera
{
    // Una sola apertura para todo el negocio. El borrador puede revisarse hasta confirmar.
    public int Id { get; set; } = 1;
    public Guid Version { get; set; }
    public string FilasJson { get; set; } = "[]";
    public string UsuarioId { get; set; } = string.Empty;
    public DateTime? ConfirmadaUtc { get; set; }
    public DateOnly? Corte { get; set; }
    public decimal Total { get; set; }
}

public class ComandoFinanciero
{
    public string Clave { get; set; } = string.Empty;
    public string Huella { get; set; } = string.Empty;
    public string Resultado { get; set; } = string.Empty;
    public string UsuarioId { get; set; } = string.Empty;
    public DateTime RegistradoUtc { get; set; }
}

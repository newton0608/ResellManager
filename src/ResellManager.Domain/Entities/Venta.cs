using ResellManager.Domain.Enums;

namespace ResellManager.Domain.Entities;

public class Venta
{
    public int Id { get; set; }
    public string CodigoInterno { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public EstadoVenta Estado { get; set; }
    public string? Observaciones { get; set; }
    public string? NombreOcasional { get; set; }
    public string? ApellidoOcasional { get; set; }
    public string? UsuarioId { get; set; }
    public DateTime? RegistradaUtc { get; set; }
    public DateTime? CanceladaUtc { get; set; }
    public string? CanceladaPor { get; set; }
    public int PedidoId { get; set; }

    public Pedido Pedido { get; set; } = null!;
    public ICollection<DetalleVenta> Detalles { get; set; } = [];

    public decimal CalcularTotal()
    {
        return Detalles.Sum(detalle => detalle.PrecioFinal);
    }
}

namespace ResellManager.Domain.Entities;

public class Cliente
{
    public int Id { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string? Apellidos { get; set; }
    public string Telefono { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Observaciones { get; set; }

    public ICollection<Pedido> Pedidos { get; set; } = [];
    public ICollection<Pago> Pagos { get; set; } = [];

    public string NombreCompleto()
    {
        return string.IsNullOrWhiteSpace(Apellidos)
            ? Nombres
            : $"{Nombres} {Apellidos}";
    }
}

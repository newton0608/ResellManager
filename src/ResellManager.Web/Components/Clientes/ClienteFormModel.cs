using System.ComponentModel.DataAnnotations;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Components.Clientes;

public sealed class ClienteFormModel
{
    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    public string Nombres { get; set; } = string.Empty;

    public string? Apellidos { get; set; }

    [Required(ErrorMessage = "El teléfono es obligatorio.")]
    public string Telefono { get; set; } = string.Empty;

    public string? Direccion { get; set; }

    public string? Observaciones { get; set; }

    public ClienteInput ToInput() =>
        new(Nombres, Apellidos, Telefono, Direccion, Observaciones);

    public static ClienteFormModel FromDto(ClienteDto cliente) =>
        new()
        {
            Nombres = cliente.Nombres,
            Apellidos = cliente.Apellidos,
            Telefono = cliente.Telefono,
            Direccion = cliente.Direccion,
            Observaciones = cliente.Observaciones,
        };
}

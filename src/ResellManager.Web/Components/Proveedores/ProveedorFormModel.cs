using System.ComponentModel.DataAnnotations;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Components.Proveedores;

public sealed class ProveedorFormModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre permite hasta 150 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(30, ErrorMessage = "El teléfono permite hasta 30 caracteres.")]
    public string? Telefono { get; set; }

    [StringLength(10, ErrorMessage = "El código de país permite hasta 10 caracteres.")]
    public string? CodigoPais { get; set; }

    [StringLength(500, ErrorMessage = "La descripción permite hasta 500 caracteres.")]
    public string? Descripcion { get; set; }

    public ProveedorInput ToInput() => new(Nombre, Telefono, CodigoPais, Descripcion);
}

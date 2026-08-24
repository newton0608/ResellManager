using System.ComponentModel.DataAnnotations;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Components.Categorias;

public sealed class CategoriaFormModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    public string Nombre { get; set; } = string.Empty;

    public string? Observaciones { get; set; }

    public CategoriaInput ToInput() => new(Nombre, Observaciones);

    public static CategoriaFormModel FromDto(CategoriaDto categoria) =>
        new()
        {
            Nombre = categoria.Nombre,
            Observaciones = categoria.Observaciones,
        };
}

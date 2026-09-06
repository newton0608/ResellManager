using System.ComponentModel.DataAnnotations;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Components.Productos;

public sealed class ProductoFormModel : IValidatableObject
{
    [Required(ErrorMessage = "El código de producto es obligatorio.")]
    public string CodigoInterno { get; set; } = string.Empty;

    public string? CodigoBarras { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public string? Marca { get; set; }

    public string? Modelo { get; set; }

    public string? Color { get; set; }

    public string? Talla { get; set; }

    public decimal PrecioSugerido { get; set; }

    public int CategoriaId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PrecioSugerido < 0)
        {
            yield return new ValidationResult(
                "El precio sugerido no puede ser negativo.",
                [nameof(PrecioSugerido)]);
        }

        if (CategoriaId <= 0)
        {
            yield return new ValidationResult(
                "Selecciona una categoría.",
                [nameof(CategoriaId)]);
        }
    }

    public ProductoInput ToInput() =>
        new(
            CodigoInterno,
            CodigoBarras,
            Nombre,
            Descripcion,
            Marca,
            Modelo,
            Color,
            Talla,
            PrecioSugerido,
            CategoriaId);

    public static ProductoFormModel FromDto(ProductoDto producto) =>
        new()
        {
            CodigoInterno = producto.CodigoInterno,
            CodigoBarras = producto.CodigoBarras,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            Marca = producto.Marca,
            Modelo = producto.Modelo,
            Color = producto.Color,
            Talla = producto.Talla,
            PrecioSugerido = producto.PrecioSugerido,
            CategoriaId = producto.CategoriaId,
        };
}

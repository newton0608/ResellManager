using System.ComponentModel.DataAnnotations;
using ResellManager.Application.DTOs;

namespace ResellManager.Web.Components.Productos;

public sealed class ProductoFormModel : IValidatableObject
{
    public string? CodigoBarras { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public string? Marca { get; set; }

    public string? Modelo { get; set; }

    public string? Color { get; set; }

    public string? Talla { get; set; }

    public decimal? ContenidoMl { get; set; }

    public decimal? PesoGramos { get; set; }

    [MaxLength(100, ErrorMessage = "La presentación no puede superar los 100 caracteres.")]
    public string? Presentacion { get; set; }

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

        if (ContenidoMl is <= 0)
        {
            yield return new ValidationResult(
                "El contenido en ml debe ser mayor que cero.",
                [nameof(ContenidoMl)]);
        }

        if (PesoGramos is <= 0)
        {
            yield return new ValidationResult(
                "El peso en gramos debe ser mayor que cero.",
                [nameof(PesoGramos)]);
        }

        if (ContenidoMl.HasValue && PesoGramos.HasValue)
        {
            yield return new ValidationResult(
                "Un producto no puede tener contenido en ml y peso en gramos simultáneamente.",
                [nameof(ContenidoMl), nameof(PesoGramos)]);
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
            CodigoBarras,
            Nombre,
            Descripcion,
            Marca,
            Modelo,
            Color,
            Talla,
            PrecioSugerido,
            CategoriaId,
            ContenidoMl,
            PesoGramos,
            Presentacion);

    public static ProductoFormModel FromDto(ProductoDto producto) =>
        new()
        {
            CodigoBarras = producto.CodigoBarras,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            Marca = producto.Marca,
            Modelo = producto.Modelo,
            Color = producto.Color,
            Talla = producto.Talla,
            ContenidoMl = producto.ContenidoMl,
            PesoGramos = producto.PesoGramos,
            Presentacion = producto.Presentacion,
            PrecioSugerido = producto.PrecioSugerido,
            CategoriaId = producto.CategoriaId,
        };
}

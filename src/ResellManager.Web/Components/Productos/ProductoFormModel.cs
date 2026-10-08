using Microsoft.AspNetCore.Components.Forms;
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

    // Valores y unidades usados solo en el formulario.
    public decimal? Volumen { get; set; }
    public UnidadVolumen VolumenUnidad { get; set; } = UnidadVolumen.Ml;
    public decimal? Peso { get; set; }
    public UnidadPeso PesoUnidad { get; set; } = UnidadPeso.G;

    public decimal? ContenidoMl
    {
        get => ProductoMedidasConversion.AContenidoMl(Volumen, VolumenUnidad);
        set => (Volumen, VolumenUnidad) = ProductoMedidasConversion.VolumenParaEditar(value);
    }

    public decimal? PesoGramos
    {
        get => ProductoMedidasConversion.APesoGramos(Peso, PesoUnidad);
        set => (Peso, PesoUnidad) = ProductoMedidasConversion.PesoParaEditar(value);
    }

    [MaxLength(100, ErrorMessage = "La presentación no puede superar los 100 caracteres.")]
    public string? Presentacion { get; set; }

    public decimal PrecioSugerido { get; set; }
    public int CategoriaId { get; set; }
    public string? ImagenPrincipalRuta { get; set; }
    public IBrowserFile? ImagenArchivo { get; set; }
    public bool EliminarImagenPrincipal { get; set; }
    public string? ImagenExternaUrl { get; set; }
    // Bytes ya leídos para preservar una selección manual aunque se reabra el selector y luego se deshaga.
    public byte[]? ImagenContenido { get; set; }

    public List<ProductoImagenFormModel> Galeria { get; private set; } = [];
    public bool GaleriaCargada { get; private set; }

    public void CargarGaleria(IReadOnlyList<ImagenProductoDto> imagenes)
    {
        Galeria = imagenes.OrderBy(x => x.Orden).Select(x => new ProductoImagenFormModel
        {
            ImagenId = x.Id,
            EsPortada = x.EsPortada,
        }).ToList();
        GaleriaCargada = true;
    }

    public GaleriaProductoEdicion ToGaleriaEdicion()
    {
        var nuevas = 0;
        var imagenes = Galeria.Select(x => x.ImagenId.HasValue
            ? new ImagenProductoEdicion(x.ImagenId)
            : new ImagenProductoEdicion(NuevaImagenIndice: nuevas++)).ToArray();
        return new GaleriaProductoEdicion(imagenes, Math.Max(0, Galeria.FindIndex(x => x.EsPortada)));
    }

    public ProductoFormModel CrearInstantanea()
    {
        var copia = (ProductoFormModel)MemberwiseClone();
        copia.Galeria = Galeria.Select(x => x.CrearInstantanea()).ToList();
        return copia;
    }

    public void Restaurar(ProductoFormModel anterior)
    {
        CodigoBarras = anterior.CodigoBarras;
        Nombre = anterior.Nombre;
        Descripcion = anterior.Descripcion;
        Marca = anterior.Marca;
        Modelo = anterior.Modelo;
        Color = anterior.Color;
        Talla = anterior.Talla;
        Volumen = anterior.Volumen;
        VolumenUnidad = anterior.VolumenUnidad;
        Peso = anterior.Peso;
        PesoUnidad = anterior.PesoUnidad;
        Presentacion = anterior.Presentacion;
        PrecioSugerido = anterior.PrecioSugerido;
        CategoriaId = anterior.CategoriaId;
        ImagenPrincipalRuta = anterior.ImagenPrincipalRuta;
        ImagenArchivo = anterior.ImagenArchivo;
        ImagenContenido = anterior.ImagenContenido;
        ImagenExternaUrl = anterior.ImagenExternaUrl;
        EliminarImagenPrincipal = anterior.EliminarImagenPrincipal;
        Galeria = anterior.Galeria.Select(x => x.CrearInstantanea()).ToList();
        GaleriaCargada = anterior.GaleriaCargada;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Galeria.Count > 8)
            yield return new ValidationResult("Un producto admite hasta 8 fotografías en total.", [nameof(Galeria)]);
        if (Galeria.Count > 0 && Galeria.Count(x => x.EsPortada) != 1)
            yield return new ValidationResult("Selecciona exactamente una portada para la galería.", [nameof(Galeria)]);
        if (PrecioSugerido < 0)
        {
            yield return new ValidationResult(
                "El precio sugerido no puede ser negativo.",
                [nameof(PrecioSugerido)]);
        }

        if (Volumen.HasValue && (Volumen <= 0 || ContenidoMl <= 0))
        {
            yield return new ValidationResult(
                "El contenido en ml debe ser mayor que cero.",
                [nameof(Volumen)]);
        }

        if (Peso.HasValue && (Peso <= 0 || PesoGramos <= 0))
        {
            yield return new ValidationResult(
                "El peso en gramos debe ser mayor que cero.",
                [nameof(Peso)]);
        }

        if (Volumen.HasValue && Peso.HasValue)
        {
            yield return new ValidationResult(
                "Un producto no puede tener contenido en ml y peso en gramos simultáneamente.",
                [nameof(Volumen), nameof(Peso)]);
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
            ImagenPrincipalRuta = producto.ImagenPrincipalRuta,
        };
}

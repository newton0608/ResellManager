using Microsoft.AspNetCore.Components.Forms;

namespace ResellManager.Web.Components.Productos;

// Estado de edición temporal; los archivos se persisten solo al guardar el producto.
public sealed class ProductoImagenFormModel
{
    public Guid? ImagenId { get; init; }
    public IBrowserFile? Archivo { get; init; }
    public byte[]? Contenido { get; init; }
    public string? VistaPrevia { get; init; }
    public bool EsPortada { get; set; }

    public ProductoImagenFormModel CrearInstantanea() => (ProductoImagenFormModel)MemberwiseClone();
}

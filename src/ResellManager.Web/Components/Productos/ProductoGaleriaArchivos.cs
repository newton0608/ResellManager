namespace ResellManager.Web.Components.Productos;

// Mantiene las lecturas vivas durante el guardado y libera todas al terminar.
public sealed class ProductoGaleriaArchivos : IDisposable
{
    public IReadOnlyList<Stream> Imagenes { get; }

    public ProductoGaleriaArchivos(ProductoFormModel modelo)
    {
        Imagenes = modelo.Galeria.Where(x => !x.ImagenId.HasValue)
            .Select(x => (Stream)new MemoryStream(x.Contenido ?? [], writable: false)).ToArray();
    }

    public void Dispose()
    {
        foreach (var imagen in Imagenes) imagen.Dispose();
    }
}

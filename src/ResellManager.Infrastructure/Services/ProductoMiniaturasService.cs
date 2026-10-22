using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Infrastructure.Services;

// Sólo deriva archivos de referencias persistidas; nunca modifica productos ni imágenes grandes.
public sealed class ProductoMiniaturasService(ResellManagerDbContext db,
    IAlmacenamientoImagenesProducto almacenamiento, ILogger<ProductoMiniaturasService> logger)
{
    public async Task<ResultadoMiniaturas> GenerarPendientesAsync(CancellationToken ct = default)
    {
        var creadas = 0;
        var existentes = 0;
        var fallidas = 0;
        var rutas = db.Productos.AsNoTracking().Where(x => x.ImagenPrincipalRuta != null)
            .Select(x => x.ImagenPrincipalRuta!)
            .Union(db.ProductoImagenes.AsNoTracking().Select(x => x.RutaRelativa))
            .OrderBy(x => x);
        await foreach (var ruta in rutas.AsAsyncEnumerable().WithCancellation(ct))
        {
            var resultado = await almacenamiento.GenerarMiniaturaAsync(ruta, ct);
            if (!resultado.IsSuccess)
            {
                fallidas++;
                logger.LogWarning("No fue posible generar la miniatura de {Referencia}: {Error}", ruta, resultado.ErrorMessage);
            }
            else if (resultado.Value) creadas++;
            else existentes++;
        }
        return new(creadas, existentes, fallidas);
    }
}

public sealed record ResultadoMiniaturas(int Creadas, int Existentes, int Fallidas);

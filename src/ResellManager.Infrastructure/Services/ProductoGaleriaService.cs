using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;

namespace ResellManager.Infrastructure.Services;

public sealed partial class ProductoConImagenService
{
    public const int MaximoImagenes = 8;

    public async Task<ServiceResult<IReadOnlyList<ImagenProductoDto>>> ObtenerGaleriaAsync(int id, CancellationToken ct = default)
    {
        var producto = await db.Productos.AsNoTracking().Include(x => x.Imagenes).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (producto is null)
            return ServiceResult<IReadOnlyList<ImagenProductoDto>>.Failure("Producto no encontrado.");
        IReadOnlyList<ImagenProductoDto> resultado = producto.Imagenes.OrderBy(x => x.Orden).ThenBy(x => x.Id)
            .Select(x => new ImagenProductoDto(x.Id, x.Orden, x.RutaRelativa == producto.ImagenPrincipalRuta)).ToList();
        // Compatibilidad con referencias heredadas: la migración asigna sus identificadores definitivos.
        if (resultado.Count == 0 && !string.IsNullOrWhiteSpace(producto.ImagenPrincipalRuta))
            resultado = [new ImagenProductoDto(Guid.Empty, 0, true)];
        return ServiceResult<IReadOnlyList<ImagenProductoDto>>.Ok(resultado);
    }

    public async Task<ServiceResult<ImagenProductoLectura>> AbrirImagenAsync(int id, Guid imagenId, CancellationToken ct = default)
    {
        var ruta = imagenId == Guid.Empty
            ? await db.Productos.AsNoTracking().Where(x => x.Id == id).Select(x => x.ImagenPrincipalRuta).SingleOrDefaultAsync(ct)
            : await db.ProductoImagenes.AsNoTracking().Where(x => x.ProductoId == id && x.Id == imagenId)
                .Select(x => x.RutaRelativa).SingleOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(ruta) || !ruta.StartsWith($"productos/{id}/", StringComparison.Ordinal)
            ? ServiceResult<ImagenProductoLectura>.Failure("Imagen no disponible.")
            : await almacenamiento.AbrirLecturaAsync(ruta, ct);
    }

    public async Task<ServiceResult<ProductoDto>> CrearGaleriaAsync(
        ProductoInput input, IReadOnlyList<Stream> imagenes, int portadaIndice = 0, CancellationToken ct = default, bool validarCodigoBarras = false)
    {
        var error = ValidarCantidadYPortada(imagenes.Count, portadaIndice);
        if (error is not null) return ServiceResult<ProductoDto>.Failure(error);
        if (validarCodigoBarras)
        {
            var errorCodigo = await ErrorCodigoRegistradoAsync(input, ct);
            if (errorCodigo is not null) return ServiceResult<ProductoDto>.Failure(errorCodigo);
        }
        if (imagenes.Count == 0)
            return validarCodigoBarras ? await CrearSinImagenAsistidoAsync(input, ct) : await productos.CrearAsync(input, ct);
        var preparadas = new List<ImagenProductoPreparada>();
        var guardadas = new List<ImagenProductoGuardada>();
        int? productoId = null;
        var persistida = false;
        try
        {
            var preparacion = await PrepararGaleriaAsync(imagenes, preparadas, ct);
            if (preparacion is not null) return ServiceResult<ProductoDto>.Failure(preparacion);
            await using var transaccion = await db.Database.BeginTransactionAsync(ct);
            if (validarCodigoBarras)
            {
                var errorCodigo = await ErrorCodigoRegistradoAsync(input, ct);
                if (errorCodigo is not null) return ServiceResult<ProductoDto>.Failure(errorCodigo);
            }
            var creacion = await productos.CrearAsync(input, ct);
            if (!creacion.IsSuccess || creacion.Value is null) return creacion;
            productoId = creacion.Value.Id;
            var confirmacion = await ConfirmarGaleriaAsync(preparadas, guardadas, productoId.Value, ct);
            if (confirmacion is not null) return ServiceResult<ProductoDto>.Failure(confirmacion);

            var producto = await db.Productos.FindAsync([productoId.Value], ct);
            for (var i = 0; i < guardadas.Count; i++)
                db.ProductoImagenes.Add(new ProductoImagen { ProductoId = productoId.Value, RutaRelativa = guardadas[i].RutaRelativa, Orden = i });
            producto!.ImagenPrincipalRuta = guardadas[portadaIndice].RutaRelativa;
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            persistida = true;
            return await productos.ObtenerPorIdAsync(productoId.Value, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible crear el producto con su galería.");
            db.ChangeTracker.Clear();
            var recuperado = productoId.HasValue ? await RecuperarGaleriaCreadaAsync(productoId.Value, guardadas) : null;
            if (recuperado is not null)
            {
                persistida = true;
                return ServiceResult<ProductoDto>.Ok(recuperado);
            }
            return ServiceResult<ProductoDto>.Failure("No fue posible guardar el producto con sus imágenes.");
        }
        finally
        {
            if (!persistida) db.ChangeTracker.Clear();
            await LimpiarGaleriaAsync(preparadas, guardadas, persistida);
        }
    }

    public async Task<ServiceResult<ProductoDto>> EditarGaleriaAsync(
        int id, ProductoInput input, GaleriaProductoEdicion galeria, IReadOnlyList<Stream> nuevas, CancellationToken ct = default)
    {
        var error = ValidarEdicion(galeria, nuevas.Count);
        if (error is not null) return ServiceResult<ProductoDto>.Failure(error);
        var preparadas = new List<ImagenProductoPreparada>();
        var guardadas = new List<ImagenProductoGuardada>();
        var eliminadas = new List<string>();
        ProductoDto? esperado = null;
        Guid[]? idsEsperados = null;
        var persistida = false;
        try
        {
            var preparacion = await PrepararGaleriaAsync(nuevas, preparadas, ct);
            if (preparacion is not null) return ServiceResult<ProductoDto>.Failure(preparacion);
            // SQLite toma el bloqueo de escritura antes de leer las referencias; el límite se valida sobre el estado vigente.
            await using var transaccion = await db.Database.BeginTransactionAsync(ct);
            // Un circuito administrativo puede conservar un DbContext: volver a cargar este agregado evita usar fotos obsoletas.
            foreach (var entrada in db.ChangeTracker.Entries().Where(x =>
                x.Entity is Producto p && p.Id == id || x.Entity is ProductoImagen foto && foto.ProductoId == id).ToList())
                entrada.State = EntityState.Detached;
            var producto = await db.Productos.Include(x => x.Imagenes).SingleOrDefaultAsync(x => x.Id == id, ct);
            if (producto is null) return ServiceResult<ProductoDto>.Failure("Producto no encontrado.");
            var existentes = producto.Imagenes.ToDictionary(x => x.Id);
            if (existentes.Count == 0 && !string.IsNullOrWhiteSpace(producto.ImagenPrincipalRuta))
            {
                var heredada = new ProductoImagen { ProductoId = id, RutaRelativa = producto.ImagenPrincipalRuta, Orden = 0 };
                db.ProductoImagenes.Add(heredada);
                existentes.Add(Guid.Empty, heredada);
            }
            if (galeria.ImagenesOrdenadas.Any(x => x.ImagenId.HasValue && !existentes.ContainsKey(x.ImagenId.Value)))
                return ServiceResult<ProductoDto>.Failure("Una imagen no pertenece al producto o ya no está disponible.");
            var edicion = await productos.EditarAsync(id, input, ct);
            if (!edicion.IsSuccess || edicion.Value is null) return edicion;
            var confirmacion = await ConfirmarGaleriaAsync(preparadas, guardadas, id, ct);
            if (confirmacion is not null) return ServiceResult<ProductoDto>.Failure(confirmacion);
            var finales = galeria.ImagenesOrdenadas.Select((x, i) =>
            {
                var imagen = x.ImagenId.HasValue ? existentes[x.ImagenId.Value]
                    : new ProductoImagen { ProductoId = id, RutaRelativa = guardadas[x.NuevaImagenIndice!.Value].RutaRelativa };
                imagen.Orden = i;
                return imagen;
            }).ToList();
            foreach (var imagen in producto.Imagenes.Where(x => !finales.Contains(x)).ToList())
            {
                eliminadas.Add(imagen.RutaRelativa);
                db.ProductoImagenes.Remove(imagen);
                producto.Imagenes.Remove(imagen);
            }
            foreach (var imagen in finales.Where(x => !producto.Imagenes.Contains(x)))
                db.ProductoImagenes.Add(imagen);
            producto.ImagenPrincipalRuta = finales.Count == 0 ? null : finales[galeria.PortadaIndice].RutaRelativa;
            esperado = edicion.Value with { ImagenPrincipalRuta = producto.ImagenPrincipalRuta };
            idsEsperados = finales.Select(x => x.Id).ToArray();
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            persistida = true;
            return await productos.ObtenerPorIdAsync(id, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible editar la galería del producto {ProductoId}.", id);
            db.ChangeTracker.Clear();
            var recuperado = await RecuperarGaleriaEditadaAsync(id, esperado, idsEsperados);
            if (recuperado is not null)
            {
                persistida = true;
                return ServiceResult<ProductoDto>.Ok(recuperado);
            }
            return ServiceResult<ProductoDto>.Failure("No fue posible guardar el producto con sus imágenes.");
        }
        finally
        {
            if (!persistida) db.ChangeTracker.Clear();
            await LimpiarGaleriaAsync(preparadas, guardadas, persistida);
            foreach (var ruta in eliminadas.Distinct()) await EliminarAnteriorAsync(ruta);
        }
    }

    private static string? ValidarCantidadYPortada(int cantidad, int portadaIndice)
    {
        if (cantidad > MaximoImagenes) return "Cada producto admite como máximo 8 fotografías en total.";
        if (portadaIndice < 0 || (cantidad > 0 && portadaIndice >= cantidad) || (cantidad == 0 && portadaIndice != 0))
            return "Selecciona una portada válida.";
        return null;
    }

    private static string? ValidarEdicion(GaleriaProductoEdicion galeria, int nuevas)
    {
        var error = ValidarCantidadYPortada(galeria.ImagenesOrdenadas.Count, galeria.PortadaIndice);
        if (error is not null) return error;
        var referencias = galeria.ImagenesOrdenadas;
        if (nuevas > MaximoImagenes || referencias.Any(x => x.ImagenId.HasValue == x.NuevaImagenIndice.HasValue))
            return "Las referencias de imágenes no son válidas.";
        if (referencias.Where(x => x.ImagenId.HasValue).Select(x => x.ImagenId).Distinct().Count() != referencias.Count(x => x.ImagenId.HasValue))
            return "No se puede repetir una imagen.";
        var indices = referencias.Where(x => x.NuevaImagenIndice.HasValue).Select(x => x.NuevaImagenIndice!.Value).Order().ToArray();
        if (!indices.SequenceEqual(Enumerable.Range(0, nuevas)))
            return "Las imágenes nuevas deben asociarse exactamente una vez al producto.";
        return null;
    }

    private async Task<string?> PrepararGaleriaAsync(IReadOnlyList<Stream> imagenes, List<ImagenProductoPreparada> preparadas, CancellationToken ct)
    {
        foreach (var imagen in imagenes)
        {
            var resultado = await almacenamiento.PrepararGaleriaAsync(imagen, ct);
            if (!resultado.IsSuccess || resultado.Value is null)
                return resultado.ErrorMessage ?? "No fue posible preparar una imagen.";
            preparadas.Add(resultado.Value);
        }
        return null;
    }

    private async Task<string?> ConfirmarGaleriaAsync(List<ImagenProductoPreparada> preparadas, List<ImagenProductoGuardada> guardadas, int id, CancellationToken ct)
    {
        foreach (var preparada in preparadas)
        {
            var resultado = await almacenamiento.ConfirmarAsync(preparada, id, ct);
            if (!resultado.IsSuccess || resultado.Value is null)
                return resultado.ErrorMessage ?? "No fue posible guardar una imagen.";
            guardadas.Add(resultado.Value);
        }
        return null;
    }

    private async Task<ProductoDto?> RecuperarGaleriaEditadaAsync(int id, ProductoDto? esperado, Guid[]? idsEsperados)
    {
        if (esperado is null || idsEsperados is null) return null;
        try
        {
            var persistido = await productos.ObtenerPorIdAsync(id, CancellationToken.None);
            var ids = await db.ProductoImagenes.AsNoTracking().Where(x => x.ProductoId == id)
                .OrderBy(x => x.Orden).Select(x => x.Id).ToArrayAsync(CancellationToken.None);
            return persistido.IsSuccess && persistido.Value == esperado && ids.SequenceEqual(idsEsperados)
                ? persistido.Value : null;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "No se pudo comprobar la galería persistida del producto {ProductoId}.", id);
            return null;
        }
    }

    private async Task<ProductoDto?> RecuperarGaleriaCreadaAsync(int id, List<ImagenProductoGuardada> guardadas)
    {
        if (guardadas.Count == 0) return null;
        try
        {
            var rutas = guardadas.Select(x => x.RutaRelativa).ToList();
            var cantidad = await db.ProductoImagenes.AsNoTracking()
                .CountAsync(x => x.ProductoId == id && rutas.Contains(x.RutaRelativa), CancellationToken.None);
            if (cantidad != rutas.Count) return null;
            var persistido = await productos.ObtenerPorIdAsync(id, CancellationToken.None);
            return persistido.IsSuccess ? persistido.Value : null;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "No se pudo comprobar el guardado de la galería del producto {ProductoId}; se conservan los archivos cuya referencia no se puede verificar.", id);
            return null;
        }
    }
    private async Task LimpiarGaleriaAsync(List<ImagenProductoPreparada> preparadas, List<ImagenProductoGuardada> guardadas, bool persistida)
    {
        foreach (var preparada in preparadas) await LimpiarTemporalAsync(preparada.IdentificadorTemporal);
        if (!persistida)
            foreach (var guardada in guardadas) await EliminarNuevoAsync(guardada.RutaRelativa);
    }

    private async Task<bool> RutaReferenciadaAsync(string ruta)
    {
        try
        {
            return await db.Productos.AsNoTracking().AnyAsync(x => x.ImagenPrincipalRuta == ruta, CancellationToken.None)
                || await db.ProductoImagenes.AsNoTracking().AnyAsync(x => x.RutaRelativa == ruta, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "No se pudo comprobar si una imagen sigue referenciada; se conserva su archivo.");
            return true;
        }
    }
}

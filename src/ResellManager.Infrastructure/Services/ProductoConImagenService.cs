using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Infrastructure.Services;

public sealed class ProductoConImagenService(
    IProductoService productos,
    IAlmacenamientoImagenesProducto almacenamiento,
    ResellManagerDbContext db,
    ILogger<ProductoConImagenService> logger, IImagenProductoExternaService? imagenesExternas = null) : IProductoConImagenService, IAltaProductoAsistidaService
{
    public async Task<AltaProductoAsistidaResultado> CrearAsistidoAsync(
        ProductoInput input, Stream? imagenManual, string? imagenExternaUrl, CancellationToken ct = default)
    {
        // No se descarga una imagen cuando el código ya existe.
        var errorCodigo = await ErrorCodigoRegistradoAsync(input, ct);
        if (errorCodigo is not null) return new(ServiceResult<ProductoDto>.Failure(errorCodigo));
        if (imagenManual is not null)
            return new(await CrearConImagenAsync(input, imagenManual, false, true, null, ct));
        if (string.IsNullOrEmpty(imagenExternaUrl))
            return new(await CrearSinImagenAsistidoAsync(input, ct));
        var descarga = imagenesExternas is not null
            ? await imagenesExternas.DescargarAsync(imagenExternaUrl, ct)
            : ServiceResult<Stream>.Failure("La imagen externa no está disponible.");
        const string aviso = "No pudimos incorporar la imagen externa. El producto se guardó sin ella.";
        if (!descarga.IsSuccess || descarga.Value is null)
            return new(await CrearSinImagenAsistidoAsync(input, ct), aviso);
        await using var imagen = descarga.Value;
        string? avisoImagen = null;
        var resultado = await CrearConImagenAsync(input, imagen, true, true, () => avisoImagen = aviso, ct);
        return new(resultado, avisoImagen);
    }

    public Task<ServiceResult<ProductoDto>> CrearAsync(
        ProductoInput input, Stream? imagen, CancellationToken ct = default) =>
        CrearConImagenAsync(input, imagen, false, false, null, ct);

    private async Task<ServiceResult<ProductoDto>> CrearConImagenAsync(
        ProductoInput input, Stream? imagen, bool imagenOpcional, bool validarCodigo, Action? avisar, CancellationToken ct)
    {
        if (imagen is null)
            return validarCodigo ? await CrearSinImagenAsistidoAsync(input, ct) : await productos.CrearAsync(input, ct);

        ImagenProductoPreparada? preparada = null;
        ImagenProductoGuardada? guardada = null;
        int? productoId = null;
        var confirmada = false;
        try
        {
            var preparacion = await almacenamiento.PrepararAsync(imagen, ct);
            if (!preparacion.IsSuccess || preparacion.Value is null)
            {
                if (!imagenOpcional)
                    return ServiceResult<ProductoDto>.Failure(preparacion.ErrorMessage ?? "No fue posible preparar la imagen.");
                avisar?.Invoke();
                return validarCodigo ? await CrearSinImagenAsistidoAsync(input, ct) : await productos.CrearAsync(input, ct);
            }
            preparada = preparacion.Value;

            await using var transaccion = await db.Database.BeginTransactionAsync(ct);
            if (validarCodigo)
            {
                var errorCodigo = await ErrorCodigoRegistradoAsync(input, ct);
                if (errorCodigo is not null) return ServiceResult<ProductoDto>.Failure(errorCodigo);
            }
            var creacion = await productos.CrearAsync(input, ct);
            if (!creacion.IsSuccess || creacion.Value is null)
                return creacion;
            productoId = creacion.Value.Id;

            var confirmacion = await almacenamiento.ConfirmarAsync(preparada, productoId.Value, ct);
            if (!confirmacion.IsSuccess || confirmacion.Value is null)
            {
                if (!imagenOpcional)
                    return ServiceResult<ProductoDto>.Failure(confirmacion.ErrorMessage ?? "No fue posible guardar la imagen.");
                avisar?.Invoke();
            }
            else guardada = confirmacion.Value;

            var entidad = await db.Productos.FindAsync([productoId.Value], ct);
            entidad!.ImagenPrincipalRuta = guardada?.RutaRelativa;
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            confirmada = true;
            return await productos.ObtenerPorIdAsync(productoId.Value, ct);
        }
        catch (OperationCanceledException)
        {
            await RecuperarCreacionAsync(productoId, guardada, CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible crear el producto con imagen.");
            var recuperado = await RecuperarCreacionAsync(productoId, guardada, CancellationToken.None);
            return recuperado is not null
                ? ServiceResult<ProductoDto>.Ok(recuperado)
                : ServiceResult<ProductoDto>.Failure("No fue posible guardar el producto con su imagen.");
        }
        finally
        {
            if (preparada is not null && (!confirmada || guardada is null))
                await LimpiarTemporalAsync(preparada.IdentificadorTemporal);
        }
    }

    private async Task<ServiceResult<ProductoDto>> CrearSinImagenAsistidoAsync(ProductoInput input, CancellationToken ct)
    {
        // SQLite adquiere el bloqueo de escritura al iniciar la transacción, antes de leer el código.
        // Esto también cubre otro registro que termine mientras se descarga/prepara una imagen.
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var errorCodigo = await ErrorCodigoRegistradoAsync(input, ct);
        if (errorCodigo is not null) return ServiceResult<ProductoDto>.Failure(errorCodigo);
        var resultado = await productos.CrearAsync(input, ct);
        if (resultado.IsSuccess) await transaccion.CommitAsync(ct);
        return resultado;
    }

    private async Task<string?> ErrorCodigoRegistradoAsync(ProductoInput input, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.CodigoBarras)) return null;
        var existente = await db.Productos.AsNoTracking()
            .Where(x => x.CodigoBarras == input.CodigoBarras).Select(x => x.Nombre).FirstOrDefaultAsync(ct);
        return existente is not null
            ? $"El código ya pertenece al producto {existente}. Abre ese producto para consultarlo." : null;
    }
    public async Task<ServiceResult<ProductoDto>> EditarAsync(
        int id, ProductoInput input, Stream? imagen, bool eliminarImagen, CancellationToken ct = default)
    {
        if (imagen is not null && eliminarImagen)
            return ServiceResult<ProductoDto>.Failure("Selecciona reemplazar o eliminar la imagen.");

        var anterior = await db.Productos.AsNoTracking()
            .Where(x => x.Id == id).Select(x => x.ImagenPrincipalRuta).FirstOrDefaultAsync(ct);
        if (imagen is null && (!eliminarImagen || anterior is null))
            return await productos.EditarAsync(id, input, ct);

        ImagenProductoPreparada? preparada = null;
        ImagenProductoGuardada? guardada = null;
        var confirmada = false;
        try
        {
            if (imagen is not null)
            {
                var preparacion = await almacenamiento.PrepararAsync(imagen, ct);
                if (!preparacion.IsSuccess || preparacion.Value is null)
                    return ServiceResult<ProductoDto>.Failure(preparacion.ErrorMessage ?? "No fue posible preparar la imagen.");
                preparada = preparacion.Value;
            }

            await using var transaccion = await db.Database.BeginTransactionAsync(ct);
            var edicion = await productos.EditarAsync(id, input, ct);
            if (!edicion.IsSuccess || edicion.Value is null)
                return edicion;

            if (preparada is not null)
            {
                var resultado = await almacenamiento.ConfirmarAsync(preparada, id, ct);
                if (!resultado.IsSuccess || resultado.Value is null)
                    return ServiceResult<ProductoDto>.Failure(resultado.ErrorMessage ?? "No fue posible guardar la imagen.");
                guardada = resultado.Value;
            }

            var entidad = await db.Productos.FindAsync([id], ct);
            entidad!.ImagenPrincipalRuta = guardada?.RutaRelativa;
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            confirmada = true;
            if (anterior is not null)
                await EliminarAnteriorAsync(anterior);
            return await productos.ObtenerPorIdAsync(id, ct);
        }
        catch (OperationCanceledException)
        {
            await RecuperarEdicionAsync(id, anterior, guardada);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No fue posible editar el producto {ProductoId} con su imagen.", id);
            var recuperado = await RecuperarEdicionAsync(id, anterior, guardada);
            return recuperado is not null
                ? ServiceResult<ProductoDto>.Ok(recuperado)
                : ServiceResult<ProductoDto>.Failure("No fue posible guardar el producto con su imagen.");
        }
        finally
        {
            if (preparada is not null && !confirmada)
                await LimpiarTemporalAsync(preparada.IdentificadorTemporal);
        }
    }

    private async Task<ProductoDto?> RecuperarCreacionAsync(
        int? id, ImagenProductoGuardada? guardada, CancellationToken ct)
    {
        if (guardada is null) return null;
        try
        {
            var persistido = id.HasValue ? await productos.ObtenerPorIdAsync(id.Value, ct) : null;
            if (persistido?.IsSuccess == true
                && persistido.Value?.ImagenPrincipalRuta == guardada.RutaRelativa)
                return persistido.Value;
            await EliminarNuevoAsync(guardada.RutaRelativa);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "No se pudo comprobar el guardado del producto; se conserva su imagen.");
        }
        return null;
    }

    private async Task<ProductoDto?> RecuperarEdicionAsync(
        int id, string? anterior, ImagenProductoGuardada? guardada)
    {
        try
        {
            var persistido = await productos.ObtenerPorIdAsync(id, CancellationToken.None);
            if (persistido.IsSuccess && persistido.Value is not null
                && persistido.Value.ImagenPrincipalRuta == guardada?.RutaRelativa)
            {
                if (anterior is not null) await EliminarAnteriorAsync(anterior);
                return persistido.Value;
            }
            if (guardada is not null) await EliminarNuevoAsync(guardada.RutaRelativa);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "No se pudo comprobar la referencia de la imagen del producto {ProductoId}; se conserva el archivo.", id);
        }
        return null;
    }

    private async Task LimpiarTemporalAsync(string identificador)
    {
        var resultado = await almacenamiento.EliminarTemporalAsync(identificador, CancellationToken.None);
        if (!resultado.IsSuccess)
            logger.LogCritical("No fue posible limpiar una imagen temporal de producto.");
    }

    private async Task EliminarNuevoAsync(string ruta)
    {
        var resultado = await almacenamiento.EliminarAsync(ruta, CancellationToken.None);
        if (!resultado.IsSuccess)
            logger.LogCritical("No fue posible limpiar la nueva imagen tras fallar el guardado del producto.");
    }

    private async Task EliminarAnteriorAsync(string ruta)
    {
        var resultado = await almacenamiento.EliminarAsync(ruta, CancellationToken.None);
        if (!resultado.IsSuccess)
            logger.LogCritical("La referencia del producto cambió pero no fue posible eliminar su imagen anterior {Ruta}.", ruta);
    }
}

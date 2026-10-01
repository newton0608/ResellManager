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
    ILogger<ProductoConImagenService> logger) : IProductoConImagenService
{
    public async Task<ServiceResult<ProductoDto>> CrearAsync(
        ProductoInput input, Stream? imagen, CancellationToken ct = default)
    {
        if (imagen is null)
            return await productos.CrearAsync(input, ct);

        ImagenProductoPreparada? preparada = null;
        ImagenProductoGuardada? guardada = null;
        int? productoId = null;
        var confirmada = false;
        try
        {
            var preparacion = await almacenamiento.PrepararAsync(imagen, ct);
            if (!preparacion.IsSuccess || preparacion.Value is null)
                return ServiceResult<ProductoDto>.Failure(preparacion.ErrorMessage ?? "No fue posible preparar la imagen.");
            preparada = preparacion.Value;

            await using var transaccion = await db.Database.BeginTransactionAsync(ct);
            var creacion = await productos.CrearAsync(input, ct);
            if (!creacion.IsSuccess || creacion.Value is null)
                return creacion;
            productoId = creacion.Value.Id;

            var confirmacion = await almacenamiento.ConfirmarAsync(preparada, productoId.Value, ct);
            if (!confirmacion.IsSuccess || confirmacion.Value is null)
                return ServiceResult<ProductoDto>.Failure(confirmacion.ErrorMessage ?? "No fue posible guardar la imagen.");
            guardada = confirmacion.Value;

            var entidad = await db.Productos.FindAsync([productoId.Value], ct);
            entidad!.ImagenPrincipalRuta = guardada.RutaRelativa;
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
            if (preparada is not null && !confirmada)
                await LimpiarTemporalAsync(preparada.IdentificadorTemporal);
        }
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

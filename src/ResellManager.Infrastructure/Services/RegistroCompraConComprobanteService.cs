using Microsoft.Extensions.Logging;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;

namespace ResellManager.Infrastructure.Services;

public sealed class RegistroCompraConComprobanteService(
    ICompraService compraService,
    IAlmacenamientoComprobantes almacenamiento,
    ILogger<RegistroCompraConComprobanteService> logger
) : IRegistroCompraConComprobanteService
{
    public async Task<ServiceResult<CompraDto>> RegistrarAsync(
        CompraInput compra,
        DatosComprobanteCompraInput? datosComprobante,
        ArchivoComprobanteInput? archivo,
        CancellationToken ct = default
    )
    {
        if (compra.Comprobante is not null)
            return ServiceResult<CompraDto>.Failure(
                "La ruta del comprobante se genera exclusivamente mediante el archivo adjunto."
            );
        if ((datosComprobante is null) != (archivo is null))
            return ServiceResult<CompraDto>.Failure(
                "Adjunta el archivo y sus datos de comprobante, o registra la compra sin comprobante."
            );

        if (archivo is null || datosComprobante is null)
            return await compraService.RegistrarAsync(compra with { Comprobante = null }, ct);

        ComprobantePreparadoDto? preparado = null;
        ComprobanteGuardadoDto? guardado = null;
        try
        {
            var preparacion = await almacenamiento.PrepararAsync(archivo, ct);
            if (!preparacion.IsSuccess || preparacion.Value is null)
                return ServiceResult<CompraDto>.Failure(
                    preparacion.ErrorMessage ?? "No fue posible preparar el comprobante."
                );
            preparado = preparacion.Value;

            var confirmacion = await almacenamiento.ConfirmarAsync(preparado, ct);
            if (!confirmacion.IsSuccess || confirmacion.Value is null)
            {
                await LimpiarTemporalAsync(preparado.IdentificadorTemporal, ct);
                return ServiceResult<CompraDto>.Failure(
                    confirmacion.ErrorMessage ?? "No fue posible guardar el comprobante."
                );
            }
            guardado = confirmacion.Value;

            var inputConComprobante = compra with
            {
                Comprobante = new ComprobanteCompraInput(
                    datosComprobante.NumeroDocumento,
                    datosComprobante.Fecha,
                    guardado.RutaRelativa,
                    datosComprobante.Observaciones
                ),
            };
            var resultado = await compraService.RegistrarAsync(inputConComprobante, ct);
            if (resultado.IsSuccess)
                return resultado;

            var limpieza = await almacenamiento.EliminarAsync(guardado.RutaRelativa, ct);
            if (!limpieza.IsSuccess)
                logger.LogCritical(
                    "La compra no se registró y no fue posible limpiar su comprobante confirmado."
                );
            return resultado;
        }
        catch (OperationCanceledException)
        {
            await RecuperarCompraOLimpiarAsync(
                compra.CodigoInterno,
                preparado,
                guardado,
                CancellationToken.None
            );
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ocurrió un fallo inesperado al registrar compra y comprobante.");
            var recuperada = await RecuperarCompraOLimpiarAsync(
                compra.CodigoInterno,
                preparado,
                guardado,
                CancellationToken.None
            );
            if (recuperada is not null)
                return ServiceResult<CompraDto>.Ok(recuperada);

            return ServiceResult<CompraDto>.Failure(
                "Ocurrió un problema inesperado al registrar la compra. Intenta nuevamente."
            );
        }
    }

    private async Task<CompraDto?> RecuperarCompraOLimpiarAsync(
        string codigoCompra,
        ComprobantePreparadoDto? preparado,
        ComprobanteGuardadoDto? guardado,
        CancellationToken ct
    )
    {
        if (guardado is not null)
        {
            try
            {
                var compraPersistida = (await compraService.ListarAsync(ct)).FirstOrDefault(x =>
                    string.Equals(x.CodigoInterno, codigoCompra, StringComparison.Ordinal)
                );
                if (compraPersistida is not null)
                {
                    logger.LogWarning(
                        "La compra {CodigoCompra} quedó persistida pese a una excepción posterior.",
                        codigoCompra
                    );
                    return compraPersistida;
                }

                var limpieza = await almacenamiento.EliminarAsync(guardado.RutaRelativa, ct);
                if (!limpieza.IsSuccess)
                    logger.LogCritical(
                        "La compra no existe y no fue posible limpiar su comprobante confirmado."
                    );
            }
            catch (Exception ex)
            {
                logger.LogCritical(
                    ex,
                    "No se pudo determinar si la compra {CodigoCompra} quedó persistida; se conserva el archivo para evitar una referencia rota.",
                    codigoCompra
                );
            }
        }
        else if (preparado is not null)
        {
            await LimpiarTemporalAsync(preparado.IdentificadorTemporal, ct);
        }

        return null;
    }

    private async Task LimpiarTemporalAsync(string identificador, CancellationToken ct)
    {
        var limpieza = await almacenamiento.EliminarTemporalAsync(identificador, ct);
        if (!limpieza.IsSuccess)
            logger.LogError("No fue posible limpiar un comprobante temporal tras un fallo.");
    }
}

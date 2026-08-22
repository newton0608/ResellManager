using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Infrastructure.Services;

public sealed class CompraService(ResellManagerDbContext db) : ICompraService
{
    public async Task<ServiceResult<CompraDto>> RegistrarAsync(
        CompraInput input,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(input.CodigoInterno))
            return ServiceResult<CompraDto>.Failure("El código interno es obligatorio.");
        if (input.Detalles.Count == 0)
            return ServiceResult<CompraDto>.Failure("La compra debe incluir detalles.");
        if (!await db.Proveedores.AnyAsync(x => x.Id == input.ProveedorId, ct))
            return ServiceResult<CompraDto>.Failure("Proveedor no encontrado.");
        if (await db.Compras.AnyAsync(x => x.CodigoInterno == input.CodigoInterno.Trim(), ct))
            return ServiceResult<CompraDto>.Failure("El código de compra ya está registrado.");
        if (input.Detalles.Any(x => x.Cantidad <= 0 || x.CostoUnitario < 0))
            return ServiceResult<CompraDto>.Failure(
                "La cantidad y el costo de los detalles no son válidos."
            );

        var productIds = input.Detalles.Select(x => x.ProductoId).Distinct().ToArray();
        if (await db.Productos.CountAsync(x => productIds.Contains(x.Id), ct) != productIds.Length)
            return ServiceResult<CompraDto>.Failure("Uno o más productos no existen.");
        if (
            input.Comprobante is not null
            && string.IsNullOrWhiteSpace(input.Comprobante.RutaDocumento)
        )
            return ServiceResult<CompraDto>.Failure("La ruta del comprobante es obligatoria.");
        if (
            input.Origen is OrigenCompra.CompraLocal or OrigenCompra.EnvioHermano
            && !input.FechaIngreso.HasValue
        )
            return ServiceResult<CompraDto>.Failure(
                "La fecha de ingreso es obligatoria para mercancía recibida."
            );
        if (input.Origen == OrigenCompra.Importacion && input.FechaIngreso.HasValue)
            return ServiceResult<CompraDto>.Failure(
                "La importación debe registrar su ingreso mediante la recepción de mercancía."
            );

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var compra = new Compra
        {
            CodigoInterno = input.CodigoInterno.Trim(),
            FechaCompra = input.FechaCompra,
            Origen = input.Origen,
            ProveedorId = input.ProveedorId,
            Observaciones = input.Observaciones?.Trim(),
            Total = input.Detalles.Sum(x => x.Cantidad * x.CostoUnitario),
        };

        var generaInventario = input.Origen != OrigenCompra.Catalogo;
        var estadoInicial =
            input.Origen == OrigenCompra.Importacion
                ? EstadoUnidadInventario.Comprada
                : EstadoUnidadInventario.Disponible;
        var detailNumber = 0;

        foreach (var item in input.Detalles)
        {
            detailNumber++;
            var detalle = new DetalleCompra
            {
                ProductoId = item.ProductoId,
                Cantidad = item.Cantidad,
                CostoUnitario = item.CostoUnitario,
            };

            if (generaInventario)
            {
                for (var i = 1; i <= item.Cantidad; i++)
                    detalle.UnidadesInventario.Add(
                        new UnidadInventario
                        {
                            CodigoInterno = $"{compra.CodigoInterno}-{detailNumber:D2}-{i:D3}",
                            Estado = estadoInicial,
                            FechaIngreso =
                                estadoInicial == EstadoUnidadInventario.Disponible
                                    ? input.FechaIngreso
                                    : null,
                            Costo = item.CostoUnitario,
                            ProductoId = item.ProductoId,
                        }
                    );
            }

            compra.Detalles.Add(detalle);
        }

        if (input.Comprobante is not null)
            compra.Comprobante = new ComprobanteCompra
            {
                NumeroDocumento = input.Comprobante.NumeroDocumento?.Trim(),
                Fecha = input.Comprobante.Fecha,
                RutaDocumento = input.Comprobante.RutaDocumento.Trim(),
                Observaciones = input.Comprobante.Observaciones?.Trim(),
            };

        db.Compras.Add(compra);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(ct);
            return ServiceResult<CompraDto>.Failure(
                "No se pudo registrar la compra; verifique que los códigos generados no estén duplicados."
            );
        }

        return await ObtenerPorIdAsync(compra.Id, ct);
    }

    public async Task<ServiceResult<CompraDto>> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default
    )
    {
        var compra = await CompraCompleta().FirstOrDefaultAsync(x => x.Id == id, ct);
        return compra is null
            ? ServiceResult<CompraDto>.Failure("Compra no encontrada.")
            : ServiceResult<CompraDto>.Ok(Map(compra));
    }

    public async Task<IReadOnlyList<CompraDto>> ListarAsync(CancellationToken ct = default)
    {
        var compras = await CompraCompleta()
            .OrderByDescending(x => x.FechaCompra)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
        return compras.Select(Map).ToList();
    }

    public async Task<ServiceResult<ComprobanteCompraDto>> ObtenerComprobanteAsync(
        int compraId,
        CancellationToken ct = default
    )
    {
        if (!await db.Compras.AnyAsync(x => x.Id == compraId, ct))
            return ServiceResult<ComprobanteCompraDto>.Failure("Compra no encontrada.");

        var comprobante = await db
            .ComprobantesCompra.AsNoTracking()
            .Where(x => x.CompraId == compraId)
            .Select(x => new ComprobanteCompraDto(
                x.Id,
                x.CompraId,
                x.NumeroDocumento,
                x.Fecha,
                x.RutaDocumento,
                x.Observaciones
            ))
            .FirstOrDefaultAsync(ct);

        return comprobante is null
            ? ServiceResult<ComprobanteCompraDto>.Failure(
                "La compra no tiene un comprobante registrado."
            )
            : ServiceResult<ComprobanteCompraDto>.Ok(comprobante);
    }

    private IQueryable<Compra> CompraCompleta() =>
        db
            .Compras.AsNoTracking()
            .Include(x => x.Proveedor)
            .Include(x => x.Comprobante)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.Producto)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.UnidadesInventario);

    private static CompraDto Map(Compra x) =>
        new(
            x.Id,
            x.CodigoInterno,
            x.FechaCompra,
            x.Origen,
            x.Total,
            x.Observaciones,
            x.ProveedorId,
            x.Proveedor.Nombre,
            x.Detalles.Select(d => new DetalleCompraDto(
                    d.Id,
                    d.ProductoId,
                    d.Producto.Nombre,
                    d.Cantidad,
                    d.CostoUnitario,
                    d.Cantidad * d.CostoUnitario
                ))
                .ToList(),
            x.Comprobante?.RutaDocumento
        );
}

public sealed class InventarioService(ResellManagerDbContext db) : IInventarioService
{
    public async Task<IReadOnlyList<UnidadInventarioDto>> ListarAsync(
        CancellationToken ct = default
    ) =>
        await Query(
                db.UnidadesInventario.OrderByDescending(x => x.FechaIngreso)
                    .ThenBy(x => x.CodigoInterno)
            )
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UnidadInventarioDto>> ListarDisponiblesAsync(
        CancellationToken ct = default
    ) =>
        await Query(
                db.UnidadesInventario.Where(x => x.Estado == EstadoUnidadInventario.Disponible)
                    .OrderBy(x => x.Producto.Nombre)
            )
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UnidadInventarioDto>> BuscarAsync(
        string? termino,
        EstadoUnidadInventario? estado,
        CancellationToken ct = default
    )
    {
        var unidades = db.UnidadesInventario.AsQueryable();
        if (!string.IsNullOrWhiteSpace(termino))
        {
            termino = termino.Trim();
            unidades = unidades.Where(x =>
                x.Producto.Nombre.Contains(termino)
                || x.Producto.CodigoInterno.Contains(termino)
                || x.CodigoInterno.Contains(termino)
            );
        }

        if (estado.HasValue)
            unidades = unidades.Where(x => x.Estado == estado.Value);

        return await Query(unidades.OrderBy(x => x.Producto.Nombre).ThenBy(x => x.CodigoInterno))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult<IReadOnlyList<UnidadInventarioDto>>> RegistrarRecepcionAsync(
        RecepcionMercanciaInput input,
        CancellationToken ct = default
    )
    {
        var ids = input.UnidadInventarioIds.Distinct().ToArray();
        if (ids.Length == 0)
            return ServiceResult<IReadOnlyList<UnidadInventarioDto>>.Failure(
                "Debe indicar al menos una unidad para recibir."
            );

        var unidades = await db
            .UnidadesInventario.Include(x => x.DetalleCompra)
                .ThenInclude(x => x.Compra)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);

        if (unidades.Count != ids.Length)
            return ServiceResult<IReadOnlyList<UnidadInventarioDto>>.Failure(
                "Una o más unidades de inventario no existen."
            );
        if (unidades.Any(x => x.DetalleCompra.Compra.Origen == OrigenCompra.Catalogo))
            return ServiceResult<IReadOnlyList<UnidadInventarioDto>>.Failure(
                "Las compras de catálogo no generan unidades para recepción."
            );
        if (unidades.Any(x => x.Estado == EstadoUnidadInventario.Vendida))
            return ServiceResult<IReadOnlyList<UnidadInventarioDto>>.Failure(
                "Una unidad vendida no puede recibirse."
            );
        if (unidades.Any(x => x.Estado == EstadoUnidadInventario.Entregada))
            return ServiceResult<IReadOnlyList<UnidadInventarioDto>>.Failure(
                "Una unidad entregada no puede recibirse."
            );
        if (unidades.Any(x => x.Estado == EstadoUnidadInventario.Disponible))
            return ServiceResult<IReadOnlyList<UnidadInventarioDto>>.Failure(
                "Una o más unidades ya están disponibles."
            );

        foreach (var unidad in unidades)
        {
            unidad.FechaIngreso = input.FechaRecepcion;
            if (
                unidad.Estado
                is EstadoUnidadInventario.Comprada or EstadoUnidadInventario.EnTransito
            )
                unidad.Estado = EstadoUnidadInventario.Disponible;
        }

        await db.SaveChangesAsync(ct);
        var recibidas = await Query(db.UnidadesInventario.Where(x => ids.Contains(x.Id)))
            .ToListAsync(ct);
        return ServiceResult<IReadOnlyList<UnidadInventarioDto>>.Ok(recibidas);
    }

    public async Task<ServiceResult<UnidadInventarioDto>> ReservarAsync(
        int unidadInventarioId,
        int detallePedidoId,
        CancellationToken ct = default
    )
    {
        var unidad = await db
            .UnidadesInventario.Include(x => x.DetallePedidoReserva)
            .FirstOrDefaultAsync(x => x.Id == unidadInventarioId, ct);
        if (unidad is null)
            return ServiceResult<UnidadInventarioDto>.Failure(
                "Unidad de inventario no encontrada."
            );

        var detalle = await db
            .DetallesPedido.Include(x => x.Pedido)
            .Include(x => x.UnidadesReservadas)
            .FirstOrDefaultAsync(x => x.Id == detallePedidoId, ct);
        if (detalle is null)
            return ServiceResult<UnidadInventarioDto>.Failure("Detalle de pedido no encontrado.");
        if (detalle.Pedido.Estado is EstadoPedido.Cancelado or EstadoPedido.Completado)
            return ServiceResult<UnidadInventarioDto>.Failure(
                "No se puede reservar para un pedido cancelado o completado."
            );
        if (detalle.Pedido.TipoPedido == TipoPedido.Catalogo)
            return ServiceResult<UnidadInventarioDto>.Failure(
                "Un pedido de catálogo no puede reservar unidades de inventario."
            );
        if (unidad.ProductoId != detalle.ProductoId)
            return ServiceResult<UnidadInventarioDto>.Failure(
                "La unidad y el detalle de pedido deben corresponder al mismo producto."
            );
        if (unidad.Estado is EstadoUnidadInventario.Vendida or EstadoUnidadInventario.Entregada)
            return ServiceResult<UnidadInventarioDto>.Failure(
                "Una unidad vendida o entregada no puede reservarse."
            );
        if (
            unidad.DetallePedidoReservaId.HasValue
            && unidad.DetallePedidoReservaId.Value != detallePedidoId
        )
            return ServiceResult<UnidadInventarioDto>.Failure(
                "La unidad ya tiene una reserva activa para otro detalle de pedido."
            );
        if (unidad.DetallePedidoReservaId == detallePedidoId)
            return ServiceResult<UnidadInventarioDto>.Ok(
                await Query(db.UnidadesInventario.Where(x => x.Id == unidadInventarioId))
                    .SingleAsync(ct)
            );
        if (detalle.UnidadesReservadas.Count >= detalle.Cantidad)
            return ServiceResult<UnidadInventarioDto>.Failure(
                "El detalle de pedido ya tiene reservadas todas sus unidades."
            );

        unidad.DetallePedidoReservaId = detallePedidoId;
        await db.SaveChangesAsync(ct);
        var result = await Query(
                db.UnidadesInventario.Where(x => x.Id == unidadInventarioId)
            )
            .SingleAsync(ct);
        return ServiceResult<UnidadInventarioDto>.Ok(result);
    }

    public async Task<ServiceResult<UnidadInventarioDto>> CancelarReservaAsync(
        int unidadInventarioId,
        CancellationToken ct = default
    )
    {
        var unidad = await db.UnidadesInventario.FindAsync([unidadInventarioId], ct);
        if (unidad is null)
            return ServiceResult<UnidadInventarioDto>.Failure(
                "Unidad de inventario no encontrada."
            );

        unidad.DetallePedidoReservaId = null;
        await db.SaveChangesAsync(ct);
        var result = await Query(
                db.UnidadesInventario.Where(x => x.Id == unidadInventarioId)
            )
            .SingleAsync(ct);
        return ServiceResult<UnidadInventarioDto>.Ok(result);
    }

    public async Task<ServiceResult<UnidadInventarioDto>> CambiarEstadoAsync(
        int id,
        EstadoUnidadInventario estado,
        CancellationToken ct = default
    )
    {
        var unidad = await db.UnidadesInventario.FindAsync([id], ct);
        if (unidad is null)
            return ServiceResult<UnidadInventarioDto>.Failure(
                "Unidad de inventario no encontrada."
            );

        if (unidad.Estado == estado)
            return ServiceResult<UnidadInventarioDto>.Ok(
                await Query(db.UnidadesInventario.Where(x => x.Id == id)).SingleAsync(ct)
            );

        var transicionManualValida =
            (unidad.Estado == EstadoUnidadInventario.Comprada
                && estado == EstadoUnidadInventario.EnTransito)
            || (unidad.Estado == EstadoUnidadInventario.Vendida
                && estado == EstadoUnidadInventario.Entregada);

        if (!transicionManualValida)
        {
            if (estado == EstadoUnidadInventario.Disponible)
                return ServiceResult<UnidadInventarioDto>.Failure(
                    "Use la recepción de mercancía para marcar una unidad como disponible."
                );

            return ServiceResult<UnidadInventarioDto>.Failure(
                $"La transición manual de {unidad.Estado} a {estado} no está permitida."
            );
        }

        unidad.Estado = estado;
        await db.SaveChangesAsync(ct);
        var result = await Query(db.UnidadesInventario.Where(x => x.Id == id)).SingleAsync(ct);
        return ServiceResult<UnidadInventarioDto>.Ok(result);
    }

    private static IQueryable<UnidadInventarioDto> Query(IQueryable<UnidadInventario> source) =>
        source
            .AsNoTracking()
            .Select(x => new UnidadInventarioDto(
                x.Id,
                x.CodigoInterno,
                x.Estado,
                x.FechaIngreso,
                x.Costo,
                x.ProductoId,
                x.Producto.Nombre,
                x.Producto.CodigoInterno,
                x.DetalleCompraId,
                x.DetalleCompra.Compra.Origen,
                x.DetallePedidoReservaId,
                x.DetallePedidoReserva == null ? null : x.DetallePedidoReserva.PedidoId,
                x.DetallePedidoReserva == null ? null : x.DetallePedidoReserva.Pedido.ClienteId
            ));
}

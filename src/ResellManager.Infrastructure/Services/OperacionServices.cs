using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Infrastructure.Services;

public sealed class PedidoService(ResellManagerDbContext db) : IPedidoService
{
    public async Task<ServiceResult<PedidoDto>> CrearAsync(
        PedidoInput input,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(input.CodigoInterno) || input.Detalles.Count == 0)
            return ServiceResult<PedidoDto>.Failure("Código y detalles son obligatorios.");
        if (!await db.Clientes.AnyAsync(x => x.Id == input.ClienteId, ct))
            return ServiceResult<PedidoDto>.Failure("Cliente no encontrado.");
        if (await db.Pedidos.AnyAsync(x => x.CodigoInterno == input.CodigoInterno.Trim(), ct))
            return ServiceResult<PedidoDto>.Failure("El código de pedido ya está registrado.");
        var error = await ValidarDetalles(input.Detalles, ct);
        if (error is not null)
            return ServiceResult<PedidoDto>.Failure(error);
        var pedido = new Pedido
        {
            CodigoInterno = input.CodigoInterno.Trim(),
            Fecha = input.Fecha,
            TipoPedido = input.TipoPedido,
            Estado = EstadoPedido.Pendiente,
            ClienteId = input.ClienteId,
            Observaciones = input.Observaciones?.Trim(),
        };
        foreach (var d in input.Detalles)
            pedido.Detalles.Add(Map(d));
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync(ct);
        return await ObtenerPorIdAsync(pedido.Id, ct);
    }

    public async Task<ServiceResult<PedidoDto>> AgregarDetalleAsync(
        int pedidoId,
        DetallePedidoInput input,
        CancellationToken ct = default
    )
    {
        var pedido = await db
            .Pedidos.Include(x => x.Detalles)
            .FirstOrDefaultAsync(x => x.Id == pedidoId, ct);
        if (pedido is null)
            return ServiceResult<PedidoDto>.Failure("Pedido no encontrado.");
        if (pedido.Estado is EstadoPedido.Cancelado or EstadoPedido.Completado)
            return ServiceResult<PedidoDto>.Failure("No se puede modificar este pedido.");
        var error = await ValidarDetalles([input], ct);
        if (error is not null)
            return ServiceResult<PedidoDto>.Failure(error);
        pedido.Detalles.Add(Map(input));
        await db.SaveChangesAsync(ct);
        return await ObtenerPorIdAsync(pedidoId, ct);
    }

    public async Task<ServiceResult<PedidoDto>> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default
    )
    {
        var x = await Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        return x is null
            ? ServiceResult<PedidoDto>.Failure("Pedido no encontrado.")
            : ServiceResult<PedidoDto>.Ok(x);
    }

    public async Task<IReadOnlyList<PedidoDto>> ListarAsync(CancellationToken ct = default) =>
        await Query().OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Id).ToListAsync(ct);

    public async Task<ServiceResult> CancelarAsync(int id, CancellationToken ct = default)
    {
        var x = await db.Pedidos.Include(p => p.Venta).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (x is null)
            return ServiceResult.Failure("Pedido no encontrado.");
        if (x.Venta is { Estado: EstadoVenta.Registrada })
            return ServiceResult.Failure("El pedido tiene una venta registrada.");
        x.Estado = EstadoPedido.Cancelado;
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private async Task<string?> ValidarDetalles(
        IEnumerable<DetallePedidoInput> detalles,
        CancellationToken ct
    )
    {
        var a = detalles.ToArray();
        if (a.Any(x => x.Cantidad <= 0 || x.PrecioUnitario < 0))
            return "Cantidad o precio no válido.";
        var ids = a.Select(x => x.ProductoId).Distinct().ToArray();
        return await db.Productos.CountAsync(x => ids.Contains(x.Id), ct) == ids.Length
            ? null
            : "Uno o más productos no existen.";
    }

    private static DetallePedido Map(DetallePedidoInput x) =>
        new()
        {
            ProductoId = x.ProductoId,
            Cantidad = x.Cantidad,
            PrecioUnitario = x.PrecioUnitario,
            Observaciones = x.Observaciones?.Trim(),
        };

    private IQueryable<PedidoDto> Query() =>
        db
            .Pedidos.AsNoTracking()
            .Select(x => new PedidoDto(
                x.Id,
                x.CodigoInterno,
                x.Fecha,
                x.TipoPedido,
                x.Estado,
                x.Observaciones,
                x.ClienteId,
                x.Cliente.Nombres + (x.Cliente.Apellidos == null ? "" : " " + x.Cliente.Apellidos),
                x.Detalles.Select(d => new DetallePedidoDto(
                        d.Id,
                        d.ProductoId,
                        d.Producto.Nombre,
                        d.Cantidad,
                        d.PrecioUnitario,
                        d.Observaciones,
                        d.Cantidad * d.PrecioUnitario
                    ))
                    .ToList(),
                x.Venta == null ? null : x.Venta.Id
            ));
}

public sealed class VentaService(ResellManagerDbContext db) : IVentaService
{
    public async Task<ServiceResult<VentaDto>> RegistrarDesdePedidoAsync(
        VentaInput input,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(input.CodigoInterno) || input.Detalles.Count == 0)
            return ServiceResult<VentaDto>.Failure("Código y detalles son obligatorios.");

        var pedido = await db
            .Pedidos.Include(x => x.Venta)
            .Include(x => x.Detalles)
            .FirstOrDefaultAsync(x => x.Id == input.PedidoId, ct);

        if (pedido is null)
            return ServiceResult<VentaDto>.Failure("Pedido no encontrado.");
        if (pedido.Estado == EstadoPedido.Cancelado)
            return ServiceResult<VentaDto>.Failure(
                "Un pedido cancelado no puede convertirse en venta."
            );
        if (pedido.Venta is not null)
            return ServiceResult<VentaDto>.Failure("El pedido ya tiene una venta.");
        if (await db.Ventas.AnyAsync(x => x.CodigoInterno == input.CodigoInterno.Trim(), ct))
            return ServiceResult<VentaDto>.Failure("El código de venta ya está registrado.");

        var error = await ValidarDetallesVentaAsync(pedido, input.Detalles, ct);
        if (error is not null)
            return ServiceResult<VentaDto>.Failure(error);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var venta = new Venta
        {
            PedidoId = input.PedidoId,
            CodigoInterno = input.CodigoInterno.Trim(),
            Fecha = input.Fecha,
            Estado = EstadoVenta.Registrada,
            Observaciones = input.Observaciones?.Trim(),
        };

        if (pedido.TipoPedido == TipoPedido.Catalogo)
        {
            foreach (var item in input.Detalles)
                venta.Detalles.Add(CrearDetalleCatalogo(item));
        }
        else
        {
            var ids = input.Detalles.Select(x => x.UnidadInventarioId!.Value).ToArray();
            var unidades = await db
                .UnidadesInventario.Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

            foreach (var item in input.Detalles)
            {
                var unidad = unidades[item.UnidadInventarioId!.Value];
                unidad.Estado = EstadoUnidadInventario.Vendida;
                venta.Detalles.Add(CrearDetalleInventario(item, unidad));
            }
        }

        pedido.Estado = EstadoPedido.Completado;
        db.Ventas.Add(venta);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await ObtenerPorIdAsync(venta.Id, ct);
    }

    public async Task<ServiceResult<VentaDto>> AgregarDetalleAsync(
        int ventaId,
        DetalleVentaInput input,
        CancellationToken ct = default
    )
    {
        var venta = await db
            .Ventas.Include(x => x.Pedido)
            .ThenInclude(x => x.Detalles)
            .FirstOrDefaultAsync(x => x.Id == ventaId, ct);

        if (venta is null)
            return ServiceResult<VentaDto>.Failure("Venta no encontrada.");
        if (venta.Estado == EstadoVenta.Cancelada)
            return ServiceResult<VentaDto>.Failure("La venta está cancelada.");

        var error = await ValidarDetallesVentaAsync(venta.Pedido, [input], ct);
        if (error is not null)
            return ServiceResult<VentaDto>.Failure(error);

        if (venta.Pedido.TipoPedido == TipoPedido.Catalogo)
        {
            db.DetallesVenta.Add(CrearDetalleCatalogo(input, ventaId));
        }
        else
        {
            var unidad = await db.UnidadesInventario.FindAsync(
                [input.UnidadInventarioId!.Value],
                ct
            );
            unidad!.Estado = EstadoUnidadInventario.Vendida;
            db.DetallesVenta.Add(CrearDetalleInventario(input, unidad, ventaId));
        }

        await db.SaveChangesAsync(ct);
        return await ObtenerPorIdAsync(ventaId, ct);
    }

    public async Task<ServiceResult<VentaDto>> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default
    )
    {
        var venta = await Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        return venta is null
            ? ServiceResult<VentaDto>.Failure("Venta no encontrada.")
            : ServiceResult<VentaDto>.Ok(venta);
    }

    public async Task<IReadOnlyList<VentaDto>> ListarAsync(CancellationToken ct = default) =>
        await Query()
            .OrderByDescending(x => x.Fecha)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);

    public async Task<ServiceResult<decimal>> CalcularTotalAsync(
        int ventaId,
        CancellationToken ct = default
    )
    {
        if (!await db.Ventas.AnyAsync(x => x.Id == ventaId, ct))
            return ServiceResult<decimal>.Failure("Venta no encontrada.");

        var total =
            await db.DetallesVenta
                .Where(x => x.VentaId == ventaId)
                .SumAsync(x => (decimal?)x.PrecioFinal, ct)
            ?? 0m;

        return ServiceResult<decimal>.Ok(total);
    }

public async Task<ServiceResult> CancelarAsync(int id, CancellationToken ct = default)
    {
        var venta = await db
            .Ventas.Include(x => x.Pedido)
            .Include(x => x.Detalles)
            .ThenInclude(x => x.UnidadInventario)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (venta is null)
            return ServiceResult.Failure("Venta no encontrada.");
        if (venta.Estado == EstadoVenta.Cancelada)
            return ServiceResult.Ok();
        if (
            venta.Detalles.Any(x =>
                x.UnidadInventario?.Estado == EstadoUnidadInventario.Entregada
            )
        )
            return ServiceResult.Failure(
                "La venta incluye unidades entregadas y requiere un proceso de devolución o cambio futuro."
            );

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        venta.Estado = EstadoVenta.Cancelada;
        foreach (
            var unidad in venta.Detalles
                .Where(x => x.UnidadInventario?.Estado == EstadoUnidadInventario.Vendida)
                .Select(x => x.UnidadInventario!)
        )
            unidad.Estado = EstadoUnidadInventario.Disponible;

        venta.Pedido.Estado = EstadoPedido.Pendiente;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ServiceResult.Ok();
    }

    private async Task<string?> ValidarDetallesVentaAsync(
        Pedido pedido,
        IEnumerable<DetalleVentaInput> values,
        CancellationToken ct
    )
    {
        var detalles = values.ToArray();
        if (detalles.Any(x => x.PrecioFinal < 0))
            return "El precio final no puede ser negativo.";

        var productosPedido = pedido.Detalles.Select(x => x.ProductoId).ToHashSet();
        if (pedido.TipoPedido == TipoPedido.Catalogo)
        {
            if (detalles.Any(x => x.UnidadInventarioId.HasValue))
                return "Una venta de catálogo no puede incluir unidades de inventario.";
            if (
                detalles.Any(x =>
                    !x.ProductoId.HasValue
                    || !x.CostoUnitario.HasValue
                    || x.CostoUnitario.Value < 0
                )
            )
                return "Los detalles de catálogo requieren producto y costo unitario válido.";
            if (detalles.Any(x => !productosPedido.Contains(x.ProductoId!.Value)))
                return "El producto vendido debe estar incluido en el pedido.";

            return null;
        }

        if (detalles.Any(x => !x.UnidadInventarioId.HasValue))
            return "Las ventas que no son de catálogo requieren una unidad de inventario por detalle.";

        var ids = detalles.Select(x => x.UnidadInventarioId!.Value).ToArray();
        if (ids.Distinct().Count() != ids.Length)
            return "Una unidad de inventario no puede repetirse en la venta.";

        var unidades = await db
            .UnidadesInventario.Where(x => ids.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.ProductoId,
                x.Costo,
                x.Estado,
                Usada = x.DetallesVenta.Any(d => d.Venta.Estado == EstadoVenta.Registrada),
            })
            .ToListAsync(ct);

        if (unidades.Count != ids.Length)
            return "Una o más unidades de inventario no existen.";
        if (
            unidades.Any(x =>
                x.Usada
                || x.Estado
                    is not (
                        EstadoUnidadInventario.Disponible
                        or EstadoUnidadInventario.Apartada
                    )
            )
        )
            return "Solo se pueden vender unidades disponibles o apartadas no vendidas.";
        if (unidades.Any(x => !productosPedido.Contains(x.ProductoId)))
            return "El producto de cada unidad vendida debe estar incluido en el pedido.";

        var unidadesPorId = unidades.ToDictionary(x => x.Id);
        if (
            detalles.Any(x =>
                x.ProductoId.HasValue
                && x.ProductoId.Value
                    != unidadesPorId[x.UnidadInventarioId!.Value].ProductoId
            )
        )
            return "El producto indicado no corresponde a la unidad de inventario.";
        if (
            detalles.Any(x =>
                x.CostoUnitario.HasValue
                && x.CostoUnitario.Value != unidadesPorId[x.UnidadInventarioId!.Value].Costo
            )
        )
            return "El costo indicado no corresponde a la unidad de inventario.";

        return null;
    }

    private static DetalleVenta CrearDetalleCatalogo(DetalleVentaInput input, int ventaId = 0) =>
        new()
        {
            VentaId = ventaId,
            ProductoId = input.ProductoId!.Value,
            CostoUnitario = input.CostoUnitario!.Value,
            PrecioFinal = input.PrecioFinal,
            Observaciones = input.Observaciones?.Trim(),
        };

    private static DetalleVenta CrearDetalleInventario(
        DetalleVentaInput input,
        UnidadInventario unidad,
        int ventaId = 0
    ) =>
        new()
        {
            VentaId = ventaId,
            UnidadInventarioId = unidad.Id,
            ProductoId = unidad.ProductoId,
            CostoUnitario = unidad.Costo,
            PrecioFinal = input.PrecioFinal,
            Observaciones = input.Observaciones?.Trim(),
        };

    private IQueryable<VentaDto> Query() =>
        db.Ventas.AsNoTracking()
            .Select(x => new VentaDto(
                x.Id,
                x.CodigoInterno,
                x.Fecha,
                x.Estado,
                x.Observaciones,
                x.PedidoId,
                x.Pedido.ClienteId,
                x.Pedido.Cliente.Nombres
                    + (x.Pedido.Cliente.Apellidos == null
                        ? ""
                        : " " + x.Pedido.Cliente.Apellidos),
                x.Detalles.Sum(d => d.PrecioFinal),
                x.Detalles
                    .Select(d => new DetalleVentaDto(
                        d.Id,
                        d.UnidadInventarioId,
                        d.UnidadInventario == null ? null : d.UnidadInventario.CodigoInterno,
                        d.ProductoId ?? d.UnidadInventario!.ProductoId,
                        d.Producto == null
                            ? d.UnidadInventario!.Producto.Nombre
                            : d.Producto.Nombre,
                        d.CostoUnitario ?? d.UnidadInventario!.Costo,
                        d.PrecioFinal,
                        d.Observaciones
                    ))
                    .ToList()
            ));
}

public sealed class PagoService(ResellManagerDbContext db) : IPagoService
{
    public async Task<ServiceResult<PagoDto>> RegistrarAsync(
        PagoInput input,
        CancellationToken ct = default
    )
    {
        if (input.Monto <= 0)
            return ServiceResult<PagoDto>.Failure("El monto debe ser mayor que cero.");
        if (!await db.Clientes.AnyAsync(x => x.Id == input.ClienteId, ct))
            return ServiceResult<PagoDto>.Failure("Cliente no encontrado.");
        var x = new Pago
        {
            ClienteId = input.ClienteId,
            Fecha = input.Fecha,
            Monto = input.Monto,
            MetodoPago = input.MetodoPago,
            Referencia = input.Referencia?.Trim(),
            Observaciones = input.Observaciones?.Trim(),
        };
        db.Pagos.Add(x);
        await db.SaveChangesAsync(ct);
        return await ObtenerPorIdAsync(x.Id, ct);
    }

    public async Task<ServiceResult<PagoDto>> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default
    )
    {
        var x = await Query().FirstOrDefaultAsync(x => x.Id == id, ct);
        return x is null
            ? ServiceResult<PagoDto>.Failure("Pago no encontrado.")
            : ServiceResult<PagoDto>.Ok(x);
    }

    public async Task<IReadOnlyList<PagoDto>> ListarPorClienteAsync(
        int clienteId,
        CancellationToken ct = default
    ) =>
        await Query()
            .Where(x => x.ClienteId == clienteId)
            .OrderByDescending(x => x.Fecha)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);

    private IQueryable<PagoDto> Query() =>
        db
            .Pagos.AsNoTracking()
            .Select(x => new PagoDto(
                x.Id,
                x.ClienteId,
                x.Cliente.Nombres + (x.Cliente.Apellidos == null ? "" : " " + x.Cliente.Apellidos),
                x.Fecha,
                x.Monto,
                x.MetodoPago,
                x.Referencia,
                x.Observaciones
            ));
}

public sealed class DashboardService(ResellManagerDbContext db) : IDashboardService
{
    public async Task<DashboardDto> ObtenerAsync(
        int cantidadRecientes = 5,
        CancellationToken ct = default
    )
    {
        cantidadRecientes = Math.Clamp(cantidadRecientes, 1, 50);
        var ventas =
            await db
                .DetallesVenta.Where(x => x.Venta.Estado == EstadoVenta.Registrada)
                .SumAsync(x => (decimal?)x.PrecioFinal, ct)
            ?? 0m;
        var pagos = await db.Pagos.SumAsync(x => (decimal?)x.Monto, ct) ?? 0m;
        var q = db.UnidadesInventario.Where(x => x.Estado == EstadoUnidadInventario.Disponible);
        var valor = await q.SumAsync(x => (decimal?)x.Costo, ct) ?? 0m;
        var cantidad = await q.CountAsync(ct);
        var pendientes = await db.Pedidos.CountAsync(
            x => x.Estado == EstadoPedido.Pendiente || x.Estado == EstadoPedido.Confirmado,
            ct
        );
        var ultimosPagos = await QueryPagos()
            .OrderByDescending(x => x.Fecha)
            .ThenByDescending(x => x.Id)
            .Take(cantidadRecientes)
            .ToListAsync(ct);
        var ultimasVentas = await QueryVentas()
            .OrderByDescending(x => x.Fecha)
            .ThenByDescending(x => x.Id)
            .Take(cantidadRecientes)
            .ToListAsync(ct);
        return new DashboardDto(
            ventas - pagos,
            valor,
            cantidad,
            pendientes,
            ultimosPagos,
            ultimasVentas
        );
    }

    private IQueryable<PagoDto> QueryPagos() =>
        db
            .Pagos.AsNoTracking()
            .Select(x => new PagoDto(
                x.Id,
                x.ClienteId,
                x.Cliente.Nombres + (x.Cliente.Apellidos == null ? "" : " " + x.Cliente.Apellidos),
                x.Fecha,
                x.Monto,
                x.MetodoPago,
                x.Referencia,
                x.Observaciones
            ));

    private IQueryable<VentaDto> QueryVentas() =>
        db
            .Ventas.AsNoTracking()
            .Select(x => new VentaDto(
                x.Id,
                x.CodigoInterno,
                x.Fecha,
                x.Estado,
                x.Observaciones,
                x.PedidoId,
                x.Pedido.ClienteId,
                x.Pedido.Cliente.Nombres
                    + (x.Pedido.Cliente.Apellidos == null ? "" : " " + x.Pedido.Cliente.Apellidos),
                x.Detalles.Sum(d => d.PrecioFinal),
                x.Detalles.Select(d => new DetalleVentaDto(
                        d.Id,
                        d.UnidadInventarioId,
                        d.UnidadInventario == null ? null : d.UnidadInventario.CodigoInterno,
                        d.ProductoId ?? d.UnidadInventario!.ProductoId,
                        d.Producto == null
                            ? d.UnidadInventario!.Producto.Nombre
                            : d.Producto.Nombre,
                        d.CostoUnitario ?? d.UnidadInventario!.Costo,
                        d.PrecioFinal,
                        d.Observaciones
                    ))
                    .ToList()
            ));
}

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
        var x = await Query(db.Pedidos.Where(x => x.Id == id)).FirstOrDefaultAsync(ct);
        return x is null
            ? ServiceResult<PedidoDto>.Failure("Pedido no encontrado.")
            : ServiceResult<PedidoDto>.Ok(x);
    }

    public async Task<IReadOnlyList<PedidoDto>> ListarAsync(CancellationToken ct = default) =>
        await Query(
                db.Pedidos.OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Id)
            )
            .ToListAsync(ct);

    public async Task<ServiceResult> CancelarAsync(int id, CancellationToken ct = default)
    {
        var x = await db
            .Pedidos.Include(p => p.Venta)
            .Include(p => p.Detalles)
                .ThenInclude(d => d.UnidadesReservadas)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
        if (x is null)
            return ServiceResult.Failure("Pedido no encontrado.");
        if (x.Venta is { Estado: EstadoVenta.Registrada })
            return ServiceResult.Failure("El pedido tiene una venta registrada.");

        foreach (var unidad in x.Detalles.SelectMany(d => d.UnidadesReservadas))
            unidad.DetallePedidoReservaId = null;

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

    private static IQueryable<PedidoDto> Query(IQueryable<Pedido> source) =>
        source.AsNoTracking()
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
                unidad.DetallePedidoReservaId = null;
                venta.Detalles.Add(CrearDetalleInventario(item, unidad));
            }
        }

        pedido.Estado = EstadoPedido.Completado;
        db.Ventas.Add(venta);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await ObtenerPorIdAsync(venta.Id, ct);
    }

    public async Task<ServiceResult<VentaDto>> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default
    )
    {
        var venta = await VentaCompleta().FirstOrDefaultAsync(x => x.Id == id, ct);
        return venta is null
            ? ServiceResult<VentaDto>.Failure("Venta no encontrada.")
            : ServiceResult<VentaDto>.Ok(Map(venta));
    }

    public async Task<IReadOnlyList<VentaDto>> ListarAsync(CancellationToken ct = default)
    {
        var ventas = await VentaCompleta()
            .OrderByDescending(x => x.Fecha)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
        return ventas.Select(Map).ToList();
    }

    public async Task<ServiceResult<decimal>> CalcularTotalAsync(
        int ventaId,
        CancellationToken ct = default
    )
    {
        if (!await db.Ventas.AnyAsync(x => x.Id == ventaId, ct))
            return ServiceResult<decimal>.Failure("Venta no encontrada.");

        var importes = await db
            .DetallesVenta.AsNoTracking()
            .Where(x => x.VentaId == ventaId)
            .Select(x => x.PrecioFinal)
            .ToListAsync(ct);

        return ServiceResult<decimal>.Ok(importes.Sum());
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
        if (venta.Detalles.Any(x => x.UnidadInventario?.Estado == EstadoUnidadInventario.Entregada))
            return ServiceResult.Failure(
                "La venta incluye unidades entregadas y requiere un proceso de devolución o cambio futuro."
            );

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var otrasVentas = await db
            .DetallesVenta.AsNoTracking()
            .Where(x =>
                x.VentaId != id
                && x.Venta.Estado == EstadoVenta.Registrada
                && x.Venta.Pedido.ClienteId == venta.Pedido.ClienteId
            )
            .Select(x => x.PrecioFinal)
            .ToListAsync(ct);
        var pagos = await db
            .Pagos.AsNoTracking()
            .Where(x => x.ClienteId == venta.Pedido.ClienteId)
            .Select(x => x.Monto)
            .ToListAsync(ct);

        if (otrasVentas.Sum() - pagos.Sum() < 0)
        {
            await transaction.RollbackAsync(ct);
            return ServiceResult.Failure(
                "No se puede cancelar la venta porque existen pagos que deben ajustarse o devolverse primero."
            );
        }

        venta.Estado = EstadoVenta.Cancelada;
        foreach (
            var unidad in venta
                .Detalles.Where(x => x.UnidadInventario?.Estado == EstadoUnidadInventario.Vendida)
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

        var cantidadesPedido = pedido
            .Detalles.GroupBy(x => x.ProductoId)
            .ToDictionary(x => x.Key, x => x.Sum(d => d.Cantidad));

        if (pedido.TipoPedido == TipoPedido.Catalogo)
        {
            if (detalles.Any(x => x.UnidadInventarioId.HasValue))
                return "Una venta de catálogo no puede incluir unidades de inventario.";
            if (
                detalles.Any(x =>
                    !x.ProductoId.HasValue || !x.CostoUnitario.HasValue || x.CostoUnitario.Value < 0
                )
            )
                return "Los detalles de catálogo requieren producto y costo unitario válido.";

            var cantidadesVenta = detalles
                .GroupBy(x => x.ProductoId!.Value)
                .ToDictionary(x => x.Key, x => x.Count());
            if (!CantidadesCoinciden(cantidadesPedido, cantidadesVenta))
                return "Las cantidades vendidas por producto deben coincidir exactamente con las cantidades del pedido.";

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
                ReservaPedidoId =
                    x.DetallePedidoReserva == null
                        ? null
                        : (int?)x.DetallePedidoReserva.PedidoId,
                Usada = x.DetallesVenta.Any(d => d.Venta.Estado == EstadoVenta.Registrada),
            })
            .ToListAsync(ct);

        if (unidades.Count != ids.Length)
            return "Una o más unidades de inventario no existen.";
        if (unidades.Any(x => x.ReservaPedidoId.HasValue && x.ReservaPedidoId != pedido.Id))
            return "Una unidad está reservada para un pedido distinto y no puede venderse en esta venta.";
        if (unidades.Any(x => x.Usada || x.Estado != EstadoUnidadInventario.Disponible))
            return "Solo se pueden vender unidades disponibles que no pertenezcan a otra venta activa.";

        var unidadesPorId = unidades.ToDictionary(x => x.Id);
        if (
            detalles.Any(x =>
                x.ProductoId.HasValue
                && x.ProductoId.Value != unidadesPorId[x.UnidadInventarioId!.Value].ProductoId
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

        var cantidadesVentaInventario = unidades
            .GroupBy(x => x.ProductoId)
            .ToDictionary(x => x.Key, x => x.Count());
        if (!CantidadesCoinciden(cantidadesPedido, cantidadesVentaInventario))
            return "Las cantidades vendidas por producto deben coincidir exactamente con las cantidades del pedido.";

        return null;
    }

    private static bool CantidadesCoinciden(
        IReadOnlyDictionary<int, int> cantidadesPedido,
        IReadOnlyDictionary<int, int> cantidadesVenta
    ) =>
        cantidadesPedido.Count == cantidadesVenta.Count
        && cantidadesPedido.All(x =>
            cantidadesVenta.TryGetValue(x.Key, out var cantidad) && cantidad == x.Value
        );

    private static DetalleVenta CrearDetalleCatalogo(DetalleVentaInput input) =>
        new()
        {
            ProductoId = input.ProductoId!.Value,
            CostoUnitario = input.CostoUnitario!.Value,
            PrecioFinal = input.PrecioFinal,
            Observaciones = input.Observaciones?.Trim(),
        };

    private static DetalleVenta CrearDetalleInventario(
        DetalleVentaInput input,
        UnidadInventario unidad
    ) =>
        new()
        {
            UnidadInventarioId = unidad.Id,
            ProductoId = unidad.ProductoId,
            CostoUnitario = unidad.Costo,
            PrecioFinal = input.PrecioFinal,
            Observaciones = input.Observaciones?.Trim(),
        };

    private IQueryable<Venta> VentaCompleta() =>
        db
            .Ventas.AsNoTracking()
            .Include(x => x.Pedido)
                .ThenInclude(x => x.Cliente)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.Producto)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.UnidadInventario)
                    .ThenInclude(x => x!.Producto);

    private static VentaDto Map(Venta x) =>
        new(
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
                    d.UnidadInventario?.CodigoInterno,
                    d.ProductoId ?? d.UnidadInventario!.ProductoId,
                    d.Producto?.Nombre ?? d.UnidadInventario!.Producto.Nombre,
                    d.CostoUnitario ?? d.UnidadInventario!.Costo,
                    d.PrecioFinal,
                    d.Observaciones
                ))
                .ToList()
        );
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

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var saldoActual = await ObtenerSaldoActualAsync(input.ClienteId, ct);

        if (saldoActual <= 0)
            return ServiceResult<PagoDto>.Failure("El cliente no tiene deuda pendiente.");
        if (input.Monto > saldoActual)
            return ServiceResult<PagoDto>.Failure(
                $"El monto del pago supera la deuda actual del cliente de {saldoActual:0.00}."
            );

        var pago = new Pago
        {
            ClienteId = input.ClienteId,
            Fecha = input.Fecha,
            Monto = input.Monto,
            MetodoPago = input.MetodoPago,
            Referencia = input.Referencia?.Trim(),
            Observaciones = input.Observaciones?.Trim(),
        };

        db.Pagos.Add(pago);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await ObtenerPorIdAsync(pago.Id, ct);
    }

    public async Task<ServiceResult<PagoDto>> ObtenerPorIdAsync(
        int id,
        CancellationToken ct = default
    )
    {
        var x = await Query(db.Pagos.Where(x => x.Id == id)).FirstOrDefaultAsync(ct);
        return x is null
            ? ServiceResult<PagoDto>.Failure("Pago no encontrado.")
            : ServiceResult<PagoDto>.Ok(x);
    }

    public async Task<IReadOnlyList<PagoDto>> ListarPorClienteAsync(
        int clienteId,
        CancellationToken ct = default
    ) =>
        await Query(
                db.Pagos.Where(x => x.ClienteId == clienteId)
                    .OrderByDescending(x => x.Fecha)
                    .ThenByDescending(x => x.Id)
            )
            .ToListAsync(ct);

    private async Task<decimal> ObtenerSaldoActualAsync(int clienteId, CancellationToken ct)
    {
        var ventas = await db
            .DetallesVenta.AsNoTracking()
            .Where(x =>
                x.Venta.Estado == EstadoVenta.Registrada && x.Venta.Pedido.ClienteId == clienteId
            )
            .Select(x => x.PrecioFinal)
            .ToListAsync(ct);
        var pagos = await db
            .Pagos.AsNoTracking()
            .Where(x => x.ClienteId == clienteId)
            .Select(x => x.Monto)
            .ToListAsync(ct);

        return ventas.Sum() - pagos.Sum();
    }

    private static IQueryable<PagoDto> Query(IQueryable<Pago> source) =>
        source
            .AsNoTracking()
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
        var importesVentas = await db
            .DetallesVenta.AsNoTracking()
            .Where(x => x.Venta.Estado == EstadoVenta.Registrada)
            .Select(x => x.PrecioFinal)
            .ToListAsync(ct);
        var importesPagos = await db.Pagos.AsNoTracking().Select(x => x.Monto).ToListAsync(ct);
        var costosDisponibles = await db
            .UnidadesInventario.AsNoTracking()
            .Where(x => x.Estado == EstadoUnidadInventario.Disponible)
            .Select(x => x.Costo)
            .ToListAsync(ct);
        var ventas = importesVentas.Sum();
        var pagos = importesPagos.Sum();
        var valor = costosDisponibles.Sum();
        var cantidad = costosDisponibles.Count;
        var pendientes = await db.Pedidos.CountAsync(
            x => x.Estado == EstadoPedido.Pendiente || x.Estado == EstadoPedido.Confirmado,
            ct
        );
        var ultimosPagos = await QueryPagos(
                db.Pagos.OrderByDescending(x => x.Fecha)
                    .ThenByDescending(x => x.Id)
                    .Take(cantidadRecientes)
            )
            .ToListAsync(ct);
        var ultimasVentasEntities = await db
            .Ventas.AsNoTracking()
            .Include(x => x.Pedido)
                .ThenInclude(x => x.Cliente)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.Producto)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.UnidadInventario)
                    .ThenInclude(x => x!.Producto)
            .OrderByDescending(x => x.Fecha)
            .ThenByDescending(x => x.Id)
            .Take(cantidadRecientes)
            .ToListAsync(ct);
        var ultimasVentas = ultimasVentasEntities.Select(MapVenta).ToList();
        return new DashboardDto(
            ventas - pagos,
            valor,
            cantidad,
            pendientes,
            ultimosPagos,
            ultimasVentas
        );
    }

    public async Task<ServiceResult<decimal>> ObtenerUtilidadAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default
    )
    {
        if (desde > hasta)
            return ServiceResult<decimal>.Failure(
                "La fecha inicial no puede ser posterior a la fecha final."
            );

        var detalles = await db
            .DetallesVenta.AsNoTracking()
            .Where(x =>
                x.Venta.Estado == EstadoVenta.Registrada
                && x.Venta.Fecha >= desde
                && x.Venta.Fecha <= hasta
            )
            .Select(x => new
            {
                x.PrecioFinal,
                Costo = x.CostoUnitario
                    ?? (x.UnidadInventario == null ? 0m : x.UnidadInventario.Costo),
            })
            .ToListAsync(ct);

        return ServiceResult<decimal>.Ok(detalles.Sum(x => x.PrecioFinal - x.Costo));
    }

    private static IQueryable<PagoDto> QueryPagos(IQueryable<Pago> source) =>
        source
            .AsNoTracking()
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

    private static VentaDto MapVenta(Venta x) =>
        new(
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
                    d.UnidadInventario?.CodigoInterno,
                    d.ProductoId ?? d.UnidadInventario!.ProductoId,
                    d.Producto?.Nombre ?? d.UnidadInventario!.Producto.Nombre,
                    d.CostoUnitario ?? d.UnidadInventario!.Costo,
                    d.PrecioFinal,
                    d.Observaciones
                ))
                .ToList()
        );
}

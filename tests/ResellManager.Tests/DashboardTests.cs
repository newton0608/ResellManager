using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class DashboardTests
{
    [Fact]
    public async Task TotalAdeudado_AgregaClientesVentasRegistradasYPagos()
    {
        await using var test = await TestDatabase.CreateAsync();
        var segundoCliente = new Cliente
        {
            Nombres = "Segundo cliente",
            Telefono = "555-0200",
        };
        test.Db.Clientes.Add(segundoCliente);
        await test.Db.SaveChangesAsync();

        await CrearVentaAsync(
            test,
            test.Cliente,
            "DEUDA-A",
            new DateOnly(2026, 2, 1),
            CanalVenta.Facebook,
            150m,
            50m
        );
        var cancelada = await CrearVentaAsync(
            test,
            test.Cliente,
            "DEUDA-CANCELADA",
            new DateOnly(2026, 2, 2),
            CanalVenta.WhatsApp,
            70m,
            20m
        );
        cancelada.Estado = EstadoVenta.Cancelada;
        await CrearVentaAsync(
            test,
            segundoCliente,
            "DEUDA-B",
            new DateOnly(2026, 2, 3),
            CanalVenta.Presencial,
            80m,
            30m
        );
        await test.Db.SaveChangesAsync();

        var pagos = new PagoService(test.Db);
        var pagoA = await pagos.RegistrarAsync(
            new PagoInput(
                test.Cliente.Id,
                new DateOnly(2026, 2, 4),
                30m,
                MetodoPago.Efectivo,
                null,
                null
            )
        );
        var pagoB = await pagos.RegistrarAsync(
            new PagoInput(
                segundoCliente.Id,
                new DateOnly(2026, 2, 4),
                20m,
                MetodoPago.Transferencia,
                null,
                null
            )
        );

        var dashboard = await new DashboardService(test.Db).ObtenerAsync();

        Assert.True(pagoA.IsSuccess, pagoA.ErrorMessage);
        Assert.True(pagoB.IsSuccess, pagoB.ErrorMessage);
        Assert.Equal(180m, dashboard.TotalAdeudado);
    }

    [Fact]
    public async Task InventarioDisponible_SoloCuentaYSumaEstadoDisponible()
    {
        await using var test = await TestDatabase.CreateAsync();
        var compra = await new CompraService(test.Db).RegistrarAsync(
            test.Compra(
                OrigenCompra.CompraLocal,
                "DASH-INVENTARIO",
                new DateOnly(2026, 1, 11),
                cantidad: 5
            )
        );
        Assert.True(compra.IsSuccess, compra.ErrorMessage);

        var unidades = await test.Db.UnidadesInventario.OrderBy(x => x.Id).ToListAsync();
        unidades[1].Estado = EstadoUnidadInventario.Comprada;
        unidades[2].Estado = EstadoUnidadInventario.EnTransito;
        unidades[3].Estado = EstadoUnidadInventario.Vendida;
        unidades[4].Estado = EstadoUnidadInventario.Entregada;
        await test.Db.SaveChangesAsync();

        var dashboard = await new DashboardService(test.Db).ObtenerAsync();

        Assert.Equal(1, dashboard.UnidadesDisponibles);
        Assert.Equal(40m, dashboard.ValorInventarioDisponible);
    }

    [Fact]
    public async Task PedidosActivos_IncluyePendienteYConfirmadoExcluyeTerminales()
    {
        await using var test = await TestDatabase.CreateAsync();
        var pendiente = await test.CrearPedidoAsync(TipoPedido.Catalogo, "DASH-PENDIENTE");
        var confirmado = await test.CrearPedidoAsync(TipoPedido.Catalogo, "DASH-CONFIRMADO");
        var cancelado = await test.CrearPedidoAsync(TipoPedido.Catalogo, "DASH-CANCELADO");
        var completado = await test.CrearPedidoAsync(TipoPedido.Catalogo, "DASH-COMPLETADO");
        pendiente.Estado = EstadoPedido.Pendiente;
        confirmado.Estado = EstadoPedido.Confirmado;
        cancelado.Estado = EstadoPedido.Cancelado;
        completado.Estado = EstadoPedido.Completado;
        await test.Db.SaveChangesAsync();

        var dashboard = await new DashboardService(test.Db).ObtenerAsync();

        Assert.Equal(2, dashboard.PedidosActivos);
    }

    [Fact]
    public async Task UltimosPagos_RespetaLimiteYOrdenFechaLuegoId()
    {
        await using var test = await TestDatabase.CreateAsync();
        var fecha = new DateOnly(2026, 3, 5);
        var primero = new Pago
        {
            ClienteId = test.Cliente.Id,
            Fecha = fecha,
            Monto = 10m,
            MetodoPago = MetodoPago.Efectivo,
        };
        var segundo = new Pago
        {
            ClienteId = test.Cliente.Id,
            Fecha = fecha,
            Monto = 20m,
            MetodoPago = MetodoPago.Transferencia,
        };
        var anterior = new Pago
        {
            ClienteId = test.Cliente.Id,
            Fecha = fecha.AddDays(-1),
            Monto = 30m,
            MetodoPago = MetodoPago.Tarjeta,
        };
        test.Db.Pagos.AddRange(primero, segundo, anterior);
        await test.Db.SaveChangesAsync();

        var servicio = new DashboardService(test.Db);
        var dashboard = await servicio.ObtenerAsync(2);
        var sinRecientes = await servicio.ObtenerAsync(0);

        Assert.Equal([segundo.Id, primero.Id], dashboard.UltimosPagos.Select(x => x.Id));
        Assert.Empty(sinRecientes.UltimosPagos);
        Assert.Empty(sinRecientes.UltimasVentas);
    }

    [Fact]
    public async Task UltimasVentas_ExcluyeCanceladasYOrdenaFechaLuegoId()
    {
        await using var test = await TestDatabase.CreateAsync();
        var fecha = new DateOnly(2026, 4, 10);
        var primera = await CrearVentaAsync(
            test,
            test.Cliente,
            "RECIENTE-A",
            fecha,
            CanalVenta.Facebook,
            100m,
            40m
        );
        var segunda = await CrearVentaAsync(
            test,
            test.Cliente,
            "RECIENTE-B",
            fecha,
            CanalVenta.WhatsApp,
            110m,
            45m
        );
        await CrearVentaAsync(
            test,
            test.Cliente,
            "RECIENTE-ANTERIOR",
            fecha.AddDays(-1),
            CanalVenta.Otro,
            90m,
            30m
        );
        var cancelada = await CrearVentaAsync(
            test,
            test.Cliente,
            "RECIENTE-CANCELADA",
            fecha.AddDays(1),
            CanalVenta.Presencial,
            500m,
            10m
        );
        cancelada.Estado = EstadoVenta.Cancelada;
        await test.Db.SaveChangesAsync();

        var dashboard = await new DashboardService(test.Db).ObtenerAsync(2);

        Assert.Equal([segunda.Id, primera.Id], dashboard.UltimasVentas.Select(x => x.Id));
        Assert.Equal([CanalVenta.WhatsApp, CanalVenta.Facebook], dashboard.UltimasVentas.Select(x => x.Canal));
        Assert.DoesNotContain(dashboard.UltimasVentas, x => x.Id == cancelada.Id);
    }

    [Fact]
    public async Task Canales_MuestraTodosYAplicaFiltrosDePedidoYVenta()
    {
        await using var test = await TestDatabase.CreateAsync();
        await CrearVentaAsync(
            test,
            test.Cliente,
            "CANAL-FACEBOOK",
            new DateOnly(2026, 5, 1),
            CanalVenta.Facebook,
            100m,
            40m
        );
        await CrearVentaAsync(
            test,
            test.Cliente,
            "CANAL-WHATSAPP",
            new DateOnly(2026, 5, 2),
            CanalVenta.WhatsApp,
            70m,
            30m
        );
        var ventaCancelada = await CrearVentaAsync(
            test,
            test.Cliente,
            "CANAL-PRESENCIAL-CANCELADA",
            new DateOnly(2026, 5, 3),
            CanalVenta.Presencial,
            300m,
            20m
        );
        ventaCancelada.Estado = EstadoVenta.Cancelada;
        var pedidoCancelado = await test.CrearPedidoAsync(TipoPedido.Catalogo, "CANAL-PEDIDO-CANCELADO");
        pedidoCancelado.CanalVenta = CanalVenta.Facebook;
        pedidoCancelado.Estado = EstadoPedido.Cancelado;
        await test.Db.SaveChangesAsync();

        var dashboard = await new DashboardService(test.Db).ObtenerAsync();
        var canales = dashboard.Canales.ToDictionary(x => x.Canal);

        Assert.Equal(Enum.GetValues<CanalVenta>(), dashboard.Canales.Select(x => x.Canal));
        Assert.Equal(new ResumenCanalVentaDto(CanalVenta.Facebook, 1, 1, 100m), canales[CanalVenta.Facebook]);
        Assert.Equal(new ResumenCanalVentaDto(CanalVenta.WhatsApp, 1, 1, 70m), canales[CanalVenta.WhatsApp]);
        Assert.Equal(new ResumenCanalVentaDto(CanalVenta.Presencial, 1, 0, 0m), canales[CanalVenta.Presencial]);
        Assert.Equal(new ResumenCanalVentaDto(CanalVenta.Web, 0, 0, 0m), canales[CanalVenta.Web]);
        Assert.Equal(new ResumenCanalVentaDto(CanalVenta.Otro, 0, 0, 0m), canales[CanalVenta.Otro]);
    }

    [Fact]
    public async Task Utilidad_FiltraPeriodoInclusivoYSoloVentasRegistradas()
    {
        await using var test = await TestDatabase.CreateAsync();
        await CrearVentaAsync(
            test,
            test.Cliente,
            "UTILIDAD-DESDE",
            new DateOnly(2026, 6, 1),
            CanalVenta.Web,
            100m,
            40m
        );
        await CrearVentaAsync(
            test,
            test.Cliente,
            "UTILIDAD-HASTA",
            new DateOnly(2026, 6, 30),
            CanalVenta.Web,
            80m,
            25m
        );
        await CrearVentaAsync(
            test,
            test.Cliente,
            "UTILIDAD-FUERA",
            new DateOnly(2026, 7, 1),
            CanalVenta.Web,
            1000m,
            1m
        );
        var cancelada = await CrearVentaAsync(
            test,
            test.Cliente,
            "UTILIDAD-CANCELADA",
            new DateOnly(2026, 6, 15),
            CanalVenta.Web,
            500m,
            10m
        );
        cancelada.Estado = EstadoVenta.Cancelada;
        await test.Db.SaveChangesAsync();

        var resultado = await new DashboardService(test.Db).ObtenerUtilidadAsync(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30)
        );

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal(115m, resultado.Value);
    }

    [Fact]
    public async Task Utilidad_RangoInvalidoFalla()
    {
        await using var test = await TestDatabase.CreateAsync();

        var resultado = await new DashboardService(test.Db).ObtenerUtilidadAsync(
            new DateOnly(2026, 8, 2),
            new DateOnly(2026, 8, 1)
        );

        Assert.False(resultado.IsSuccess);
        Assert.Contains("fecha inicial", resultado.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Utilidad_SinVentasEsCero()
    {
        await using var test = await TestDatabase.CreateAsync();

        var resultado = await new DashboardService(test.Db).ObtenerUtilidadAsync(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30)
        );

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal(0m, resultado.Value);
    }

    private static async Task<Venta> CrearVentaAsync(
        TestDatabase test,
        Cliente cliente,
        string sufijo,
        DateOnly fecha,
        CanalVenta canal,
        decimal precio,
        decimal costo
    )
    {
        var pedido = new Pedido
        {
            CodigoInterno = $"PED-{sufijo}",
            Fecha = fecha,
            TipoPedido = TipoPedido.Catalogo,
            CanalVenta = canal,
            Estado = EstadoPedido.Pendiente,
            ClienteId = cliente.Id,
            Detalles =
            [
                new DetallePedido
                {
                    ProductoId = test.Producto.Id,
                    Cantidad = 1,
                    PrecioUnitario = precio,
                },
            ],
        };
        test.Db.Pedidos.Add(pedido);
        await test.Db.SaveChangesAsync();

        var resultado = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new VentaInput(
                pedido.Id,
                $"VEN-{sufijo}",
                fecha,
                null,
                [new DetalleVentaInput(null, test.Producto.Id, costo, precio, null)]
            )
        );
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);

        return await test.Db.Ventas.Include(x => x.Detalles)
            .SingleAsync(x => x.Id == resultado.Value!.Id);
    }
}

public sealed class DashboardUiTests
{
    [Fact]
    public void Home_UsaDashboardBackendYMuestraSeccionesRealesResponsive()
    {
        var raiz = BuscarRaizRepositorio();
        var home = File.ReadAllText(
            Path.Combine(raiz, "src", "ResellManager.Web", "Components", "Pages", "Home.razor")
        );

        Assert.Contains("IDashboardService", home, StringComparison.Ordinal);
        Assert.Contains("Resumen del negocio", home, StringComparison.Ordinal);
        Assert.Contains("Total adeudado", home, StringComparison.Ordinal);
        Assert.Contains("Inventario disponible", home, StringComparison.Ordinal);
        Assert.Contains("Pedidos activos", home, StringComparison.Ordinal);
        Assert.Contains("Utilidad por periodo", home, StringComparison.Ordinal);
        Assert.Contains("Ventas por canal", home, StringComparison.Ordinal);
        Assert.Contains("Últimos pagos", home, StringComparison.Ordinal);
        Assert.Contains("Últimas ventas", home, StringComparison.Ordinal);
        Assert.Contains("type=\"date\"", home, StringComparison.Ordinal);
        Assert.Contains("desktop-table", home, StringComparison.Ordinal);
        Assert.Contains("mobile-card-list", home, StringComparison.Ordinal);
        Assert.DoesNotContain("Panel en preparación", home, StringComparison.Ordinal);
        Assert.DoesNotContain("subió", home, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DbContext", home, StringComparison.Ordinal);
    }

    [Fact]
    public void Dashboard_NoParalelizaDbContextYTieneEstilosMoviles()
    {
        var raiz = BuscarRaizRepositorio();
        var servicio = File.ReadAllText(
            Path.Combine(
                raiz,
                "src",
                "ResellManager.Infrastructure",
                "Services",
                "OperacionServices.cs"
            )
        );
        var css = File.ReadAllText(
            Path.Combine(raiz, "src", "ResellManager.Web", "wwwroot", "app.css")
        );

        Assert.DoesNotContain("Task.WhenAll", servicio, StringComparison.Ordinal);
        Assert.Contains("dashboard-metrics", css, StringComparison.Ordinal);
        Assert.Contains("dashboard-period-form", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 47.99rem)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 30rem)", css, StringComparison.Ordinal);
    }

    private static string BuscarRaizRepositorio()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (
            directorio is not null
            && !File.Exists(Path.Combine(directorio.FullName, "ResellManager.sln"))
        )
            directorio = directorio.Parent;

        return directorio?.FullName
            ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }
}

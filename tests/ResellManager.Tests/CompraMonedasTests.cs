using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class CompraMonedasTests
{
    [Fact]
    public void Monedas_TienenValoresExplicitosYSoloGtqUsd()
    {
        Assert.Equal([MonedaCompra.GTQ, MonedaCompra.USD], Enum.GetValues<MonedaCompra>());
        Assert.Equal(0, (int)MonedaCompra.GTQ);
        Assert.Equal(1, (int)MonedaCompra.USD);
    }

    [Fact]
    public async Task InputCompatibleAnterior_SinMonedaExplicitaContinuaRegistrandoGtq()
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = test.Compra(OrigenCompra.CompraLocal, "COMPRA-INPUT-ANTERIOR", new(2026, 1, 11));
        Assert.Equal(MonedaCompra.GTQ, input.Moneda);
        Assert.Equal(1m, input.TipoCambio);
        var resultado = await new CompraService(test.Db).RegistrarAsync(input);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal(40m, resultado.Value!.Total);
        Assert.Equal(40m, resultado.Value.TotalMonedaOrigen);
        Assert.Equal(40m, resultado.Value.Detalles.Single().CostoUnitarioMonedaOrigen);
        Assert.Equal(40m, (await test.Db.UnidadesInventario.SingleAsync()).Costo);
    }

    [Theory]
    [InlineData(OrigenCompra.CompraLocal)]
    [InlineData(OrigenCompra.Importacion)]
    [InlineData(OrigenCompra.EnvioHermano)]
    [InlineData(OrigenCompra.Catalogo)]
    public async Task Gtq_PorDefectoMantieneCostosYEsIndependienteDelOrigen(OrigenCompra origen)
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = Input(test, origen, MonedaCompra.GTQ, 1m, 50m, 2);
        var resultado = await new CompraService(test.Db).RegistrarAsync(input);

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        var compra = resultado.Value!;
        Assert.Equal(MonedaCompra.GTQ, compra.Moneda);
        Assert.Equal(1m, compra.TipoCambio);
        Assert.Equal(100m, compra.Total);
        Assert.Equal(100m, compra.TotalMonedaOrigen);
        var detalle = Assert.Single(compra.Detalles);
        Assert.Equal(50m, detalle.CostoUnitario);
        Assert.Equal(50m, detalle.CostoUnitarioMonedaOrigen);
        Assert.Equal(100m, detalle.SubtotalMonedaOrigen);
        var unidades = await test.Db.UnidadesInventario.ToListAsync();
        Assert.Equal(origen == OrigenCompra.Catalogo ? 0 : 2, unidades.Count);
        Assert.All(unidades, unidad => Assert.Equal(50m, unidad.Costo));
    }

    [Fact]
    public async Task Usd_ConvierteCadaUnidadAntesDeSumarYConservaOrigenEnDtoPersistenciaYLista()
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = Input(test, OrigenCompra.CompraLocal, MonedaCompra.USD, 7.64136m, 12.50m, 2)
            with { Detalles = [new(test.Producto.Id, 2, 12.50m), new(test.Producto.Id, 3, 0.01m)] };
        var service = new CompraService(test.Db);
        var resultado = await service.RegistrarAsync(input);

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        var compra = resultado.Value!;
        Assert.Equal(MonedaCompra.USD, compra.Moneda);
        Assert.Equal(25.03m, compra.TotalMonedaOrigen);
        Assert.Equal(191.28m, compra.Total); // 2 * 95.52 + 3 * 0.08
        Assert.NotEqual(decimal.Round(compra.TotalMonedaOrigen * input.TipoCambio, 2,
            MidpointRounding.AwayFromZero), compra.Total);
        var detalle = compra.Detalles.Single(x => x.Cantidad == 2);
        Assert.Equal(12.50m, detalle.CostoUnitarioMonedaOrigen);
        Assert.Equal(95.52m, detalle.CostoUnitario);
        Assert.Equal(25m, detalle.SubtotalMonedaOrigen);
        Assert.Equal(191.04m, detalle.Subtotal);
        var persistida = await test.Db.Compras.AsNoTracking().Include(x => x.Detalles)
            .ThenInclude(x => x.UnidadesInventario).SingleAsync();
        Assert.Equal(7.64136m, persistida.TipoCambio);
        Assert.Equal(compra.Total, persistida.Detalles.Sum(x => x.UnidadesInventario.Sum(u => u.Costo)));
        Assert.All(persistida.Detalles, d => Assert.All(d.UnidadesInventario,
            u => Assert.Equal(d.CostoUnitario, u.Costo)));
        var listado = Assert.Single(await service.ListarAsync());
        Assert.Equal(compra.Total, listado.Total);
        Assert.Equal(compra.TotalMonedaOrigen, listado.TotalMonedaOrigen);
        Assert.Equal(compra.Moneda, listado.Moneda);
        Assert.Equal(compra.TipoCambio, listado.TipoCambio);
        Assert.Equal(compra.Detalles.Select(x => x.CostoUnitarioMonedaOrigen),
            listado.Detalles.Select(x => x.CostoUnitarioMonedaOrigen));
    }

    [Theory]
    [InlineData("12.50", "7.67", "95.88")]
    [InlineData("0.01", "1.5", "0.02")]
    [InlineData("0.01", "2.5", "0.03")]
    [InlineData("0", "7.67", "0")]
    [InlineData("12.50", "7.64136", "95.52")]
    public void Conversion_UsaDecimalYRedondeaMidpointAlejandoseDeCero(string origen, string tipo, string esperado)
    {
        Assert.Equal(Decimal(esperado), ConversionMonedaCompra.CostoUnitarioGtq(Decimal(origen), Decimal(tipo)));
    }

    [Theory]
    [InlineData(OrigenCompra.CompraLocal)]
    [InlineData(OrigenCompra.Importacion)]
    [InlineData(OrigenCompra.EnvioHermano)]
    public async Task Usd_GeneraInventarioEnGtqSinReglaDeMonedaPorOrigen(OrigenCompra origen)
    {
        await using var test = await TestDatabase.CreateAsync();
        var resultado = await new CompraService(test.Db).RegistrarAsync(
            Input(test, origen, MonedaCompra.USD, 7.67m));
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal(95.88m, (await test.Db.UnidadesInventario.SingleAsync()).Costo);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Usd_CostoGtqSeUsaEnVentaUtilidadSaldoPagosYDashboard(bool catalogo)
    {
        await using var test = await TestDatabase.CreateAsync();
        var compra = await new CompraService(test.Db).RegistrarAsync(Input(test,
            catalogo ? OrigenCompra.Catalogo : OrigenCompra.CompraLocal, MonedaCompra.USD, 7.67m));
        Assert.True(compra.IsSuccess, compra.ErrorMessage);
        var costo = compra.Value!.Detalles.Single().CostoUnitario;
        Assert.Equal(95.88m, costo);
        var dashboard = new DashboardService(test.Db);
        Assert.Equal(catalogo ? 0m : 95.88m, (await dashboard.ObtenerAsync()).ValorInventarioDisponible);
        var unidades = await test.Db.UnidadesInventario.ToListAsync();
        Assert.Equal(catalogo ? 0 : 1, unidades.Count);
        var pedido = await test.CrearPedidoAsync(catalogo ? TipoPedido.Catalogo : TipoPedido.VentaDirecta, "PED-USD");
        var fechaVenta = new DateOnly(2026, 2, 2);
        var detalleVenta = catalogo
            ? new DetalleVentaInput(null, test.Producto.Id, costo, 175m, null)
            : new DetalleVentaInput(unidades.Single().Id, null, null, 175m, null);
        var venta = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new VentaInput(pedido.Id, "VEN-USD", fechaVenta, null, [detalleVenta]));
        Assert.True(venta.IsSuccess, venta.ErrorMessage);
        Assert.Equal(175m, venta.Value!.Total);
        Assert.Equal(95.88m, venta.Value.Detalles.Single().CostoUnitario);
        var utilidad = await dashboard.ObtenerUtilidadAsync(fechaVenta, fechaVenta);
        Assert.True(utilidad.IsSuccess, utilidad.ErrorMessage);
        Assert.Equal(79.12m, utilidad.Value);
        Assert.Equal(175m, (await dashboard.ObtenerAsync()).TotalAdeudado);
        var pago = await new PagoService(test.Db).RegistrarAsync(
            new PagoInput(test.Cliente.Id, fechaVenta, 50m, MetodoPago.Efectivo, null, null));
        Assert.True(pago.IsSuccess, pago.ErrorMessage);
        Assert.Equal(125m, (await dashboard.ObtenerAsync()).TotalAdeudado);
    }

    [Fact]
    public async Task Usd_UsaTipoAplicadoYCongelaReferenciaSinVolverAConsultar()
    {
        await using var test = await TestDatabase.CreateAsync();
        var fechaReferencia = new DateOnly(2026, 1, 9);
        var input = Input(test, OrigenCompra.CompraLocal, MonedaCompra.USD, 7.78m) with
        {
            TipoCambioReferencia = 7.64136m,
            FechaTipoCambioReferencia = fechaReferencia,
            FuenteTipoCambio = "Banco de Guatemala",
        };
        var service = new CompraService(test.Db);
        var resultado = await service.RegistrarAsync(input);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        var dto = (await service.ObtenerPorIdAsync(resultado.Value!.Id)).Value!;
        Assert.Equal(97.25m, dto.Total);
        Assert.Equal(7.78m, dto.TipoCambio);
        Assert.Equal(7.64136m, dto.TipoCambioReferencia);
        Assert.Equal(fechaReferencia, dto.FechaTipoCambioReferencia);
        Assert.Equal("Banco de Guatemala", dto.FuenteTipoCambio);
        Assert.Equal(97.25m, (await test.Db.UnidadesInventario.SingleAsync()).Costo);
    }

    [Theory]
    [InlineData("7.64136011")]
    [InlineData("7.1234567890123456789012345678")]
    public async Task TipoCambio_PreservaDecimalExactoEnSqliteSinTruncarEscala(string valor)
    {
        await using var test = await TestDatabase.CreateAsync();
        var tipo = Decimal(valor);
        var input = Input(test, OrigenCompra.CompraLocal, MonedaCompra.USD, tipo) with
        {
            TipoCambioReferencia = tipo,
            FechaTipoCambioReferencia = new(2026, 1, 9),
            FuenteTipoCambio = "Banco de Guatemala",
        };
        var service = new CompraService(test.Db);
        var resultado = await service.RegistrarAsync(input);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal(tipo, resultado.Value!.TipoCambio);
        Assert.Equal(tipo, resultado.Value.TipoCambioReferencia);
        var persistida = await test.Db.Compras.AsNoTracking().SingleAsync();
        Assert.Equal(tipo, persistida.TipoCambio);
        Assert.Equal(tipo, persistida.TipoCambioReferencia);
        Assert.Equal(ConversionMonedaCompra.CostoUnitarioGtq(12.50m, tipo), persistida.Total);
        await test.Db.Database.OpenConnectionAsync();
        await using var command = test.Db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT typeof(TipoCambio), typeof(TipoCambioReferencia) FROM Compras";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("text", reader.GetString(0));
        Assert.Equal("text", reader.GetString(1));
    }

    [Fact]
    public async Task Gtq_NoGuardaMetadatosDeReferenciaRedundantes()
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = Input(test, OrigenCompra.CompraLocal, MonedaCompra.GTQ, 1m) with
        {
            TipoCambioReferencia = 7.64136m,
            FechaTipoCambioReferencia = new(2026, 1, 9),
            FuenteTipoCambio = "Banco de Guatemala",
        };
        var resultado = await new CompraService(test.Db).RegistrarAsync(input);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Null(resultado.Value!.TipoCambioReferencia);
        Assert.Null(resultado.Value.FechaTipoCambioReferencia);
        Assert.Null(resultado.Value.FuenteTipoCambio);
    }

    [Theory]
    [InlineData(MonedaCompra.USD, "0")]
    [InlineData(MonedaCompra.USD, "-7.67")]
    [InlineData(MonedaCompra.GTQ, "0")]
    [InlineData(MonedaCompra.GTQ, "-1")]
    [InlineData(MonedaCompra.GTQ, "1.000001")]
    public async Task RechazaTiposInvalidosInclusoInputManipulado(MonedaCompra moneda, string tipo)
    {
        await using var test = await TestDatabase.CreateAsync();
        await RechazaSinPersistir(test, Input(test, OrigenCompra.CompraLocal, moneda, Decimal(tipo)));
    }

    [Theory]
    [InlineData(MonedaCompra.GTQ)]
    [InlineData(MonedaCompra.USD)]
    public async Task RechazaCostoNegativoYMayorEscalaMonetaria(MonedaCompra moneda)
    {
        await using var test = await TestDatabase.CreateAsync();
        var tipo = moneda == MonedaCompra.GTQ ? 1m : 7.67m;
        await RechazaSinPersistir(test, Input(test, OrigenCompra.CompraLocal, moneda, tipo, -0.01m));
        var resultado = await new CompraService(test.Db).RegistrarAsync(
            Input(test, OrigenCompra.CompraLocal, moneda, tipo, 12.501m));
        Assert.False(resultado.IsSuccess);
        Assert.Contains("dos decimales", resultado.ErrorMessage);
        Assert.Empty(await test.Db.Compras.ToListAsync());
    }

    [Fact]
    public async Task RechazaEnumsInvalidosYReferenciaInvalidaOFutura()
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = Input(test, OrigenCompra.CompraLocal, MonedaCompra.USD, 7.67m);
        await RechazaSinPersistir(test, input with { Moneda = (MonedaCompra)99 });
        await RechazaSinPersistir(test, input with { Origen = (OrigenCompra)99 });
        await RechazaSinPersistir(test, input with { TipoCambioReferencia = 0m });
        await RechazaSinPersistir(test, input with { TipoCambioReferencia = -1m });
        await RechazaSinPersistir(test, input with { FechaTipoCambioReferencia = input.FechaCompra.AddDays(1) });
    }

    [Fact]
    public async Task RangoMonetarioSeValidaEnServicioSinDependerDeSqlite()
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = Input(test, OrigenCompra.Catalogo, MonedaCompra.GTQ, 1m);
        await RechazaSinPersistir(test, input with { Detalles = [new(test.Producto.Id, 1, 100_000_000m)] });
        await RechazaSinPersistir(test, input with { Detalles = [new(test.Producto.Id, 2, 50_000_000m)] });
        await RechazaSinPersistir(test, input with
        {
            Moneda = MonedaCompra.USD,
            TipoCambio = 2m,
            Detalles = [new(test.Producto.Id, 1, 50_000_000m)],
        });
        await RechazaSinPersistir(test, input with
        {
            Moneda = MonedaCompra.USD,
            TipoCambio = 0.5m,
            Detalles = [new(test.Producto.Id, 2, 50_000_000m)],
        });
        var limite = await new CompraService(test.Db).RegistrarAsync(input with
        {
            Detalles = [new(test.Producto.Id, 1, 99_999_999.99m)],
        });
        Assert.True(limite.IsSuccess, limite.ErrorMessage);
        Assert.Equal(99_999_999.99m, limite.Value!.Total);
    }

    [Fact]
    public async Task OverflowDeConversionSubtotalOSumaDevuelveErrorControlado()
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = Input(test, OrigenCompra.CompraLocal, MonedaCompra.USD, decimal.MaxValue);
        await RechazaSinPersistir(test, input);
        await RechazaSinPersistir(test, input with
        {
            TipoCambio = decimal.MaxValue,
            Detalles = [new(test.Producto.Id, int.MaxValue, 0.01m)],
        });
        await RechazaSinPersistir(test, input with
        {
            TipoCambio = decimal.MaxValue / 2m,
            Detalles = [new(test.Producto.Id, 1, 1m), new(test.Producto.Id, 1, 1m), new(test.Producto.Id, 1, 1m)],
        });
    }

    private static CompraInput Input(TestDatabase test, OrigenCompra origen, MonedaCompra moneda,
        decimal tipo, decimal costo = 12.50m, int cantidad = 1) =>
        test.Compra(origen, "COMPRA-MONEDA",
            origen is OrigenCompra.CompraLocal or OrigenCompra.EnvioHermano ? new DateOnly(2026, 1, 11) : null)
            with { Moneda = moneda, TipoCambio = tipo, Detalles = [new(test.Producto.Id, cantidad, costo)] };

    private static decimal Decimal(string valor) => decimal.Parse(valor, CultureInfo.InvariantCulture);

    private static async Task RechazaSinPersistir(TestDatabase test, CompraInput input)
    {
        var resultado = await new CompraService(test.Db).RegistrarAsync(input);
        Assert.False(resultado.IsSuccess);
        Assert.NotEmpty(resultado.ErrorMessage!);
        Assert.Empty(await test.Db.Compras.ToListAsync());
        Assert.Empty(await test.Db.DetallesCompra.ToListAsync());
        Assert.Empty(await test.Db.UnidadesInventario.ToListAsync());
    }
}

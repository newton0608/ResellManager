using System.Collections;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Clientes;
using ResellManager.Web.Components.Inventario;
using ResellManager.Web.Components.Pedidos;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class CierreV1OperativoTests
{
    [Fact]
    public async Task VentaFallida_RevierteVentaEstadoYLiberacionDeReservas()
    {
        await using var test = await TestDatabase.CreateAsync();
        var original = await test.CrearUnidadImportadaAsync("ORIGINAL");
        var sustituta = await test.CrearUnidadDisponibleAsync("SUSTITUTA");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Importacion, "PED");
        var detalleId = pedido.Detalles.Single().Id;
        Assert.True((await new InventarioService(test.Db).ReservarAsync(original.Id, detalleId)).IsSuccess);
        // Fallo real de persistencia, exclusivamente en la SQLite en memoria de esta prueba.
        await test.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER venta_rechazada BEFORE INSERT ON Ventas BEGIN SELECT RAISE(ABORT, 'Fallo de prueba'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new(pedido.Id, "VEN", new(2026, 9, 1), null, [new(sustituta.Id, null, null, 100, null)])));
        Assert.Empty(await test.Db.Ventas.AsNoTracking().ToListAsync());
        Assert.Equal(EstadoPedido.Pendiente, (await test.Db.Pedidos.AsNoTracking().SingleAsync()).Estado);
        var unidades = await test.Db.UnidadesInventario.AsNoTracking().ToListAsync();
        Assert.Equal(detalleId, unidades.Single(x => x.Id == original.Id).DetallePedidoReservaId);
        Assert.Equal(EstadoUnidadInventario.Comprada, unidades.Single(x => x.Id == original.Id).Estado);
        Assert.Equal(EstadoUnidadInventario.Disponible, unidades.Single(x => x.Id == sustituta.Id).Estado);
    }

    [Fact]
    public async Task ActividadMensual_CargaUnMesPorAccion_ConservaAnterioresYEvitaDuplicados()
    {
        var componente = new ActividadMensual<string>();
        var consultas = new List<DateOnly?>();
        var anterior = new DateOnly(2025, 6, 1);
        var respuesta = new TaskCompletionSource<PaginaMes<string>>();
        Set(componente, "ClienteId", 7);
        Set(componente, "Logger", NullLogger<ActividadMensual<string>>.Instance);
        Set(componente, "Consultar", (Func<int, DateOnly?, Task<PaginaMes<string>>>)(
            (cliente, mes) => {
                Assert.Equal(7, cliente);
                consultas.Add(mes);
                return mes is null ? Task.FromResult(new PaginaMes<string>(new(2026, 9, 1), ["septiembre"], anterior)) : respuesta.Task;
            }));
        await CallAsync(componente, "OnParametersSetAsync");
        await CallAsync(componente, "OnParametersSetAsync");
        var meses = (List<PaginaMes<string>>)Campo(componente, "Meses");
        Assert.Single(consultas);
        Assert.Equal("septiembre", Assert.Single(Assert.Single(meses).Registros));
        var carga = CallAsync(componente, "CargarAsync");
        await CallAsync(componente, "CargarAsync");
        Assert.Equal(2, consultas.Count);
        Assert.Equal(anterior, consultas[1]);
        respuesta.SetResult(new(anterior, ["junio"], null));
        await carga;
        await CallAsync(componente, "CargarAsync");
        Assert.Equal(2, consultas.Count);
        Assert.Equal(new[] { "septiembre", "junio" }, meses.SelectMany(x => x.Registros));
    }

    internal static ServiceProvider Servicios(TestDatabase test) => new ServiceCollection()
        .AddScoped(_ => new ResellManagerDbContext(new DbContextOptionsBuilder<ResellManagerDbContext>()
            .UseSqlite(test.Db.Database.GetDbConnection()).Options))
        .AddScoped<ISeleccionOperativaService, SeleccionOperativaService>()
        .AddScoped<IRecepcionCompraService, RecepcionCompraService>()
        .AddScoped<IInventarioService, InventarioService>().BuildServiceProvider();
    private static object Campo(object instancia, string nombre) => instancia.GetType()
        .GetField(nombre, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instancia)!;

    [Theory]
    [InlineData(EstadoUnidadInventario.Comprada)]
    [InlineData(EstadoUnidadInventario.EnTransito)]
    [InlineData(EstadoUnidadInventario.Disponible)]
    public async Task VentaSustituyeReserva_LiberaSinAlterarEstadoFisico(EstadoUnidadInventario estado)
    {
        await using var test = await TestDatabase.CreateAsync();
        var original = await test.CrearUnidadImportadaAsync("ORIGINAL");
        original.Estado = estado;
        var sustituta = await test.CrearUnidadDisponibleAsync("SUSTITUTA");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Importacion, "PED");
        await test.Db.SaveChangesAsync();
        var inventario = new InventarioService(test.Db);
        Assert.True((await inventario.ReservarAsync(original.Id, pedido.Detalles.Single().Id)).IsSuccess);
        var venta = await new VentaService(test.Db).RegistrarDesdePedidoAsync(new(pedido.Id, "VEN", new(2026, 9, 1), null,
            [new(sustituta.Id, null, null, 100, null)]));
        Assert.True(venta.IsSuccess, venta.ErrorMessage);
        Assert.Equal(EstadoPedido.Completado, pedido.Estado);
        Assert.Equal(estado, original.Estado);
        Assert.Equal(EstadoUnidadInventario.Vendida, sustituta.Estado);
        Assert.All(await test.Db.UnidadesInventario.AsNoTracking().ToListAsync(), x => Assert.Null(x.DetallePedidoReservaId));
        var pendientes = await new SeleccionOperativaService(test.Db).PendientesClienteAsync(test.Cliente.Id);
        Assert.Equal(sustituta.Id, Assert.Single(pendientes).UnidadId);
        if (estado != EstadoUnidadInventario.Disponible)
        {
            Assert.True((await inventario.RegistrarRecepcionAsync(new(new(2026, 9, 2), [original.Id]))).IsSuccess);
            Assert.Equal(EstadoUnidadInventario.Disponible, original.Estado);
            Assert.Null(original.DetallePedidoReservaId);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VentaUsaReservaExacta_OParcialConSustitucion_NoDejaReservas(bool sustituirSegunda)
    {
        await using var test = await TestDatabase.CreateAsync();
        var a = await test.CrearUnidadDisponibleAsync("A");
        var b = await test.CrearUnidadDisponibleAsync("B");
        var c = await test.CrearUnidadDisponibleAsync("C");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED", cantidad: 2);
        var inventario = new InventarioService(test.Db);
        Assert.True((await inventario.ReservarAsync(a.Id, pedido.Detalles.Single().Id)).IsSuccess);
        Assert.True((await inventario.ReservarAsync(b.Id, pedido.Detalles.Single().Id)).IsSuccess);
        var venta = await new VentaService(test.Db).RegistrarDesdePedidoAsync(new(pedido.Id, "VEN", new(2026, 9, 1), null,
            [new(a.Id, null, null, 100, null), new(sustituirSegunda ? c.Id : b.Id, null, null, 100, null)]));
        Assert.True(venta.IsSuccess, venta.ErrorMessage);
        Assert.Equal(EstadoPedido.Completado, pedido.Estado);
        Assert.Equal(EstadoUnidadInventario.Vendida, a.Estado);
        Assert.Equal(sustituirSegunda ? EstadoUnidadInventario.Disponible : EstadoUnidadInventario.Vendida, b.Estado);
        Assert.All(await test.Db.UnidadesInventario.AsNoTracking().ToListAsync(), x => Assert.Null(x.DetallePedidoReservaId));
    }

    [Theory]
    [InlineData(EstadoPedido.Completado)]
    [InlineData(EstadoPedido.Cancelado)]
    [InlineData(EstadoPedido.Pendiente)]
    public async Task Pendientes_ExcluyeCerrados_PeroIncluyeReservasAntiguasActivas(EstadoPedido estado)
    {
        await using var test = await TestDatabase.CreateAsync();
        var a = await test.CrearUnidadImportadaAsync("A");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "ANTIGUO");
        pedido.Fecha = new(2020, 1, 1);
        a.DetallePedidoReservaId = pedido.Detalles.Single().Id;
        pedido.Estado = estado; // Evidencia histórica inconsistente que la consulta debe excluir.
        await test.Db.SaveChangesAsync();
        var resultado = await new SeleccionOperativaService(test.Db).PendientesClienteAsync(test.Cliente.Id);
        if (estado == EstadoPedido.Pendiente) Assert.Equal(a.Id, Assert.Single(resultado).UnidadId);
        else Assert.Empty(resultado);
    }

    [Fact]
    public async Task Reservables_IncluyeTresEstados_ExcluyeVendidaEntregadaAjenaYOtroProducto()
    {
        await using var test = await TestDatabase.CreateAsync();
        foreach (var estado in Enum.GetValues<EstadoUnidadInventario>())
        {
            var unidad = await test.CrearUnidadImportadaAsync(estado.ToString());
            unidad.Estado = estado;
        }
        var ajena = await test.CrearUnidadDisponibleAsync("AJENA");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "AJENO");
        ajena.DetallePedidoReservaId = pedido.Detalles.Single().Id;
        await test.CrearUnidadDisponibleAsync("OTRO", await test.CrearProductoAsync("OTRO"));
        await test.Db.SaveChangesAsync();
        var opciones = await new SeleccionOperativaService(test.Db).ListarReservablesAsync(test.Producto.Id);
        Assert.Equal(3, opciones.Count);
        Assert.All(opciones, x => { Assert.Null(x.DetallePedidoReservaId); Assert.Equal(test.Producto.Id, x.ProductoId); });
        Assert.Contains(opciones, x => x.Estado == EstadoUnidadInventario.Comprada);
        Assert.Contains(opciones, x => x.Estado == EstadoUnidadInventario.EnTransito);
        Assert.Contains(opciones, x => x.Estado == EstadoUnidadInventario.Disponible);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task ReservaAlCrear_UnicaAutomaticaOVariasPorSelect_SinPersistir(int cantidad)
    {
        await using var test = await TestDatabase.CreateAsync();
        for (var i = 0; i < cantidad; i++) await test.CrearUnidadImportadaAsync($"U-{i}");
        await using var servicios = Servicios(test);
        var componente = new ReservaAlCrear();
        Set(componente, "Scopes", servicios.GetRequiredService<IServiceScopeFactory>());
        Set(componente, "Logger", NullLogger<ReservaAlCrear>.Instance);
        Set(componente, "ProductoId", test.Producto.Id);
        Set(componente, "Cantidad", 2);
        await CallAsync(componente, "OnParametersSetAsync");
        var seleccionadas = Get<List<UnidadInventarioDto>>(componente, "Seleccionadas");
        Assert.Empty(seleccionadas);
        await CallAsync(componente, "AceptarAsync");
        if (cantidad == 1) Assert.Single(seleccionadas);
        else
        {
            Assert.Empty(seleccionadas);
            var opciones = Get<IReadOnlyList<UnidadInventarioDto>>(componente, "Opciones").ToArray();
            foreach (var opcion in opciones) Call(componente, "Elegir", new ChangeEventArgs { Value = opcion.Id.ToString() });
            Assert.Equal(2, seleccionadas.Count);
            Assert.Single(Get<IReadOnlyList<UnidadInventarioDto>>(componente, "Opciones"));
            Call(componente, "Quitar", seleccionadas[0]);
            Assert.Single(seleccionadas);
            Assert.Equal(2, Get<IReadOnlyList<UnidadInventarioDto>>(componente, "Opciones").Count);
        }
        Assert.All(await test.Db.UnidadesInventario.ToListAsync(), x => Assert.Null(x.DetallePedidoReservaId));
    }

    [Theory]
    [InlineData(EstadoUnidadInventario.Comprada)]
    [InlineData(EstadoUnidadInventario.EnTransito)]
    public async Task PedidoTransaccional_AdmiteReservaAntesDeRecepcion(EstadoUnidadInventario estado)
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("A"); unidad.Estado = estado; await test.Db.SaveChangesAsync();
        await using var servicios = SeleccionOperativaTests.Servicios(test);
        var resultado = await new RegistroPedidoConReservasService(servicios.GetRequiredService<IServiceScopeFactory>())
            .RegistrarAsync(new("PED", new(2026, 9, 1), TipoPedido.Importacion, CanalVenta.Otro, test.Cliente.Id, null,
                [new(test.Producto.Id, 1, 100, null)]), [new(0, [unidad.Id])]);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.NotNull(unidad.DetallePedidoReservaId);
        Assert.Equal(estado, unidad.Estado);
    }

    [Fact]
    public async Task RecepcionParcial_PorCompra_NoMezclaYConservaReservas()
    {
        await using var test = await TestDatabase.CreateAsync();
        Assert.True((await new CompraService(test.Db).RegistrarAsync(test.Compra(OrigenCompra.Importacion, "COMPRA-A", cantidad: 2))).IsSuccess);
        var b = await test.CrearUnidadImportadaAsync("COMPRA-B");
        var grupo = (await new RecepcionCompraService(test.Db).PendientesAsync()).Single(x => x.CodigoCompra == "COMPRA-A");
        Assert.Equal(2, grupo.Unidades.Count);
        var pedido = await test.CrearPedidoAsync(TipoPedido.Importacion, "PED");
        var inventario = new InventarioService(test.Db);
        Assert.True((await inventario.ReservarAsync(grupo.Unidades[0].Id, pedido.Detalles.Single().Id)).IsSuccess);
        var mezcla = await inventario.RegistrarRecepcionAsync(new(new(2026, 9, 1), [grupo.Unidades[0].Id, b.Id]));
        Assert.False(mezcla.IsSuccess);
        Assert.All(await test.Db.UnidadesInventario.ToListAsync(), x => Assert.Equal(EstadoUnidadInventario.Comprada, x.Estado));
        Assert.True((await inventario.RegistrarRecepcionAsync(new(new(2026, 9, 1), [grupo.Unidades[0].Id]))).IsSuccess);
        var recibida = await test.Db.UnidadesInventario.FindAsync(grupo.Unidades[0].Id);
        Assert.Equal(pedido.Detalles.Single().Id, recibida!.DetallePedidoReservaId);
        Assert.Equal(EstadoUnidadInventario.Disponible, recibida.Estado);
        var restantes = await new RecepcionCompraService(test.Db).PendientesAsync();
        Assert.Equal(2, restantes.Count);
        Assert.Single(restantes.Single(x => x.CompraId == grupo.CompraId).Unidades);
        Assert.True((await inventario.RegistrarRecepcionAsync(new(new(2026, 9, 1), [grupo.Unidades[1].Id]))).IsSuccess);
        Assert.DoesNotContain(await new RecepcionCompraService(test.Db).PendientesAsync(), x => x.CompraId == grupo.CompraId);
    }

    internal static async Task VerificarConfirmacionRecepcion(TestDatabase test, UnidadInventario unidad)
    {
        await using var servicios = Servicios(test);
        var componente = new RecepcionCompras();
        Set(componente, "Scopes", servicios.GetRequiredService<IServiceScopeFactory>());
        Set(componente, "Logger", NullLogger<RecepcionCompras>.Instance);
        await CallAsync(componente, "CargarAsync");
        var grupo = ((IEnumerable)Campo(componente, "Grupos")).Cast<object>().Single();
        Call(componente, "Seleccionar", grupo, unidad.Id, true);
        Call(componente, "Revisar", grupo);
        Assert.Equal(EstadoUnidadInventario.Comprada, unidad.Estado);
        Call(componente, "Cerrar");
        await CallAsync(componente, "ConfirmarAsync");
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(EstadoUnidadInventario.Comprada, unidad.Estado);
        Call(componente, "Revisar", grupo);
        await CallAsync(componente, "ConfirmarAsync");
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(EstadoUnidadInventario.Disponible, unidad.Estado);
    }

    [Fact]
    public async Task Cliente_MesesIndependientesOrdenados_SaldoCompletoInvariable()
    {
        await using var test = await TestDatabase.CreateAsync();
        foreach (var (codigo, fecha) in new[] { ("V1", new DateOnly(2026, 9, 10)), ("V2", new DateOnly(2026, 9, 1)), ("V3", new DateOnly(2025, 6, 15)) })
        {
            var venta = await test.CrearVentaCatalogoAsync("P-" + codigo, codigo);
            venta.Fecha = fecha;
        }
        await test.Db.SaveChangesAsync();
        var pagoService = new PagoService(test.Db);
        Assert.True((await pagoService.RegistrarAsync(new(test.Cliente.Id, new(2026, 8, 1), 20, MetodoPago.Efectivo, null, null))).IsSuccess);
        Assert.True((await pagoService.RegistrarAsync(new(test.Cliente.Id, new(2024, 1, 1), 10, MetodoPago.Efectivo, null, null))).IsSuccess);
        var consulta = new ActividadClienteService(test.Db);
        var saldo = (await new ClienteService(test.Db).ObtenerSaldoAsync(test.Cliente.Id)).Value;
        var ventas = await consulta.VentasAsync(test.Cliente.Id);
        Assert.Equal(new DateOnly(2026, 9, 1), ventas.Mes);
        Assert.Equal(new[] { "V1", "V2" }, ventas.Registros.Select(x => x.CodigoInterno));
        Assert.Equal(new DateOnly(2025, 6, 1), ventas.MesAnterior);
        var anteriores = await consulta.VentasAsync(test.Cliente.Id, ventas.MesAnterior);
        Assert.Equal("V3", Assert.Single(anteriores.Registros).CodigoInterno);
        Assert.Null(anteriores.MesAnterior);
        var pagos = await consulta.PagosAsync(test.Cliente.Id);
        Assert.Equal(new DateOnly(2026, 8, 1), pagos.Mes);
        Assert.Single(pagos.Registros);
        Assert.Equal(new DateOnly(2024, 1, 1), pagos.MesAnterior);
        Assert.Null((await consulta.PagosAsync(test.Cliente.Id, pagos.MesAnterior)).MesAnterior);
        Assert.Equal(saldo, (await new ClienteService(test.Db).ObtenerSaldoAsync(test.Cliente.Id)).Value);
        Assert.Empty((await consulta.VentasAsync(99999)).Registros);
        Assert.Empty((await consulta.PagosAsync(99999)).Registros);
    }
}

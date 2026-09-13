using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Shared;

namespace ResellManager.Tests;

public sealed class SeleccionOperativaTests
{
    internal static ServiceProvider Servicios(TestDatabase test) => new ServiceCollection()
        .AddScoped(_ => new ResellManagerDbContext(new DbContextOptionsBuilder<ResellManagerDbContext>()
            .UseSqlite(test.Db.Database.GetDbConnection()).Options))
        .AddScoped<IPedidoService, PedidoService>()
        .AddScoped<IInventarioService, InventarioService>().BuildServiceProvider();

    private static PedidoInput Pedido(TestDatabase test, TipoPedido tipo = TipoPedido.Apartado, int cantidad = 1) =>
        new("PED-RESERVA", new(2026, 9, 1), tipo, CanalVenta.Otro, test.Cliente.Id, null,
            [new(test.Producto.Id, cantidad, 100, null)]);

    [Theory]
    [InlineData("unidad")]
    [InlineData("perfume")]
    [InlineData("prod-1")]
    [InlineData("750123")]
    public async Task Unidades_BuscaCuatroReferenciasYSoloElegibles(string termino)
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Producto.Nombre = "Perfume";
        test.Producto.CodigoBarras = "750123";
        var libre = await test.CrearUnidadDisponibleAsync("LIBRE");
        libre.CodigoInterno = "UNIDAD-LIBRE";
        var ajena = await test.CrearUnidadDisponibleAsync("AJENA");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-AJENO");
        var inventario = new InventarioService(test.Db);
        Assert.True((await inventario.ReservarAsync(ajena.Id, pedido.Detalles.Single().Id)).IsSuccess);
        await test.CrearUnidadImportadaAsync("COMPRADA");
        var vendida = await test.CrearUnidadDisponibleAsync("VENDIDA");
        vendida.Estado = EstadoUnidadInventario.Vendida;
        await test.Db.SaveChangesAsync();
        var service = new SeleccionOperativaService(test.Db);
        Assert.Equal(libre.Id, Assert.Single(await service.BuscarUnidadesAsync(termino)).Id);
        Assert.Empty(await service.BuscarUnidadesAsync(termino, excluir: [libre.Id]));
        Assert.Empty(await service.BuscarUnidadesAsync(termino, productoId: 99999));
        Assert.Empty(await service.BuscarUnidadesAsync("inexistente"));
        Assert.True(await service.UnidadesDirectasDisponiblesAsync([libre.Id]));
        Assert.False(await service.UnidadesDirectasDisponiblesAsync([libre.Id, libre.Id]));
        Assert.False(await service.UnidadesDirectasDisponiblesAsync([ajena.Id]));
        Assert.False(await service.UnidadesDirectasDisponiblesAsync([vendida.Id]));
    }

    [Fact]
    public async Task ReservaPropia_TienePrioridad_LimiteDoce_YDisponibilidadUnica()
    {
        await using var test = await TestDatabase.CreateAsync();
        var primera = await test.CrearUnidadDisponibleAsync("UNICA");
        var service = new SeleccionOperativaService(test.Db);
        Assert.Equal(primera.Id, (await service.DisponibilidadAsync(test.Producto.Id)).Unica!.Id);
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-PROPIO");
        Assert.True((await new InventarioService(test.Db).ReservarAsync(primera.Id, pedido.Detalles.Single().Id)).IsSuccess);
        for (var i = 0; i < 13; i++) await test.CrearUnidadDisponibleAsync($"LIBRE-{i}");
        var opciones = await service.BuscarUnidadesAsync("", pedidoId: pedido.Id);
        Assert.Equal(12, opciones.Count);
        Assert.Equal(primera.Id, opciones[0].Id);
        var disponibilidad = await service.DisponibilidadAsync(test.Producto.Id);
        Assert.Equal(13, disponibilidad.Cantidad);
        Assert.Null(disponibilidad.Unica);
        Assert.Single(await service.BuscarUnidadesAsync("", pedidoId: pedido.Id, soloReservadas: true));
    }

    [Fact]
    public async Task Pedidos_BuscaCodigoOCliente_ExcluyeCanceladosVendidosYVacios()
    {
        await using var test = await TestDatabase.CreateAsync();
        var pedido = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-ELEGIBLE");
        var cancelado = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-CANCELADO");
        cancelado.Estado = EstadoPedido.Cancelado;
        var vacio = await test.CrearPedidoAsync(TipoPedido.Catalogo, "PED-VACIO");
        test.Db.DetallesPedido.RemoveRange(vacio.Detalles);
        await test.CrearVentaCatalogoAsync("PED-VENDIDO", "VEN-1");
        await test.Db.SaveChangesAsync();
        var service = new SeleccionOperativaService(test.Db);
        Assert.Equal(pedido.Id, Assert.Single(await service.BuscarPedidosAsync("ped-elegible")).Id);
        Assert.Equal(pedido.Id, Assert.Single(await service.BuscarPedidosAsync("CLIENTE")).Id);
        Assert.Empty(await service.BuscarPedidosAsync("CANCELADO"));
        Assert.Empty(await service.BuscarPedidosAsync("VENDIDO"));
        Assert.Empty(await service.BuscarPedidosAsync("VACIO"));
        Assert.Empty(await service.BuscarPedidosAsync("inexistente"));
    }

    [Fact]
    public async Task PedidoConReservas_AsignaDetallesExactosYPreservaEstadoFisico()
    {
        await using var test = await TestDatabase.CreateAsync();
        var uno = await test.CrearUnidadDisponibleAsync("UNO");
        var dos = await test.CrearUnidadDisponibleAsync("DOS");
        await using var servicios = Servicios(test);
        var service = new RegistroPedidoConReservasService(servicios.GetRequiredService<IServiceScopeFactory>());
        var input = Pedido(test) with { Detalles = [new(test.Producto.Id, 1, 100, "primero"), new(test.Producto.Id, 1, 90, "segundo")] };
        var resultado = await service.RegistrarAsync(input, [new(0, [dos.Id]), new(1, [uno.Id])]);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        var detalles = resultado.Value!.Detalles.OrderBy(x => x.Id).ToArray();
        await test.Db.Entry(uno).ReloadAsync();
        await test.Db.Entry(dos).ReloadAsync();
        Assert.Equal(detalles[0].Id, dos.DetallePedidoReservaId);
        Assert.Equal(detalles[1].Id, uno.DetallePedidoReservaId);
        Assert.Equal(EstadoUnidadInventario.Disponible, uno.Estado);
        Assert.Equal(EstadoUnidadInventario.Disponible, dos.Estado);
    }

    [Theory]
    [InlineData("duplicada")]
    [InlineData("cantidad")]
    [InlineData("catalogo")]
    [InlineData("vendida")]
    [InlineData("ajena")]
    [InlineData("producto")]
    [InlineData("inexistente")]
    public async Task ReservaInvalida_NoCreaPedidoNiReservaParcial(string caso)
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("UNO");
        var dos = await test.CrearUnidadDisponibleAsync("DOS");
        var input = Pedido(test);
        IReadOnlyList<ReservaPedidoInput> reservas = [new(0, [unidad.Id])];
        if (caso == "duplicada") reservas = [new(0, [unidad.Id, unidad.Id])];
        if (caso == "cantidad") reservas = [new(0, [unidad.Id, dos.Id])];
        if (caso == "catalogo") input = input with { TipoPedido = TipoPedido.Catalogo };
        if (caso == "vendida") unidad.Estado = EstadoUnidadInventario.Vendida;
        if (caso == "producto") unidad.ProductoId = (await test.CrearProductoAsync("OTRO")).Id;
        if (caso == "inexistente") reservas = [new(0, [int.MaxValue])];
        if (caso == "ajena")
        {
            var ajeno = await test.CrearPedidoAsync(TipoPedido.Apartado, "AJENO");
            Assert.True((await new InventarioService(test.Db).ReservarAsync(unidad.Id, ajeno.Detalles.Single().Id)).IsSuccess);
        }
        await test.Db.SaveChangesAsync();
        var pedidosAntes = await test.Db.Pedidos.CountAsync();
        var reservaAntes = unidad.DetallePedidoReservaId;
        await using var servicios = Servicios(test);
        var service = new RegistroPedidoConReservasService(servicios.GetRequiredService<IServiceScopeFactory>());
        var result = await service.RegistrarAsync(input, reservas);
        Assert.False(result.IsSuccess);
        Assert.Equal(pedidosAntes, await test.Db.Pedidos.CountAsync());
        await test.Db.Entry(unidad).ReloadAsync();
        Assert.Equal(reservaAntes, unidad.DetallePedidoReservaId);
        Assert.Null(dos.DetallePedidoReservaId);
    }

    [Fact]
    public async Task FalloDuranteSegundaReserva_ReviertePedidoDetallesYPrimeraReserva()
    {
        await using var test = await TestDatabase.CreateAsync();
        var uno = await test.CrearUnidadDisponibleAsync("UNO");
        var dos = await test.CrearUnidadDisponibleAsync("DOS");
        // Fallo real de SQLite al persistir la segunda reserva; solo existe en esta BD en memoria.
        await test.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER reserva_rechazada BEFORE UPDATE OF DetallePedidoReservaId ON UnidadesInventario WHEN NEW.DetallePedidoReservaId IS NOT NULL AND EXISTS (SELECT 1 FROM UnidadesInventario WHERE DetallePedidoReservaId IS NOT NULL) BEGIN SELECT RAISE(ABORT, 'Fallo de prueba'); END;");
        await using var servicios = Servicios(test);
        var service = new RegistroPedidoConReservasService(servicios.GetRequiredService<IServiceScopeFactory>());
        await Assert.ThrowsAsync<DbUpdateException>(() => service.RegistrarAsync(Pedido(test, cantidad: 2), [new(0, [uno.Id, dos.Id])]));
        Assert.Empty(await test.Db.Pedidos.AsNoTracking().ToListAsync());
        Assert.Empty(await test.Db.DetallesPedido.AsNoTracking().ToListAsync());
        Assert.All(await test.Db.UnidadesInventario.AsNoTracking().ToListAsync(), x => Assert.Null(x.DetallePedidoReservaId));
    }

    [Fact]
    public async Task CatalogoSinReserva_NoGeneraInventario()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var servicios = Servicios(test);
        var result = await new RegistroPedidoConReservasService(servicios.GetRequiredService<IServiceScopeFactory>())
            .RegistrarAsync(Pedido(test, TipoPedido.Catalogo), []);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Empty(await test.Db.UnidadesInventario.ToListAsync());
    }

    [Fact]
    public async Task PendientesCliente_ReservaLiberadaVentaYEntrega()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("UNO");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-UNO");
        var inventario = new InventarioService(test.Db);
        var consulta = new SeleccionOperativaService(test.Db);
        Assert.True((await inventario.ReservarAsync(unidad.Id, pedido.Detalles.Single().Id)).IsSuccess);
        Assert.False(Assert.Single(await consulta.PendientesClienteAsync(test.Cliente.Id)).PendienteEntrega);
        Assert.Empty(await consulta.PendientesClienteAsync(99999));
        Assert.True((await inventario.CancelarReservaAsync(unidad.Id)).IsSuccess);
        Assert.Empty(await consulta.PendientesClienteAsync(test.Cliente.Id));
        var venta = await new VentaService(test.Db).RegistrarDesdePedidoAsync(new(pedido.Id, "VEN-UNO", new(2026, 9, 1), null,
            [new(unidad.Id, null, null, 100, null)]));
        Assert.True(venta.IsSuccess, venta.ErrorMessage);
        Assert.True(Assert.Single(await consulta.PendientesClienteAsync(test.Cliente.Id)).PendienteEntrega);
        Assert.True((await inventario.CambiarEstadoAsync(unidad.Id, EstadoUnidadInventario.Entregada)).IsSuccess);
        Assert.Empty(await consulta.PendientesClienteAsync(test.Cliente.Id));
    }

    [Fact]
    public void Meses_AbrirCerrarYFiltrar_NoModificaGruposNiOrden()
    {
        var datos = new[] { new DateOnly(2026, 8, 31), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10) };
        var grupos = HistorialMensual.Agrupar(datos, x => x, x => x.Day);
        var meses = new MesesDesplegables();
        meses.Reiniciar(grupos);
        Assert.True(meses.Abierto(2026, 9));
        Assert.False(meses.Abierto(2026, 8));
        Assert.Equal("Septiembre 2026", grupos[0].Titulo);
        Assert.Equal(new[] { 10, 1 }, grupos[0].Registros.Select(x => x.Day));
        meses.Alternar(2026, 9);
        meses.Alternar(2026, 8);
        Assert.False(meses.Abierto(2026, 9));
        Assert.True(meses.Abierto(2026, 8));
        meses.Reiniciar(grupos.Skip(1).ToArray());
        Assert.True(meses.Abierto(2026, 8));
        Assert.Equal(2, grupos[0].Registros.Count);
        meses.Reiniciar(Array.Empty<GrupoMensual<DateOnly>>());
        Assert.False(meses.Abierto(2026, 8));
    }
}

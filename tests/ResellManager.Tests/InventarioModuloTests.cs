using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Inventario;

namespace ResellManager.Tests;

public sealed class InventarioConsultaTests
{
    [Fact]
    public async Task ListarAsync_DevuelveUnidadesFisicasConDatosDeProductoYReserva()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("IMP-LISTADO");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-LISTADO");
        var service = new InventarioService(test.Db);
        var transito = await service.CambiarEstadoAsync(
            unidad.Id,
            EstadoUnidadInventario.EnTransito);
        var reserva = await service.ReservarAsync(unidad.Id, pedido.Detalles.Single().Id);

        var unidades = await service.ListarAsync();

        Assert.True(transito.IsSuccess, transito.ErrorMessage);
        Assert.True(reserva.IsSuccess, reserva.ErrorMessage);
        var dto = Assert.Single(unidades);
        Assert.Equal(unidad.Id, dto.Id);
        Assert.Equal("Producto", dto.Producto);
        Assert.Equal("PROD-1", dto.CodigoProducto);
        Assert.Equal(EstadoUnidadInventario.EnTransito, dto.Estado);
        Assert.Equal(pedido.Detalles.Single().Id, dto.DetallePedidoReservaId);
        Assert.Equal(pedido.Id, dto.PedidoReservaId);
        Assert.Equal(test.Cliente.Id, dto.ClienteReservaId);
    }

    [Fact]
    public async Task BuscarAsync_ConTerminoSinEstado_UsaLosCamposSoportados()
    {
        await using var test = await TestDatabase.CreateAsync();
        var otroProducto = await test.CrearProductoAsync("PROD-BUSCADO");
        await test.CrearUnidadDisponibleAsync("LOCAL-IGNORAR");
        var esperada = await test.CrearUnidadImportadaAsync("IMPORT-BUSCADA", otroProducto);

        var unidades = await new InventarioService(test.Db).BuscarAsync("BUSCADA", null);

        var dto = Assert.Single(unidades);
        Assert.Equal(esperada.Id, dto.Id);
        Assert.Equal("PROD-BUSCADO", dto.CodigoProducto);
    }

    [Fact]
    public async Task BuscarAsync_ConEstadoSinTermino_FiltraEnBackend()
    {
        await using var test = await TestDatabase.CreateAsync();
        var disponible = await test.CrearUnidadDisponibleAsync("LOCAL-DISPONIBLE");
        await test.CrearUnidadImportadaAsync("IMPORT-COMPRADA");

        var unidades = await new InventarioService(test.Db).BuscarAsync(
            null,
            EstadoUnidadInventario.Disponible);

        var dto = Assert.Single(unidades);
        Assert.Equal(disponible.Id, dto.Id);
        Assert.Equal(EstadoUnidadInventario.Disponible, dto.Estado);
    }

    [Fact]
    public async Task BuscarAsync_CombinaTerminoYEstadoEnBackend()
    {
        await using var test = await TestDatabase.CreateAsync();
        var producto = await test.CrearProductoAsync("PROD-COMBINADO");
        var esperada = await test.CrearUnidadDisponibleAsync("LOCAL-COMBINADA", producto);
        await test.CrearUnidadImportadaAsync("IMPORT-COMBINADA", producto);
        await test.CrearUnidadDisponibleAsync("LOCAL-OTRA");

        var unidades = await new InventarioService(test.Db).BuscarAsync(
            "COMBINAD",
            EstadoUnidadInventario.Disponible);

        var dto = Assert.Single(unidades);
        Assert.Equal(esperada.Id, dto.Id);
        Assert.Equal(EstadoUnidadInventario.Disponible, dto.Estado);
    }
}

public sealed class InventarioPresentacionTests
{
    [Fact]
    public void EstadoFisicoYReserva_SePresentanComoConceptosSeparados()
    {
        var reservada = Unidad(
            EstadoUnidadInventario.EnTransito,
            detallePedidoReservaId: 17);
        var noReservada = Unidad(EstadoUnidadInventario.EnTransito);

        Assert.Equal("En tránsito", InventarioPresentacion.Estado(reservada.Estado));
        Assert.True(InventarioPresentacion.EstaReservada(reservada));
        Assert.False(InventarioPresentacion.EstaReservada(noReservada));
        Assert.Equal(
            InventarioPresentacion.ClaseEstado(reservada.Estado),
            InventarioPresentacion.ClaseEstado(noReservada.Estado));
    }

    [Fact]
    public void EstadoUnidadInventario_NoContieneApartada()
    {
        var estados = Enum.GetNames<EstadoUnidadInventario>();

        Assert.DoesNotContain(
            estados,
            estado => estado.Contains("Apartada", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(5, estados.Length);
    }

    [Theory]
    [InlineData(EstadoUnidadInventario.Comprada, EstadoUnidadInventario.EnTransito)]
    [InlineData(EstadoUnidadInventario.Vendida, EstadoUnidadInventario.Entregada)]
    public void AccionesManuales_SoloExponenTransicionesPermitidas(
        EstadoUnidadInventario actual,
        EstadoUnidadInventario esperada)
    {
        Assert.Equal(
            esperada,
            InventarioPresentacion.SiguienteEstadoManual(Unidad(actual)));
    }

    [Theory]
    [InlineData(EstadoUnidadInventario.EnTransito)]
    [InlineData(EstadoUnidadInventario.Disponible)]
    [InlineData(EstadoUnidadInventario.Entregada)]
    public void AccionesManuales_NoExponenTransicionArbitraria(
        EstadoUnidadInventario estado)
    {
        Assert.Null(InventarioPresentacion.SiguienteEstadoManual(Unidad(estado)));
    }

    [Theory]
    [InlineData(EstadoUnidadInventario.Comprada, true)]
    [InlineData(EstadoUnidadInventario.EnTransito, true)]
    [InlineData(EstadoUnidadInventario.Disponible, false)]
    [InlineData(EstadoUnidadInventario.Vendida, false)]
    [InlineData(EstadoUnidadInventario.Entregada, false)]
    public void Recepcion_SoloPermiteEstadosFisicosPreviosAlIngreso(
        EstadoUnidadInventario estado,
        bool esperada)
    {
        Assert.Equal(esperada, InventarioPresentacion.PuedeRecibirse(Unidad(estado)));
    }

    private static UnidadInventarioDto Unidad(
        EstadoUnidadInventario estado,
        int? detallePedidoReservaId = null) =>
        new(
            1,
            "UNI-001",
            estado,
            null,
            40m,
            2,
            "Producto",
            "PROD-001",
            3,
            OrigenCompra.Importacion,
            detallePedidoReservaId,
            detallePedidoReservaId.HasValue ? 11 : null,
            detallePedidoReservaId.HasValue ? 9 : null);
}

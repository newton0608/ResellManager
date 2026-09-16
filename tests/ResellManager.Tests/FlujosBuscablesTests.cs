using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Pages;
using ResellManager.Web.Components.Pedidos;
using ResellManager.Web.Components.Ventas;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class FlujosBuscablesTests
{
    [Theory]
    [InlineData(false, "valido")]
    [InlineData(true, "valido")]
    [InlineData(false, "0")]
    [InlineData(true, "0")]
    [InlineData(false, "abc")]
    [InlineData(true, "abc")]
    [InlineData(false, "999999")]
    [InlineData(true, "999999")]
    public async Task ClienteDesdeQuery_PreseleccionaOControlaErrorYPermiteCambiar(bool directa, string query)
    {
        await using var test = await TestDatabase.CreateAsync();
        var service = new ClienteService(test.Db);
        var cliente = (await service.ObtenerPorIdAsync(test.Cliente.Id)).Value!;
        object form = directa ? new VentaDirectaForm() : new PedidoNuevo();
        Set(form, "ClienteService", service);
        Set(form, "ClienteDesdeQuery", query == "valido" ? cliente.Id.ToString() : query);
        if (directa) Set(form, "Logger", NullLogger<VentaDirectaForm>.Instance);
        else Set(form, "Logger", NullLogger<PedidoNuevo>.Instance);
        await CallAsync(form, "CargarAsync");
        Assert.Equal(query == "valido" ? cliente.Id : null, Get<ClienteDto?>(form, "ClienteSeleccionado")?.Id);
        if (query != "valido") Assert.Contains("cliente", Get<string>(form, "ErrorGuardado"));
        Call(form, directa ? "ElegirCliente" : "SeleccionarCliente", cliente);
        Assert.Equal(cliente.Id, Get<ClienteDto>(form, "ClienteSeleccionado").Id);
        Assert.Null(Get<string?>(form, "ErrorGuardado"));
        if (directa) Assert.Equal(cliente.Id, Get<VentaDirectaFormModel>(form, "Modelo").ClienteId);
        else Assert.Equal(cliente.Id, Get<PedidoFormModel>(form, "Modelo").ClienteId);
    }

    [Fact]
    public async Task VentaDirecta_AgregaVariasSinDuplicar_QuitaYConservaPrecioEditable()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearUnidadDisponibleAsync("UNO");
        await test.CrearUnidadDisponibleAsync("DOS");
        var consulta = new SeleccionOperativaService(test.Db);
        var opciones = await consulta.BuscarUnidadesAsync("");
        var form = new VentaDirectaForm();
        Set(form, "ProductoService", new ProductoService(test.Db));
        Set(form, "Logger", NullLogger<VentaDirectaForm>.Instance);
        await CallAsync(form, "AgregarUnidadAsync", opciones[0]);
        await CallAsync(form, "AgregarUnidadAsync", opciones[0]);
        await CallAsync(form, "AgregarUnidadAsync", opciones[1]);
        var unidades = Get<List<UnidadVentaDirectaFormModel>>(form, "Unidades");
        Assert.Equal(2, unidades.Count);
        Assert.All(unidades, x => Assert.Equal(test.Producto.PrecioSugerido, x.PrecioFinal));
        unidades[0].PrecioFinal = 155;
        Call(form, "QuitarUnidad", unidades[1]);
        Assert.Equal(155, Assert.Single(unidades).PrecioFinal);
        Set(form, "RevisandoVenta", true);
        await CallAsync(form, "AgregarUnidadAsync", opciones[1]);
        Call(form, "QuitarUnidad", unidades[0]);
        Assert.Single(unidades);
    }

    [Fact]
    public async Task Pedido_ProductoSeleccionadoSugierePrecioYCatalogoLimpiaIntencion()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearUnidadDisponibleAsync("UNO");
        var producto = (await new ProductoService(test.Db).ObtenerPorIdAsync(test.Producto.Id)).Value!;
        var form = new PedidoNuevo();
        var modelo = Get<PedidoFormModel>(form, "Modelo");
        var detalle = modelo.Detalles[0];
        Call(form, "ElegirProducto", detalle, producto);
        Assert.Equal(producto.Id, detalle.ProductoId);
        Assert.Equal(producto.PrecioSugerido, detalle.PrecioUnitario);
        detalle.Reservas.Add((await new SeleccionOperativaService(test.Db).BuscarUnidadesAsync("")).Single());
        modelo.TipoPedido = TipoPedido.Catalogo;
        Call(form, "CambiarTipo");
        Assert.Empty(detalle.Reservas);
        Assert.Single(modelo.Detalles);
    }
}

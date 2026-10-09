using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Infrastructure.Storage;

namespace ResellManager.Tests;

public sealed class CatalogoPublicoTests
{
    [Fact]
    public async Task InventarioLibre_ListaProductoUnaVezConPrecioSugeridoYSinRastrearEntidades()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearUnidadDisponibleAsync("COM-LIBRE-1");
        await test.CrearUnidadDisponibleAsync("COM-LIBRE-2");
        test.Db.ChangeTracker.Clear();

        var producto = Assert.Single(await Servicio(test).ListarAsync());

        Assert.Equal(test.Producto.Id, producto.Id);
        Assert.Equal(test.Producto.Nombre, producto.Nombre);
        Assert.Equal(test.Categoria.Id, producto.CategoriaId);
        Assert.Equal(test.Categoria.Nombre, producto.Categoria);
        Assert.Equal(test.Producto.PrecioSugerido, producto.PrecioPublico);
        Assert.True(producto.Disponible);
        Assert.False(producto.TieneImagenPrincipal);
        Assert.Empty(test.Db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task SinUnidades_NoListaProductoNiPermiteDetalle()
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = Servicio(test);

        Assert.Empty(await servicio.ListarAsync());
        Assert.False((await servicio.ObtenerPorIdAsync(test.Producto.Id)).IsSuccess);
    }

    [Theory]
    [InlineData(EstadoUnidadInventario.Comprada)]
    [InlineData(EstadoUnidadInventario.EnTransito)]
    [InlineData(EstadoUnidadInventario.Vendida)]
    [InlineData(EstadoUnidadInventario.Entregada)]
    [InlineData(EstadoUnidadInventario.Perdida)]
    public async Task EstadoFisicoNoDisponible_ExcluyeProducto(EstadoUnidadInventario estado)
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("COM-ESTADO");
        unidad.Estado = estado;
        await test.Db.SaveChangesAsync();

        Assert.Empty(await Servicio(test).ListarAsync());
        Assert.False((await Servicio(test).ObtenerPorIdAsync(test.Producto.Id)).IsSuccess);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReservaExcluyeProducto_HastaCancelarReservaOPedido(bool cancelarPedido)
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("COM-RESERVA");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-RESERVA");
        var inventario = new InventarioService(test.Db);
        var reserva = await inventario.ReservarAsync(unidad.Id, pedido.Detalles.Single().Id);
        Assert.True(reserva.IsSuccess, reserva.ErrorMessage);
        Assert.Equal(EstadoUnidadInventario.Disponible, unidad.Estado);
        var servicio = Servicio(test);
        Assert.Empty(await servicio.ListarAsync());
        Assert.False((await servicio.ObtenerPorIdAsync(test.Producto.Id)).IsSuccess);

        if (cancelarPedido)
            Assert.True((await new PedidoService(test.Db).CancelarAsync(pedido.Id)).IsSuccess);
        else
            Assert.True((await inventario.CancelarReservaAsync(unidad.Id)).IsSuccess);

        Assert.Equal(test.Producto.Id, Assert.Single(await servicio.ListarAsync()).Id);
        Assert.Null(unidad.DetallePedidoReservaId);
    }

    [Fact]
    public async Task RecibirUnidadReservada_NoLaHaceDisponibleParaCatalogo()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("COM-RECEPCION");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Importacion, "PED-RECEPCION");
        var inventario = new InventarioService(test.Db);
        Assert.True((await inventario.ReservarAsync(unidad.Id, pedido.Detalles.Single().Id)).IsSuccess);
        Assert.True((await inventario.RegistrarRecepcionAsync(
            new RecepcionMercanciaInput(new DateOnly(2026, 2, 10), [unidad.Id]))).IsSuccess);

        Assert.Equal(EstadoUnidadInventario.Disponible, unidad.Estado);
        Assert.NotNull(unidad.DetallePedidoReservaId);
        Assert.Empty(await Servicio(test).ListarAsync());
    }

    [Fact]
    public async Task VentaRegistradaExcluyeProducto_CancelarlaPermiteHistorialSinBloquearStock()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("COM-VENTA");
        var venta = await VenderAsync(test, unidad, "VENTA");
        var servicio = Servicio(test);
        Assert.Empty(await servicio.ListarAsync());
        Assert.False((await servicio.ObtenerPorIdAsync(test.Producto.Id)).IsSuccess);

        Assert.True((await new VentaService(test.Db).CancelarAsync(venta.Id)).IsSuccess);

        Assert.Equal(test.Producto.Id, Assert.Single(await servicio.ListarAsync()).Id);
        Assert.Equal(EstadoUnidadInventario.Disponible, unidad.Estado);
        Assert.Single(await test.Db.DetallesVenta.ToListAsync());
    }

    [Fact]
    public async Task VentaActiva_ExcluyeUnidadAunqueSuEstadoFisicoSeaInconsistente()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("COM-INCONSISTENTE");
        await VenderAsync(test, unidad, "ACTIVA");
        // Reproduce datos inconsistentes: VentaService también rechaza esta unidad.
        unidad.Estado = EstadoUnidadInventario.Disponible;
        await test.Db.SaveChangesAsync();

        Assert.Empty(await Servicio(test).ListarAsync());
        Assert.False((await Servicio(test).ObtenerPorIdAsync(test.Producto.Id)).IsSuccess);
    }

    [Fact]
    public async Task UnaUnidadLibreBasta_AunqueOtrasEstenReservadasOVendidas()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearUnidadDisponibleAsync("COM-LIBRE");
        var reservada = await test.CrearUnidadDisponibleAsync("COM-APARTADA");
        var vendida = await test.CrearUnidadDisponibleAsync("COM-VENDIDA");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-APARTADA");
        Assert.True((await new InventarioService(test.Db)
            .ReservarAsync(reservada.Id, pedido.Detalles.Single().Id)).IsSuccess);
        await VenderAsync(test, vendida, "MIXTA");

        Assert.Equal(test.Producto.Id, Assert.Single(await Servicio(test).ListarAsync()).Id);
    }

    [Fact]
    public async Task Categoria_CombinaFiltroYBusquedaSinIncluirProductosSinStock()
    {
        await using var test = await TestDatabase.CreateAsync();
        var categoria = new Categoria { Nombre = "Perfumes", Observaciones = "Privado" };
        test.Db.Categorias.Add(categoria);
        await test.Db.SaveChangesAsync();
        var perfume = await test.CrearProductoAsync("PER-1");
        perfume.Nombre = "Versace perfume";
        perfume.CategoriaId = categoria.Id;
        test.Producto.Nombre = "Versace accesorio";
        await test.Db.SaveChangesAsync();
        await test.CrearUnidadDisponibleAsync("COM-PERFUME", perfume);
        await test.CrearUnidadDisponibleAsync("COM-ACCESORIO");
        var servicio = Servicio(test);

        Assert.Equal(2, (await servicio.ListarAsync()).Count);
        Assert.Equal(perfume.Id, Assert.Single(await servicio.ListarAsync(categoriaId: categoria.Id)).Id);
        Assert.Equal(perfume.Id, Assert.Single(await servicio.ListarAsync(" VERSACE ", categoria.Id)).Id);
        Assert.Empty(await servicio.ListarAsync("accesorio", categoria.Id));
        Assert.Empty(await servicio.ListarAsync(categoriaId: int.MaxValue));
    }

    [Theory]
    [InlineData(" VERSACE ")]
    [InlineData("versace")]
    [InlineData(" prod-1 ")]
    [InlineData(" 7501234567890 ")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Busqueda_UsaNombreCodigoInternoYCodigoBarrasSoloConStock(string? termino)
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Producto.Nombre = "Perfume Versace";
        test.Producto.CodigoBarras = "7501234567890";
        var sinStock = await test.CrearProductoAsync("PROD-1-SIN-STOCK");
        sinStock.Nombre = test.Producto.Nombre;
        sinStock.CodigoBarras = test.Producto.CodigoBarras;
        await test.Db.SaveChangesAsync();
        await test.CrearUnidadDisponibleAsync("COM-BUSQUEDA");

        Assert.Equal(test.Producto.Id, Assert.Single(await Servicio(test).ListarAsync(termino)).Id);
        Assert.Empty(await Servicio(test).ListarAsync("no existe"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task DetalleInexistente_DevuelveFalloSinDatos(int id)
    {
        await using var test = await TestDatabase.CreateAsync();
        var resultado = await Servicio(test).ObtenerPorIdAsync(id);

        Assert.False(resultado.IsSuccess);
        Assert.Null(resultado.Value);
        Assert.Equal("Producto no encontrado.", resultado.ErrorMessage);
    }

    [Fact]
    public async Task Detalle_ProyectaAtributosComercialesYMedidasExistentes()
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Producto.Descripcion = "Perfume floral";
        test.Producto.Marca = "Marca";
        test.Producto.Modelo = "Modelo";
        test.Producto.Color = "Rojo";
        test.Producto.Talla = "Única";
        test.Producto.ContenidoMl = 100m;
        test.Producto.Presentacion = "Frasco";
        await test.Db.SaveChangesAsync();
        await test.CrearUnidadDisponibleAsync("COM-DETALLE");

        var resultado = await Servicio(test).ObtenerPorIdAsync(test.Producto.Id);

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        var detalle = resultado.Value!;
        Assert.Equal(test.Producto.Descripcion, detalle.Descripcion);
        Assert.Equal(test.Producto.Marca, detalle.Marca);
        Assert.Equal(test.Producto.Modelo, detalle.Modelo);
        Assert.Equal(test.Producto.Color, detalle.Color);
        Assert.Equal(test.Producto.Talla, detalle.Talla);
        Assert.Equal(test.Producto.ContenidoMl, detalle.ContenidoMl);
        Assert.Null(detalle.PesoGramos);
        Assert.Equal(test.Producto.Presentacion, detalle.Presentacion);
        Assert.Equal(test.Producto.PrecioSugerido, detalle.PrecioPublico);
        Assert.True(detalle.Disponible);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("productos/1/imagen-principal-00000000000000000000000000000001.webp", true)]
    public async Task IndicadorImagen_DependeDeReferenciaSinPublicarRuta(string? ruta, bool esperada)
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Producto.ImagenPrincipalRuta = ruta;
        await test.Db.SaveChangesAsync();
        await test.CrearUnidadDisponibleAsync("COM-IMAGEN");
        var servicio = Servicio(test);

        Assert.Equal(esperada, Assert.Single(await servicio.ListarAsync()).TieneImagenPrincipal);
        Assert.Equal(esperada, (await servicio.ObtenerPorIdAsync(test.Producto.Id)).Value!.TieneImagenPrincipal);
    }

    [Fact]
    public async Task ContratosPublicos_SerializanUnicamenteCamposComercialesPermitidos()
    {
        await using var test = await TestDatabase.CreateAsync();
        await test.CrearUnidadDisponibleAsync("COM-PRIVADA");
        var servicio = Servicio(test);
        var listado = JsonSerializer.SerializeToElement(Assert.Single(await servicio.ListarAsync()));
        var detalle = JsonSerializer.SerializeToElement((await servicio.ObtenerPorIdAsync(test.Producto.Id)).Value);

        AssertCampos(listado, "Id", "Nombre", "CategoriaId", "Categoria", "PrecioPublico", "TieneImagenPrincipal", "Disponible", "Marca", "CategoriaPadreId", "CategoriaPadreNombre");
        AssertCampos(detalle, "Id", "Nombre", "Descripcion", "Marca", "Modelo", "Color", "Talla",
            "ContenidoMl", "PesoGramos", "Presentacion", "CategoriaId", "Categoria", "PrecioPublico",
            "TieneImagenPrincipal", "Disponible", "Imagenes");
    }

    private static void AssertCampos(JsonElement producto, params string[] permitidos) =>
        Assert.Equal(permitidos.OrderBy(x => x), producto.EnumerateObject().Select(x => x.Name).OrderBy(x => x));

    private static CatalogoPublicoService Servicio(TestDatabase test) => new(test.Db,
        new AlmacenamientoImagenesProductoLocal(
            Options.Create(new AlmacenamientoImagenesProductoOptions
            {
                DirectorioBase = Path.Combine(Path.GetTempPath(), "resellmanager-catalogo-lectura")
            }), NullLogger<AlmacenamientoImagenesProductoLocal>.Instance));

    private static async Task<VentaDto> VenderAsync(TestDatabase test, UnidadInventario unidad, string sufijo)
    {
        var pedido = await test.CrearPedidoAsync(TipoPedido.VentaDirecta, $"PED-{sufijo}");
        var venta = await new VentaService(test.Db).RegistrarDesdePedidoAsync(new VentaInput(
            pedido.Id, $"VEN-{sufijo}", new DateOnly(2026, 2, 2), null,
            [new DetalleVentaInput(unidad.Id, null, null, 99m, null)]));
        Assert.True(venta.IsSuccess, venta.ErrorMessage);
        return venta.Value!;
    }
}

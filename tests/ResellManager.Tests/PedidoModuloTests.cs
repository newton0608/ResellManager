using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

public sealed class PedidoModuloTests
{
    [Fact]
    public async Task CrearPedido_UsaClienteYProductoRealesYComienzaPendiente()
    {
        await using var test = await TestDatabase.CreateAsync();
        var service = new PedidoService(test.Db);

        var result = await service.CrearAsync(Input(test, "PED-CREA"));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(test.Cliente.Id, result.Value!.ClienteId);
        Assert.Equal(test.Producto.Id, result.Value.Detalles.Single().ProductoId);
        Assert.Equal(EstadoPedido.Pendiente, result.Value.Estado);
    }

    [Fact]
    public async Task CrearPedido_CodigoRepetidoSeRechaza()
    {
        await using var test = await TestDatabase.CreateAsync();
        var service = new PedidoService(test.Db);
        var primero = await service.CrearAsync(Input(test, "PED-DUP"));

        var segundo = await service.CrearAsync(Input(test, "PED-DUP"));

        Assert.True(primero.IsSuccess, primero.ErrorMessage);
        Assert.False(segundo.IsSuccess);
        Assert.Contains("ya está registrado", segundo.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, -0.01)]
    public async Task CrearPedido_CantidadOPrecioInvalidoSeRechaza(int cantidad, double precio)
    {
        await using var test = await TestDatabase.CreateAsync();
        var input = Input(test, $"PED-INV-{cantidad}-{precio}") with
        {
            Detalles =
            [
                new DetallePedidoInput(test.Producto.Id, cantidad, Convert.ToDecimal(precio), null),
            ],
        };

        var result = await new PedidoService(test.Db).CrearAsync(input);

        Assert.False(result.IsSuccess);
        Assert.Contains("no válido", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListadoYDetalle_DevuelvenPedidoConSubtotal()
    {
        await using var test = await TestDatabase.CreateAsync();
        var service = new PedidoService(test.Db);
        var creado = await service.CrearAsync(Input(test, "PED-CONSULTA"));

        var listado = await service.ListarAsync();
        var detalle = await service.ObtenerPorIdAsync(creado.Value!.Id);

        Assert.Contains(listado, x => x.Id == creado.Value.Id);
        Assert.True(detalle.IsSuccess, detalle.ErrorMessage);
        Assert.Equal(240m, detalle.Value!.Detalles.Single().Subtotal);
        Assert.Equal("Cliente", detalle.Value.Cliente);
    }

    [Fact]
    public async Task AgregarDetalle_UsaProductoReal()
    {
        await using var test = await TestDatabase.CreateAsync();
        var otroProducto = await test.CrearProductoAsync("PROD-AGREGA", 75m);
        var pedido = await new PedidoService(test.Db).CrearAsync(Input(test, "PED-AGREGA"));
        var service = new PedidoService(test.Db);

        var result = await service.AgregarDetalleAsync(
            pedido.Value!.Id,
            new DetallePedidoInput(otroProducto.Id, 2, 70m, "Precio acordado")
        );

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(2, result.Value!.Detalles.Count);
        Assert.Contains(result.Value.Detalles, x =>
            x.ProductoId == otroProducto.Id && x.PrecioUnitario == 70m);
    }

    [Theory]
    [InlineData(EstadoPedido.Cancelado)]
    [InlineData(EstadoPedido.Completado)]
    public async Task AgregarDetalle_NoModificaPedidoTerminal(EstadoPedido estado)
    {
        await using var test = await TestDatabase.CreateAsync();
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, $"PED-{estado}");
        pedido.Estado = estado;
        await test.Db.SaveChangesAsync();

        var result = await new PedidoService(test.Db).AgregarDetalleAsync(
            pedido.Id,
            new DetallePedidoInput(test.Producto.Id, 1, 50m, null)
        );

        Assert.False(result.IsSuccess);
        Assert.Contains("no se puede modificar", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReservarUnidad_CompradaDelMismoProductoSeAcepta()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadImportadaAsync("IMP-COMPRADA-RES");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Importacion, "PED-COMPRADA-RES");

        var result = await new InventarioService(test.Db).ReservarAsync(
            unidad.Id,
            pedido.Detalles.Single().Id
        );

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(EstadoUnidadInventario.Comprada, result.Value!.Estado);
        Assert.Equal(pedido.Detalles.Single().Id, result.Value.DetallePedidoReservaId);
    }

    [Fact]
    public async Task ReservarUnidad_DeProductoDiferenteSeRechaza()
    {
        await using var test = await TestDatabase.CreateAsync();
        var otroProducto = await test.CrearProductoAsync("PROD-DIF");
        var unidad = await test.CrearUnidadDisponibleAsync("LOC-PROD-DIF", otroProducto);
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-PROD-DIF");

        var result = await new InventarioService(test.Db).ReservarAsync(
            unidad.Id,
            pedido.Detalles.Single().Id
        );

        Assert.False(result.IsSuccess);
        Assert.Contains("mismo producto", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReservarUnidad_VendidaOEntregadaSeRechaza(bool entregada)
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync($"LOC-TERMINAL-{entregada}");
        var pedidoVenta = await test.CrearPedidoAsync(
            TipoPedido.VentaDirecta,
            $"PED-VENDE-{entregada}"
        );
        var venta = await new VentaService(test.Db).RegistrarDesdePedidoAsync(
            new VentaInput(
                pedidoVenta.Id,
                $"VEN-TERMINAL-{entregada}",
                new DateOnly(2026, 2, 2),
                null,
                [new DetalleVentaInput(unidad.Id, null, null, 100m, null)]
            )
        );
        Assert.True(venta.IsSuccess, venta.ErrorMessage);
        if (entregada)
        {
            var cambio = await new InventarioService(test.Db).CambiarEstadoAsync(
                unidad.Id,
                EstadoUnidadInventario.Entregada
            );
            Assert.True(cambio.IsSuccess, cambio.ErrorMessage);
        }

        var otroPedido = await test.CrearPedidoAsync(
            TipoPedido.Apartado,
            $"PED-RESERVA-TERMINAL-{entregada}"
        );
        var result = await new InventarioService(test.Db).ReservarAsync(
            unidad.Id,
            otroPedido.Detalles.Single().Id
        );

        Assert.False(result.IsSuccess);
        Assert.Contains("vendida o entregada", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReservarUnidad_ReservadaPorOtroDetalleSeRechaza()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad = await test.CrearUnidadDisponibleAsync("LOC-OTRO-DETALLE");
        var primerPedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-PRIMERO");
        var segundoPedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-SEGUNDO");
        var service = new InventarioService(test.Db);
        var primeraReserva = await service.ReservarAsync(
            unidad.Id,
            primerPedido.Detalles.Single().Id
        );

        var segundaReserva = await service.ReservarAsync(
            unidad.Id,
            segundoPedido.Detalles.Single().Id
        );

        Assert.True(primeraReserva.IsSuccess, primeraReserva.ErrorMessage);
        Assert.False(segundaReserva.IsSuccess);
        Assert.Contains("otro detalle", segundaReserva.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReservarUnidad_NoExcedeCantidadSolicitada()
    {
        await using var test = await TestDatabase.CreateAsync();
        var unidad1 = await test.CrearUnidadDisponibleAsync("LOC-LIMITE-1");
        var unidad2 = await test.CrearUnidadDisponibleAsync("LOC-LIMITE-2");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-LIMITE", cantidad: 1);
        var service = new InventarioService(test.Db);
        var primera = await service.ReservarAsync(unidad1.Id, pedido.Detalles.Single().Id);

        var segunda = await service.ReservarAsync(unidad2.Id, pedido.Detalles.Single().Id);

        Assert.True(primera.IsSuccess, primera.ErrorMessage);
        Assert.False(segunda.IsSuccess);
        Assert.Contains("todas sus unidades", segunda.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelarPedido_LiberaReservasYConservaComprasUnidadesYEstadosFisicos()
    {
        await using var test = await TestDatabase.CreateAsync();
        var comprada = await test.CrearUnidadImportadaAsync("IMP-CANCELA-1");
        var enTransito = await test.CrearUnidadImportadaAsync("IMP-CANCELA-2");
        var inventario = new InventarioService(test.Db);
        var cambio = await inventario.CambiarEstadoAsync(
            enTransito.Id,
            EstadoUnidadInventario.EnTransito
        );
        Assert.True(cambio.IsSuccess, cambio.ErrorMessage);
        var pedido = await test.CrearPedidoAsync(
            TipoPedido.Importacion,
            "PED-CANCELA-TODO",
            cantidad: 2
        );
        Assert.True((await inventario.ReservarAsync(comprada.Id, pedido.Detalles.Single().Id)).IsSuccess);
        Assert.True((await inventario.ReservarAsync(enTransito.Id, pedido.Detalles.Single().Id)).IsSuccess);
        var comprasAntes = await test.Db.Compras.CountAsync();
        var unidadesAntes = await test.Db.UnidadesInventario.CountAsync();

        var result = await new PedidoService(test.Db).CancelarAsync(pedido.Id);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        await test.Db.Entry(pedido).ReloadAsync();
        await test.Db.Entry(comprada).ReloadAsync();
        await test.Db.Entry(enTransito).ReloadAsync();
        Assert.Equal(EstadoPedido.Cancelado, pedido.Estado);
        Assert.Null(comprada.DetallePedidoReservaId);
        Assert.Null(enTransito.DetallePedidoReservaId);
        Assert.Equal(EstadoUnidadInventario.Comprada, comprada.Estado);
        Assert.Equal(EstadoUnidadInventario.EnTransito, enTransito.Estado);
        Assert.Equal(comprasAntes, await test.Db.Compras.CountAsync());
        Assert.Equal(unidadesAntes, await test.Db.UnidadesInventario.CountAsync());
    }

    [Fact]
    public async Task CancelarPedido_ConVentaRegistradaSeRechaza()
    {
        await using var test = await TestDatabase.CreateAsync();
        var venta = await test.CrearVentaCatalogoAsync("PED-CON-VENTA", "VEN-ACTIVA");
        var pedido = await test.Db.Pedidos.SingleAsync(x => x.Id == venta.PedidoId);

        var result = await new PedidoService(test.Db).CancelarAsync(pedido.Id);

        Assert.False(result.IsSuccess);
        Assert.Contains("venta registrada", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        await test.Db.Entry(pedido).ReloadAsync();
        Assert.Equal(EstadoPedido.Completado, pedido.Estado);
    }

    private static PedidoInput Input(TestDatabase test, string codigo) =>
        new(
            codigo,
            new DateOnly(2026, 8, 25),
            TipoPedido.Apartado,
            CanalVenta.Facebook,
            test.Cliente.Id,
            "Pedido de prueba",
            [new DetallePedidoInput(test.Producto.Id, 2, 120m, null)]
        );
}

[CollectionDefinition("Pedidos UI no paralela", DisableParallelization = true)]
public sealed class PedidosUiNoParalelaCollection;

[Collection("Pedidos UI no paralela")]
public sealed class PedidosUiIntegracionTests : PruebaWebAislada
{
    [Fact]
    public async Task RutasPedidos_EstanProtegidas()
    {
        using var cliente = CrearCliente();

        foreach (var ruta in new[] { "/pedidos", "/pedidos/nuevo", "/pedidos/999999" })
        {
            var respuesta = await cliente.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
            Assert.Equal("/login", respuesta.Headers.Location?.AbsolutePath);
        }
    }

    [Fact]
    public async Task NuevoPedido_UsaSelectoresRealesYSinCapturaManualDeIds()
    {
        using var cliente = CrearCliente();
        await IniciarSesionAsync(cliente);

        var contenido = WebUtility.HtmlDecode(await cliente.GetStringAsync("/pedidos/nuevo"));

        Assert.Contains("Selecciona un cliente", contenido);
        Assert.Contains("Selecciona un producto", contenido);
        Assert.Contains("Precio unitario", contenido);
        Assert.Contains("Canal de venta", contenido);
        Assert.Contains("Presencial", contenido);
        Assert.Contains("WhatsApp", contenido);
        Assert.Contains("Facebook", contenido);
        Assert.Contains("Web", contenido);
        Assert.Contains("Otro", contenido);
        Assert.DoesNotContain("ID de cliente", contenido, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ID de producto", contenido, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("order-line-grid", contenido);
    }

    [Fact]
    public async Task ListadoYDetalle_MuestranCanalConEtiquetaAmigable()
    {
        var pedidoId = await CrearPedidoAsync(
            TipoPedido.Catalogo,
            EstadoPedido.Pendiente,
            CanalVenta.Facebook
        );
        using var cliente = CrearCliente();
        await IniciarSesionAsync(cliente);

        var listado = WebUtility.HtmlDecode(await cliente.GetStringAsync("/pedidos"));
        var detalle = WebUtility.HtmlDecode(await cliente.GetStringAsync($"/pedidos/{pedidoId}"));

        Assert.Contains("Canal", listado);
        Assert.Contains("Facebook", listado);
        Assert.Contains("<dt>Canal</dt><dd>Facebook</dd>", detalle);
    }

    [Fact]
    public async Task DetalleCatalogo_NoMuestraAccionesDeInventario()
    {
        var pedidoId = await CrearPedidoAsync(TipoPedido.Catalogo, EstadoPedido.Pendiente);
        using var cliente = CrearCliente();
        await IniciarSesionAsync(cliente);

        var contenido = WebUtility.HtmlDecode(
            await cliente.GetStringAsync($"/pedidos/{pedidoId}")
        );

        Assert.Contains("Pedido de catálogo", contenido);
        Assert.Contains("no utiliza ni reserva unidades físicas", contenido);
        Assert.DoesNotContain("Reservar unidad", contenido);
        Assert.DoesNotContain("Unidad compatible", contenido);
        Assert.Contains("Agregar otro producto al pedido", contenido);
    }

    [Fact]
    public async Task DetalleFisico_SeparaEstadoYReservaYMuestraAccionesModificables()
    {
        var pedidoId = await CrearPedidoAsync(TipoPedido.Importacion, EstadoPedido.Pendiente);
        using var cliente = CrearCliente();
        await IniciarSesionAsync(cliente);

        var contenido = WebUtility.HtmlDecode(
            await cliente.GetStringAsync($"/pedidos/{pedidoId}")
        );

        Assert.Contains("Detalles y reservas", contenido);
        Assert.Contains("estado físico", contenido, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reservar unidad", contenido);
        Assert.Contains("Cancelar pedido", contenido);
        Assert.DoesNotContain("EstadoUnidadInventario.Apartada", contenido);
    }

    [Theory]
    [InlineData(EstadoPedido.Cancelado)]
    [InlineData(EstadoPedido.Completado)]
    public async Task DetalleTerminal_EsSoloLectura(EstadoPedido estado)
    {
        var pedidoId = await CrearPedidoAsync(TipoPedido.Apartado, estado);
        using var cliente = CrearCliente();
        await IniciarSesionAsync(cliente);

        var contenido = WebUtility.HtmlDecode(
            await cliente.GetStringAsync($"/pedidos/{pedidoId}")
        );

        Assert.Contains(estado == EstadoPedido.Cancelado ? "Cancelado" : "Completado", contenido);
        Assert.DoesNotContain("Reservar unidad", contenido);
        Assert.DoesNotContain("Cancelar pedido", contenido);
        Assert.DoesNotContain("Agregar otro producto al pedido", contenido);
    }

    private async Task<int> CrearPedidoAsync(
        TipoPedido tipo,
        EstadoPedido estado,
        CanalVenta canalVenta = CanalVenta.Otro
    )
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        var categoria = new Categoria { Nombre = $"Categoría pedidos {sufijo}" };
        var cliente = new Cliente
        {
            Nombres = "Cliente UI pedidos",
            Telefono = $"555-{sufijo[..4]}",
        };
        var producto = new Producto
        {
            CodigoInterno = $"PROD-UI-{sufijo}",
            Nombre = "Producto UI pedidos",
            PrecioSugerido = 150m,
            Categoria = categoria,
        };
        var pedido = new Pedido
        {
            CodigoInterno = $"PED-UI-{sufijo}",
            Fecha = new DateOnly(2026, 8, 25),
            TipoPedido = tipo,
            CanalVenta = canalVenta,
            Estado = estado,
            Cliente = cliente,
            Detalles =
            [
                new DetallePedido
                {
                    Producto = producto,
                    Cantidad = 1,
                    PrecioUnitario = 150m,
                },
            ],
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        if (tipo != TipoPedido.Catalogo && estado == EstadoPedido.Pendiente)
        {
            var proveedor = new Proveedor { Nombre = $"Proveedor UI {sufijo}" };
            db.Proveedores.Add(proveedor);
            await db.SaveChangesAsync();
            var compra = await new CompraService(db).RegistrarAsync(
                new CompraInput(
                    $"COMPRA-UI-{sufijo}",
                    new DateOnly(2026, 8, 24),
                    null,
                    OrigenCompra.Importacion,
                    proveedor.Id,
                    null,
                    [new DetalleCompraInput(producto.Id, 1, 75m)],
                    null
                )
            );
            Assert.True(compra.IsSuccess, compra.ErrorMessage);
        }

        return pedido.Id;
    }

    private HttpClient CrearCliente() =>
        factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
            }
        );

    private static async Task IniciarSesionAsync(HttpClient cliente)
    {
        var paginaLogin = await cliente.GetAsync("/login");
        var contenido = await paginaLogin.Content.ReadAsStringAsync();
        var etiqueta = Regex.Match(
            contenido,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
            RegexOptions.IgnoreCase
        );
        var valor = Regex.Match(etiqueta.Value, "value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        using var formulario = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["correo"] = AplicacionAutenticacionFactory.CorreoUsuario,
                ["contrasena"] = AplicacionAutenticacionFactory.ContrasenaValida,
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(valor.Groups[1].Value),
            }
        );

        var respuesta = await cliente.PostAsync("/account/login", formulario);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
    }
}

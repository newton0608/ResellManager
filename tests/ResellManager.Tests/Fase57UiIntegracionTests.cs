using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResellManager.Application.DTOs;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Services;

namespace ResellManager.Tests;

[CollectionDefinition("Integración web", DisableParallelization = true)]
public sealed class IntegracionWebCollection { }

[Collection("Integración web")]

public sealed class Fase57UiIntegracionTests(AplicacionAutenticacionFactory factory)
    : IClassFixture<AplicacionAutenticacionFactory>
{
    [Fact]
    public async Task RutasVentasYPagos_EstanProtegidasGlobalmente()
    {
        using var cliente = CrearCliente();

        foreach (var ruta in new[] { "/ventas", "/ventas/nueva", "/ventas/999999", "/pagos" })
        {
            var respuesta = await cliente.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
            Assert.Equal("/login", respuesta.Headers.Location?.AbsolutePath);
        }
    }

    [Fact]
    public async Task VentaCatalogo_NoRenderizaSelectorDeUnidadNiIdsManuales()
    {
        var escenario = await CrearEscenarioCatalogoAsync(registrarVenta: false);
        using var cliente = CrearCliente();
        await IniciarSesionAsync(cliente);

        var respuesta = await cliente.GetAsync($"/ventas/nueva?pedido={escenario.PedidoId}");
        var contenido = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Captura manualmente el costo histórico", contenido);
        Assert.Contains("Costo unitario", contenido);
        Assert.Contains("Precio final", contenido);
        Assert.DoesNotContain("Unidad de inventario", contenido);
        Assert.DoesNotContain("Ingresa el ID", contenido, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VentaFisica_RenderizaSoloUnidadesDisponiblesCompatiblesYPriorizaReservaPropia()
    {
        var escenario = await CrearEscenarioFisicoAsync();
        using var cliente = CrearCliente();
        await IniciarSesionAsync(cliente);

        var respuesta = await cliente.GetAsync($"/ventas/nueva?pedido={escenario.PedidoId}");
        var contenido = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Unidad de inventario", contenido);
        Assert.Contains(escenario.UnidadReservadaPropia, contenido);
        Assert.Contains("reservada para este pedido", contenido);
        Assert.Contains(escenario.UnidadLibre, contenido);
        Assert.DoesNotContain(escenario.UnidadReservadaAjena, contenido);
        Assert.DoesNotContain("Ingresa el ID", contenido, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DetalleVenta_MuestraCancelarSoloMientrasEstaRegistradaYMarkupResponsive()
    {
        var escenario = await CrearEscenarioCatalogoAsync(registrarVenta: true);
        using var cliente = CrearCliente();
        await IniciarSesionAsync(cliente);

        var registrada = await cliente.GetAsync($"/ventas/{escenario.VentaId}");
        var contenidoRegistrada = WebUtility.HtmlDecode(await registrada.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, registrada.StatusCode);
        Assert.Contains("Cancelar venta", contenidoRegistrada);
        Assert.Contains("Costo unitario", contenidoRegistrada);
        Assert.Contains("Utilidad", contenidoRegistrada);
        Assert.Contains("sale-desktop-table", contenidoRegistrada);
        Assert.Contains("sale-mobile-cards", contenidoRegistrada);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
            var cancelacion = await new VentaService(db).CancelarAsync(escenario.VentaId!.Value);
            Assert.True(cancelacion.IsSuccess, cancelacion.ErrorMessage);
        }

        var cancelada = await cliente.GetAsync($"/ventas/{escenario.VentaId}");
        var contenidoCancelada = WebUtility.HtmlDecode(await cancelada.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, cancelada.StatusCode);
        Assert.Contains("Cancelada", contenidoCancelada);
        Assert.DoesNotContain(">Cancelar venta<", contenidoCancelada);
    }

    [Fact]
    public async Task Pagos_MuestraSaldoDelBackendEnumRealHistorialYMarkupResponsive()
    {
        var escenario = await CrearEscenarioCatalogoAsync(registrarVenta: true, precio: 123.45m);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
            var pago = await new PagoService(db).RegistrarAsync(
                new PagoInput(escenario.ClienteId, new DateOnly(2026, 8, 22), 23.45m,
                    MetodoPago.Transferencia, "UI-PAGO", "Pago para historial"));
            Assert.True(pago.IsSuccess, pago.ErrorMessage);
        }

        using var cliente = CrearCliente();
        await IniciarSesionAsync(cliente);

        var respuesta = await cliente.GetAsync($"/pagos?cliente={escenario.ClienteId}");
        var contenido = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("Saldo actual provisto por el sistema", contenido);
        Assert.Contains("Q 100.00", contenido);
        Assert.Contains("Efectivo", contenido);
        Assert.Contains("Transferencia", contenido);
        Assert.Contains("Depósito", contenido);
        Assert.Contains("Tarjeta", contenido);
        Assert.Contains("Otro", contenido);
        Assert.Contains("Historial de pagos", contenido);
        Assert.Contains("payment-desktop-table", contenido);
        Assert.Contains("payment-mobile-cards", contenido);
        Assert.Contains("no se asigna a una venta específica", contenido, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListadoVentas_RenderizaTablaEscritorioYCardsMovil()
    {
        var escenario = await CrearEscenarioCatalogoAsync(registrarVenta: true);
        using var cliente = CrearCliente();
        await IniciarSesionAsync(cliente);

        var respuesta = await cliente.GetAsync("/ventas");
        var contenido = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains(escenario.CodigoVenta!, contenido);
        Assert.Contains("sale-desktop-table", contenido);
        Assert.Contains("sale-mobile-cards", contenido);
        Assert.Contains("Registrada", contenido);
        Assert.Contains("Ver venta", contenido);
    }

    private async Task<EscenarioCatalogo> CrearEscenarioCatalogoAsync(
        bool registrarVenta,
        decimal precio = 100m)
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        var categoria = new Categoria { Nombre = $"Categoría UI {sufijo}" };
        var cliente = new Cliente
        {
            Nombres = $"Cliente UI {sufijo}",
            Telefono = "555-5700",
        };
        var producto = new Producto
        {
            CodigoInterno = $"PROD-UI-{sufijo}",
            Nombre = $"Producto UI {sufijo}",
            PrecioSugerido = 150m,
            Categoria = categoria,
        };
        db.AddRange(categoria, cliente, producto);
        await db.SaveChangesAsync();
        var pedido = new Pedido
        {
            CodigoInterno = $"PED-UI-{sufijo}",
            Fecha = new DateOnly(2026, 8, 20),
            TipoPedido = TipoPedido.Catalogo,
            Estado = EstadoPedido.Pendiente,
            ClienteId = cliente.Id,
            Detalles =
            [
                new DetallePedido
                {
                    ProductoId = producto.Id,
                    Cantidad = 1,
                    PrecioUnitario = precio,
                },
            ],
        };
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync();

        int? ventaId = null;
        string? codigoVenta = null;
        if (registrarVenta)
        {
            codigoVenta = $"VEN-UI-{sufijo}";
            var venta = await new VentaService(db).RegistrarDesdePedidoAsync(
                new VentaInput(
                    pedido.Id,
                    codigoVenta,
                    new DateOnly(2026, 8, 21),
                    "Venta de integración UI",
                    [new DetalleVentaInput(null, producto.Id, 40m, precio, null)]));
            Assert.True(venta.IsSuccess, venta.ErrorMessage);
            ventaId = venta.Value!.Id;
        }

        return new EscenarioCatalogo(cliente.Id, pedido.Id, ventaId, codigoVenta);
    }

    private async Task<EscenarioFisico> CrearEscenarioFisicoAsync()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        var categoria = new Categoria { Nombre = $"Categoría física {sufijo}" };
        var proveedor = new Proveedor { Nombre = $"Proveedor físico {sufijo}" };
        var cliente = new Cliente { Nombres = $"Cliente físico {sufijo}", Telefono = "555-5710" };
        var producto = new Producto
        {
            CodigoInterno = $"PROD-FIS-{sufijo}",
            Nombre = $"Producto físico {sufijo}",
            PrecioSugerido = 125m,
            Categoria = categoria,
        };
        db.AddRange(categoria, proveedor, cliente, producto);
        await db.SaveChangesAsync();
        var compra = await new CompraService(db).RegistrarAsync(
            new CompraInput(
                $"COM-FIS-{sufijo}",
                new DateOnly(2026, 8, 18),
                new DateOnly(2026, 8, 19),
                OrigenCompra.CompraLocal,
                proveedor.Id,
                null,
                [new DetalleCompraInput(producto.Id, 3, 45m)],
                null));
        Assert.True(compra.IsSuccess, compra.ErrorMessage);
        var unidades = await db.UnidadesInventario
            .Where(x => x.ProductoId == producto.Id)
            .OrderBy(x => x.Id)
            .ToListAsync();
        var pedido = CrearPedidoFisico($"PED-FIS-{sufijo}", cliente.Id, producto.Id, 2);
        var otroPedido = CrearPedidoFisico($"PED-AJENO-{sufijo}", cliente.Id, producto.Id, 1);
        db.Pedidos.AddRange(pedido, otroPedido);
        await db.SaveChangesAsync();
        var inventario = new InventarioService(db);
        var propia = await inventario.ReservarAsync(unidades[0].Id, pedido.Detalles.Single().Id);
        var ajena = await inventario.ReservarAsync(unidades[2].Id, otroPedido.Detalles.Single().Id);
        Assert.True(propia.IsSuccess, propia.ErrorMessage);
        Assert.True(ajena.IsSuccess, ajena.ErrorMessage);

        return new EscenarioFisico(
            pedido.Id,
            unidades[0].CodigoInterno,
            unidades[1].CodigoInterno,
            unidades[2].CodigoInterno);
    }

    private static Pedido CrearPedidoFisico(string codigo, int clienteId, int productoId, int cantidad) =>
        new()
        {
            CodigoInterno = codigo,
            Fecha = new DateOnly(2026, 8, 20),
            TipoPedido = TipoPedido.VentaDirecta,
            Estado = EstadoPedido.Pendiente,
            ClienteId = clienteId,
            Detalles =
            [
                new DetallePedido
                {
                    ProductoId = productoId,
                    Cantidad = cantidad,
                    PrecioUnitario = 125m,
                },
            ],
        };

    private HttpClient CrearCliente() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

    private static async Task IniciarSesionAsync(HttpClient cliente)
    {
        var paginaLogin = await cliente.GetAsync("/login");
        var token = await ObtenerTokenAntiforgeryAsync(paginaLogin);
        using var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["correo"] = AplicacionAutenticacionFactory.CorreoUsuario,
            ["contrasena"] = AplicacionAutenticacionFactory.ContrasenaValida,
            ["__RequestVerificationToken"] = token,
        });
        var respuesta = await cliente.PostAsync("/account/login", formulario);
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
    }

    private static async Task<string> ObtenerTokenAntiforgeryAsync(HttpResponseMessage respuesta)
    {
        respuesta.EnsureSuccessStatusCode();
        var contenido = await respuesta.Content.ReadAsStringAsync();
        var etiqueta = Regex.Match(
            contenido,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(etiqueta.Success);
        var valor = Regex.Match(etiqueta.Value, "value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(valor.Success);
        return WebUtility.HtmlDecode(valor.Groups[1].Value);
    }

    private sealed record EscenarioCatalogo(
        int ClienteId,
        int PedidoId,
        int? VentaId,
        string? CodigoVenta);

    private sealed record EscenarioFisico(
        int PedidoId,
        string UnidadReservadaPropia,
        string UnidadLibre,
        string UnidadReservadaAjena);
}

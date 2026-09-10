using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Compras;
using ResellManager.Web.Components.Pages;
using ResellManager.Web.Components.Proveedores;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class CompraRevisionProveedorTests
{
    [Theory]
    [InlineData("textiles")]
    [InlineData("TEXTILES")]
    [InlineData("5025551234")]
    public async Task Proveedor_BuscaNombreYTelefono(string termino)
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new ProveedorService(test.Db);
        var creado = await servicio.CrearAsync(new("Textiles del norte", "5025551234", "GT", null));
        Assert.Equal(creado.Value!.Id, Assert.Single(await servicio.BuscarAsync(termino)).Id);
    }

    [Fact]
    public async Task Proveedor_LimitaResultados_PriorizaTelefonoExactoYSinCoincidencias()
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new ProveedorService(test.Db);
        for (var i = 0; i < 15; i++) await servicio.CrearAsync(new($"AAA 555 {i}", null, null, null));
        var exacto = await servicio.CrearAsync(new("ZZZ", "555", null, null));
        var resultados = await servicio.BuscarAsync("555");
        Assert.Equal(12, resultados.Count);
        Assert.Equal(exacto.Value!.Id, resultados[0].Id);
        Assert.Empty(await servicio.BuscarAsync("inexistente"));
    }

    [Fact]
    public async Task Compra_CeroProveedoresPermiteAltaInline_YAutoseleccionSinPerderDatos()
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Db.Proveedores.Remove(test.Proveedor); // Solo SQLite en memoria de esta prueba.
        await test.Db.SaveChangesAsync();
        Assert.Empty(await new ProveedorService(test.Db).BuscarAsync("nuevo"));
        var compra = new CompraNueva();
        var modelo = Get<CompraFormModel>(compra, "Modelo");
        modelo.FechaCompra = new(2026, 9, 1);
        modelo.FechaIngreso = new(2026, 9, 2);
        modelo.Observaciones = "Conservar compra";
        modelo.Detalles[0].Cantidad = 4;
        modelo.Detalles[0].CostoUnitario = 12.50m;
        var archivo = new ArchivoSinLectura();
        Set(compra, "ArchivoSeleccionado", archivo);
        var antes = modelo.ToInput("COM-ESTABLE");
        Call(compra, "AbrirAltaProveedor");
        var panel = new ProveedorAltaPanel();
        Set(panel, "ProveedorService", new ProveedorService(test.Db));
        Set(panel, "Logger", NullLogger<ProveedorAltaPanel>.Instance);
        Set(panel, "OnCreado", EventCallback.Factory.Create<ProveedorDto>(new object(), proveedor => Call(compra, "ProveedorCreado", proveedor)));
        Get<ProveedorFormModel>(panel, "Modelo").Nombre = "Nuevo proveedor";
        await CallAsync(panel, "GuardarAsync");
        Assert.False(Get<bool>(compra, "AltaProveedor"));
        Assert.Equal(Assert.Single(await test.Db.Proveedores.ToListAsync()).Id, modelo.ProveedorId);
        Assert.Equal(modelo.ProveedorId, Get<ProveedorDto>(compra, "ProveedorSeleccionado").Id);
        Assert.Same(modelo, Get<CompraFormModel>(compra, "Modelo"));
        Assert.Same(archivo, Get<IBrowserFile>(compra, "ArchivoSeleccionado"));
        var despues = modelo.ToInput("COM-ESTABLE");
        Assert.Equal(antes with { ProveedorId = despues.ProveedorId, Detalles = despues.Detalles }, despues);
        Assert.Equal(antes.Detalles, despues.Detalles);
        Assert.Equal(50m, modelo.TotalVisual);
    }

    [Fact]
    public async Task Compra_RevisarEditarNoPersisten_ConfirmarLlamaServicioUnaVez()
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new RegistroContado(new CompraService(test.Db));
        var compra = await PrepararCompra(test, servicio);
        var modelo = Get<CompraFormModel>(compra, "Modelo");
        Call(compra, "RevisarCompra");
        Assert.True(Get<bool>(compra, "RevisandoCompra"));
        Assert.Equal(0, servicio.Llamadas);
        var resumen = BuscadoresContextualesTests.TextoRenderizado(compra);
        Assert.Contains("Q 87.50", resumen);
        Assert.Contains("Q 35.00", resumen);
        Assert.Contains("Q 17.50", resumen);
        Assert.Contains(test.Proveedor.Nombre, resumen);
        Assert.Contains(test.Producto.Nombre, resumen);
        Assert.Empty(await test.Db.Compras.ToListAsync());
        Call(compra, "EditarCompra");
        await CallAsync(compra, "ConfirmarCompraAsync");
        Assert.Equal(0, servicio.Llamadas);
        Assert.Same(modelo, Get<CompraFormModel>(compra, "Modelo"));
        Assert.Equal(87.50m, modelo.TotalVisual);
        Call(compra, "RevisarCompra");
        var primera = CallAsync(compra, "ConfirmarCompraAsync");
        Assert.True(Get<bool>(compra, "Guardando"));
        await CallAsync(compra, "ConfirmarCompraAsync");
        Assert.Equal(1, servicio.Llamadas);
        servicio.Continuar.SetResult();
        await primera;
        await CallAsync(compra, "ConfirmarCompraAsync");
        Assert.Equal(1, servicio.Llamadas);
        Assert.Null(Get<string?>(compra, "ErrorGuardado"));
        Assert.Equal(87.50m, (await new CompraService(test.Db).ListarAsync()).Single().Total);
        Assert.Equal(3, await test.Db.UnidadesInventario.CountAsync());
    }

    [Fact]
    public async Task Compra_RevisarConComprobanteNoAbreArchivo_EditarLoConserva()
    {
        await using var test = await TestDatabase.CreateAsync();
        var servicio = new RegistroContado(new CompraService(test.Db));
        var compra = await PrepararCompra(test, servicio);
        var archivo = new ArchivoSinLectura();
        Set(compra, "ArchivoSeleccionado", archivo);
        Get<CompraFormModel>(compra, "Modelo").AdjuntarComprobante = true;
        Call(compra, "RevisarCompra");
        Assert.True(Get<bool>(compra, "RevisandoCompra"));
        Assert.Contains("factura.pdf", BuscadoresContextualesTests.TextoRenderizado(compra));
        Call(compra, "EditarCompra");
        Assert.Same(archivo, Get<IBrowserFile>(compra, "ArchivoSeleccionado"));
        Assert.Equal(0, servicio.Llamadas);
        Assert.Empty(await test.Db.Compras.ToListAsync());
    }

    private static async Task<CompraNueva> PrepararCompra(TestDatabase test, IRegistroCompraConComprobanteService servicio)
    {
        var compra = new CompraNueva();
        Set(compra, "RegistroCompraService", servicio);
        Set(compra, "Logger", NullLogger<CompraNueva>.Instance);
        Set(compra, "Navigation", new Navegacion());
        Call(compra, "SeleccionarProveedor", (await new ProveedorService(test.Db).ObtenerPorIdAsync(test.Proveedor.Id)).Value!);
        var modelo = Get<CompraFormModel>(compra, "Modelo");
        modelo.FechaIngreso = new(2026, 9, 2);
        var producto = (await new ProductoService(test.Db).ObtenerPorIdAsync(test.Producto.Id)).Value!;
        Call(compra, "SeleccionarProducto", modelo.Detalles[0], producto);
        modelo.Detalles[0].Cantidad = 2;
        modelo.Detalles[0].CostoUnitario = 35m;
        modelo.Detalles.Add(new() { ProductoId = producto.Id, ProductoSeleccionado = producto, Cantidad = 1, CostoUnitario = 17.50m });
        return compra;
    }

    private sealed class RegistroContado(ICompraService compras) : IRegistroCompraConComprobanteService
    {
        public int Llamadas;
        public TaskCompletionSource Continuar { get; } = new();
        public async Task<ServiceResult<CompraDto>> RegistrarAsync(CompraInput compra, DatosComprobanteCompraInput? datos, ArchivoComprobanteInput? archivo, CancellationToken ct = default)
        {
            Llamadas++;
            await Continuar.Task;
            return await new RegistroCompraConComprobanteService(compras, null!, NullLogger<RegistroCompraConComprobanteService>.Instance)
                .RegistrarAsync(compra, datos, archivo, ct);
        }
    }

    private sealed class ArchivoSinLectura : IBrowserFile
    {
        public string Name => "factura.pdf";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => 10;
        public string ContentType => "application/pdf";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Revisar/editar no debe abrir el archivo.");
    }
}

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using ResellManager.Application.DTOs;
using ResellManager.Application.Services;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Compras;
using ResellManager.Web.Components.Productos;
using static ResellManager.Tests.ProductoLookupTests;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed partial class ScannerOperativoTests
{
    private const string CodigoNuevo = "99887766";

    [Theory]
    [InlineData("0012345678905", true)]
    [InlineData("012345678905", false)]
    [InlineData(" 0012345678905 ", false)]
    public async Task Compra_ScannerLocalExacto_SeleccionaDetalleSinCambiarCantidadCostoNiFormulario(string codigo, bool existe)
    {
        await using var test = await DatosAsync(0);
        await using var vista = await Vista.CrearAsync(test, compra: true);
        var modelo = await PrepararCompraAsync(vista, test);
        var antes = modelo.ToInput("COM-PRUEBA");
        var datos = modelo.ToDatosComprobante();
        var archivo = Get<IBrowserFile>(vista.Componente, "ArchivoSeleccionado");
        await vista.DetectarCompraAsync(1, codigo);
        Assert.Equal(codigo, vista.Consulta.UltimoCodigo);
        Assert.Equal(existe ? test.Producto.Id : 0, modelo.Detalles[1].ProductoId);
        var esperado = antes with { Detalles = antes.Detalles.Select((d, i) => i == 1 && existe ? d with { ProductoId = test.Producto.Id } : d).ToArray() };
        CompararCompra(esperado, modelo.ToInput("COM-PRUEBA"));
        Assert.Equal(datos, modelo.ToDatosComprobante());
        Assert.Same(archivo, Get<IBrowserFile>(vista.Componente, "ArchivoSeleccionado"));
        Assert.Equal(0, vista.Externo.Llamadas);
        Assert.Empty(await test.Db.UnidadesInventario.ToListAsync());
        Assert.Empty(await test.Db.Compras.ToListAsync());
        if (!existe)
        {
            Assert.Equal(codigo, Get<string>(vista.Componente, "CodigoNoRegistrado"));
            Assert.Contains("Registrar producto", await vista.HtmlAsync());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Compra_AltaAsistidaExplicita_RevisaDatosYAutoseleccionaSinPerderCompra(bool imagenExterna)
    {
        await using var test = await DatosAsync(0);
        await using var vista = await Vista.CrearAsync(test, compra: true);
        var modelo = await PrepararCompraAsync(vista, test);
        var antes = modelo.ToInput("COM-PRUEBA");
        var archivo = Get<IBrowserFile>(vista.Componente, "ArchivoSeleccionado");
        var primera = new ProveedorFalso("Open Facts", new ProductoLookupRespuesta(EstadoLookupProveedor.NoEncontrado));
        var segunda = new ProveedorFalso("UPCitemdb", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado,
            new ProductoLookupCandidato { CodigoBarras = CodigoNuevo, Nombre = "Producto nuevo de prueba", Marca = "Marca",
                ImagenUrl = imagenExterna ? "https://images.example.com/producto.png" : null }));
        vista.Externo.Servicio = new ProductoLookupService(vista.Consulta, [primera, segunda]);
        await vista.DetectarCompraAsync(1, CodigoNuevo);
        Assert.Equal(0, vista.Externo.Llamadas);
        Assert.Equal(0, primera.Llamadas + segunda.Llamadas);
        CompararCompra(antes, modelo.ToInput("COM-PRUEBA"));
        await vista.EjecutarAsync("RegistrarCodigoNoEncontrado", modelo.Detalles[1]);
        var alta = Get<ProductoFormModel>(vista.Panel, "Modelo");
        Assert.Equal(CodigoNuevo, alta.CodigoBarras);
        Assert.True(vista.AltaForm.LookupHabilitado);
        Assert.Equal(1, primera.Llamadas);
        Assert.Equal(1, segunda.Llamadas);
        Assert.Equal(2, vista.Consulta.Llamadas); // Scanner + comprobación local obligatoria del alta.
        Assert.Equal(string.Empty, alta.Nombre);
        Assert.NotNull(Leer<ProductoLookupRonda?>(vista.AltaForm, "Ronda")?.Candidato);
        Assert.Equal(1, await test.Db.Productos.CountAsync());
        alta.PrecioSugerido = 87m;
        await vista.AltaAsync("UsarCandidato");
        Assert.Equal("Producto nuevo de prueba", alta.Nombre);
        Assert.Equal(87m, alta.PrecioSugerido);
        alta.CategoriaId = test.Categoria.Id;
        Assert.Equal(0, modelo.Detalles[1].ProductoId);
        Assert.Empty(vista.Imagenes.Solicitudes);
        await vista.AltaAsync("GuardarAsync");
        var producto = await test.Db.Productos.SingleAsync(x => x.CodigoBarras == CodigoNuevo);
        Assert.Equal(producto.Id, modelo.Detalles[1].ProductoId);
        Assert.Equal(producto.Id, modelo.Detalles[1].ProductoSeleccionado!.Id);
        Assert.Null(Get<DetalleCompraFormModel?>(vista.Componente, "DetalleAlta"));
        CompararCompra(antes with { Detalles = antes.Detalles.Select((d, i) => i == 1 ? d with { ProductoId = producto.Id } : d).ToArray() }, modelo.ToInput("COM-PRUEBA"));
        Assert.Same(archivo, Get<IBrowserFile>(vista.Componente, "ArchivoSeleccionado"));
        Assert.Empty(await test.Db.Compras.ToListAsync());
        Assert.Empty(await test.Db.UnidadesInventario.ToListAsync());
        Assert.Equal(imagenExterna ? 1 : 0, vista.Imagenes.Solicitudes.Count);
        if (imagenExterna) Assert.Contains("sin ella", Get<string>(vista.Componente, "AvisoImagenProducto"));
    }

    [Fact]
    public async Task Compra_CancelarAlta_DescartaCandidatoYConservaCompraComprobanteYDetalleOriginal()
    {
        await using var test = await DatosAsync(0);
        await using var vista = await Vista.CrearAsync(test, compra: true);
        var modelo = await PrepararCompraAsync(vista, test);
        var antes = modelo.ToInput("COM-PRUEBA");
        var datos = modelo.ToDatosComprobante();
        var archivo = Get<IBrowserFile>(vista.Componente, "ArchivoSeleccionado");
        var proveedor = new ProveedorFalso("Open Facts", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado,
            new ProductoLookupCandidato { CodigoBarras = CodigoNuevo, Nombre = "No guardar" }));
        vista.Externo.Servicio = new ProductoLookupService(vista.Consulta, [proveedor]);
        await vista.DetectarCompraAsync(1, CodigoNuevo);
        await vista.EjecutarAsync("RegistrarCodigoNoEncontrado", modelo.Detalles[1]);
        await vista.AltaAsync("DescartarCandidato");
        await vista.PanelAsync("CancelarAsync");
        Assert.Null(Get<DetalleCompraFormModel?>(vista.Componente, "DetalleAlta"));
        CompararCompra(antes, modelo.ToInput("COM-PRUEBA"));
        Assert.Equal(datos, modelo.ToDatosComprobante());
        Assert.Same(archivo, Get<IBrowserFile>(vista.Componente, "ArchivoSeleccionado"));
        Assert.Equal(1, await test.Db.Productos.CountAsync());
        Assert.Empty(await test.Db.UnidadesInventario.ToListAsync());
        Assert.False(Get<bool>(vista.Componente, "SeleccionBloqueada"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Compra_CancelarScannerOFalloLocal_ConservaCompraYPermiteBusquedaManual(bool fallo)
    {
        await using var test = await DatosAsync(0);
        await using var vista = await Vista.CrearAsync(test, compra: true);
        var modelo = await PrepararCompraAsync(vista, test);
        var antes = modelo.ToInput("COM-PRUEBA");
        if (fallo)
        {
            vista.Consulta.Respuesta = _ => throw new InvalidOperationException("Fallo de prueba");
            await vista.DetectarCompraAsync(1, Codigo);
            Assert.NotNull(Get<string?>(vista.Componente, "ErrorCodigo"));
            Assert.Null(Get<string?>(vista.Componente, "CodigoNoRegistrado"));
        }
        else
        {
            await vista.ScannerDetalleAsync(1, "AbrirAsync");
            await vista.ScannerDetalleAsync(1, "CancelarAsync");
            await vista.ScannerDetalleAsync(1, "FinalizarEscaneo", "detected", Codigo);
            Assert.Equal(0, vista.Consulta.Llamadas);
        }
        CompararCompra(antes, modelo.ToInput("COM-PRUEBA"));
        Assert.Equal(0, vista.Externo.Llamadas);
        await vista.EjecutarAsync("SeleccionarProducto", modelo.Detalles[1], (await new ProductoService(test.Db).ObtenerPorIdAsync(test.Producto.Id)).Value!);
        Assert.Equal(test.Producto.Id, modelo.Detalles[1].ProductoId);
    }

    [Fact]
    public async Task Compra_DobleConsultaYAccionesIncompatibles_NoPierdenElDetalleObjetivo()
    {
        await using var test = await DatosAsync(0);
        await using var vista = await Vista.CrearAsync(test, compra: true);
        var modelo = await PrepararCompraAsync(vista, test);
        var antes = modelo.ToInput("COM-PRUEBA");
        var respuesta = new TaskCompletionSource<ProductoDto?>();
        vista.Consulta.Respuesta = _ => respuesta.Task;
        var primera = vista.EjecutarAsync("CodigoEscaneadoAsync", modelo.Detalles[1], Codigo);
        await vista.EjecutarAsync("CodigoEscaneadoAsync", modelo.Detalles[0], Codigo);
        await vista.EjecutarAsync("QuitarFila", modelo.Detalles[1]);
        await vista.EjecutarAsync("AgregarFila");
        await vista.EjecutarAsync("AbrirAlta", modelo.Detalles[1]);
        await vista.EjecutarAsync("CambiarMonedaAsync", new ChangeEventArgs { Value = "GTQ" });
        await vista.EjecutarAsync("RevisarCompra");
        Assert.False(Get<bool>(vista.Componente, "RevisandoCompra"));
        Assert.Equal(1, vista.Consulta.Llamadas);
        CompararCompra(antes, modelo.ToInput("COM-PRUEBA"));
        respuesta.SetResult(await new ProductoService(test.Db).ObtenerPorCodigoBarrasAsync(Codigo));
        await primera;
        Assert.Equal(test.Producto.Id, modelo.Detalles[1].ProductoId);
    }

    [Fact]
    public async Task Compra_RevisarYConfirmar_ElServicioGeneraCantidadDeUnidadesConCostoActual()
    {
        await using var test = await DatosAsync(0);
        await using var vista = await Vista.CrearAsync(test, compra: true);
        await vista.EjecutarAsync("SeleccionarProveedor", (await new ProveedorService(test.Db).ObtenerPorIdAsync(test.Proveedor.Id)).Value!);
        var modelo = Get<CompraFormModel>(vista.Componente, "Modelo");
        await vista.DetectarCompraAsync(0, Codigo);
        Assert.Equal(1, modelo.Detalles[0].Cantidad);
        modelo.Detalles[0].Cantidad = 4;
        modelo.Detalles[0].CostoUnitario = 12.50m;
        await vista.EjecutarAsync("RevisarCompra");
        Assert.Contains("4 × Q 12.50 = Q 50.00", await vista.HtmlAsync());
        Assert.Empty(await test.Db.Compras.ToListAsync());
        Assert.Empty(await test.Db.UnidadesInventario.ToListAsync());
        await vista.EjecutarAsync("ConfirmarCompraAsync");
        Assert.Equal(50m, Assert.Single(await test.Db.Compras.ToListAsync()).Total);
        Assert.Equal(4, await test.Db.UnidadesInventario.CountAsync());
        Assert.All(await test.Db.UnidadesInventario.ToListAsync(), x => Assert.Equal(12.50m, x.Costo));
    }

    [Fact]
    public async Task Compra_ProductoRegistradoAntesDeAbrirAlta_ReconsultaLocalSinProveedorNiDuplicado()
    {
        await using var test = await DatosAsync(0);
        await using var vista = await Vista.CrearAsync(test, compra: true);
        var modelo = await PrepararCompraAsync(vista, test);
        await vista.DetectarCompraAsync(1, CodigoNuevo);
        var nuevo = await new ProductoService(test.Db).CrearAsync(new(CodigoNuevo, "Creado concurrentemente", null, null, null, null, null, 0m, test.Categoria.Id));
        var proveedor = new ProveedorFalso("Open Facts", new ProductoLookupRespuesta(EstadoLookupProveedor.NoEncontrado));
        vista.Externo.Servicio = new ProductoLookupService(vista.Consulta, [proveedor]);
        await vista.EjecutarAsync("RegistrarCodigoNoEncontrado", modelo.Detalles[1]);
        Assert.Equal(0, proveedor.Llamadas);
        Assert.Equal(nuevo.Value!.Id, Leer<ProductoLookupRonda>(vista.AltaForm, "Ronda").ProductoLocal!.Id);
        Assert.Contains("target=" + (char)34 + "_blank", await vista.HtmlAsync());
        await vista.AltaAsync("GuardarAsync");
        Assert.Equal(2, await test.Db.Productos.CountAsync());
        Assert.Equal(0, modelo.Detalles[1].ProductoId);
        await vista.PanelAsync("CancelarAsync");
        await vista.DetectarCompraAsync(1, CodigoNuevo);
        Assert.Equal(nuevo.Value.Id, modelo.Detalles[1].ProductoId);
    }

    [Fact]
    public async Task Compra_CancelarAltaConLookupPendiente_CancelaYDescartaResultadoTardio()
    {
        await using var test = await DatosAsync(0);
        await using var vista = await Vista.CrearAsync(test, compra: true);
        var modelo = await PrepararCompraAsync(vista, test);
        var antes = modelo.ToInput("COM-PRUEBA");
        var respuesta = new TaskCompletionSource<ProductoLookupRespuesta>();
        CancellationToken token = default;
        var proveedor = new ProveedorFalso("Open Facts", ct => { token = ct; return respuesta.Task; });
        vista.Externo.Servicio = new ProductoLookupService(vista.Consulta, [proveedor]);
        await vista.DetectarCompraAsync(1, CodigoNuevo);
        await vista.EjecutarAsync("RegistrarCodigoNoEncontrado", modelo.Detalles[1]);
        Assert.Equal(1, proveedor.Llamadas);
        var formulario = vista.AltaForm;
        await vista.PanelAsync("CancelarAsync");
        Assert.True(token.IsCancellationRequested);
        respuesta.SetResult(new(EstadoLookupProveedor.Encontrado, new ProductoLookupCandidato { CodigoBarras = CodigoNuevo, Nombre = "Respuesta tardía" }));
        await vista.EjecutarAsync("StateHasChanged");
        CompararCompra(antes, modelo.ToInput("COM-PRUEBA"));
        Assert.Null(Get<DetalleCompraFormModel?>(vista.Componente, "DetalleAlta"));
        Assert.Equal(string.Empty, formulario.Modelo.Nombre);
        Assert.Equal(1, await test.Db.Productos.CountAsync());
    }

    [Fact]
    public async Task MarkupOperativoCompras_SeleccionLocalAltaAsistidaYCancelacion()
    {
        await using var test = await DatosAsync(0);
        await using var vista = await Vista.CrearAsync(test, compra: true);
        var modelo = await PrepararCompraAsync(vista, test);
        await vista.EjecutarAsync("StateHasChanged");
        await vista.GuardarHtmlAsync("compra-inicial");
        await vista.DetectarCompraAsync(1, Codigo);
        await vista.GuardarHtmlAsync("compra-encontrado");
        await vista.EjecutarAsync("RevisarCompra");
        await vista.GuardarHtmlAsync("compra-revision");
        await vista.EjecutarAsync("EditarCompra");
        await vista.DetectarCompraAsync(1, CodigoNuevo);
        await vista.GuardarHtmlAsync("compra-no-encontrado");
        vista.Externo.Servicio = new ProductoLookupService(vista.Consulta, [new ProveedorFalso("Open Facts",
            new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, new ProductoLookupCandidato
                { CodigoBarras = CodigoNuevo, Nombre = "Producto externo de prueba para revisar y aceptar" }))]);
        await vista.EjecutarAsync("RegistrarCodigoNoEncontrado", modelo.Detalles[1]);
        await vista.GuardarHtmlAsync("compra-alta-candidato");
        await vista.AltaAsync("UsarCandidato");
        await vista.GuardarHtmlAsync("compra-alta-aceptada");
        await vista.PanelAsync("CancelarAsync");
        await vista.GuardarHtmlAsync("compra-alta-cancelada");
        await vista.ScannerDetalleAsync(1, "AbrirAsync");
        await vista.ScannerDetalleAsync(1, "CancelarAsync");
        await vista.GuardarHtmlAsync("compra-scanner-cancelado");
    }

    private static async Task<CompraFormModel> PrepararCompraAsync(Vista vista, TestDatabase test)
    {
        await vista.EjecutarAsync("SeleccionarProveedor", (await new ProveedorService(test.Db).ObtenerPorIdAsync(test.Proveedor.Id)).Value!);
        var modelo = Get<CompraFormModel>(vista.Componente, "Modelo");
        modelo.FechaCompra = new(2026, 10, 6);
        modelo.Origen = OrigenCompra.Importacion;
        modelo.FechaIngreso = null;
        modelo.CambiarMoneda(MonedaCompra.USD);
        modelo.TipoCambio = 7.7m; modelo.MarcarTipoCambioManual();
        modelo.Observaciones = "Observación sintética";
        modelo.AdjuntarComprobante = true; modelo.NumeroDocumento = "FACTURA-PRUEBA";
        modelo.ObservacionesComprobante = "Documento sintético";
        var primero = modelo.Detalles[0];
        primero.ProductoId = test.Producto.Id;
        primero.ProductoSeleccionado = (await new ProductoService(test.Db).ObtenerPorIdAsync(test.Producto.Id)).Value;
        primero.Cantidad = 3; primero.CostoUnitario = 4.50m;
        modelo.Detalles.Add(new DetalleCompraFormModel { Cantidad = 5, CostoUnitario = 8.25m });
        Set(vista.Componente, "ArchivoSeleccionado", new ComprobanteSinLectura());
        await vista.EjecutarAsync("StateHasChanged");
        return modelo;
    }
    private static void CompararCompra(CompraInput esperado, CompraInput actual)
    {
        Assert.Equal(esperado with { Detalles = actual.Detalles }, actual);
        Assert.Equal(esperado.Detalles, actual.Detalles);
    }
    private sealed class ComprobanteSinLectura : IBrowserFile
    {
        public string Name => "factura-sintetica.pdf";
        public DateTimeOffset LastModified => DateTimeOffset.UnixEpoch;
        public long Size => 10;
        public string ContentType => "application/pdf";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Seleccionar o revisar no debe leer el comprobante");
    }
}

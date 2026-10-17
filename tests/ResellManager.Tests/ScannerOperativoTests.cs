using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.Common;
using ResellManager.Web.Components.Compras;
using ResellManager.Web.Components.Productos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Pages;
using ResellManager.Web.Components.Shared;
using ResellManager.Web.Components.Ventas;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed partial class ScannerOperativoTests
{
    private const string Codigo = "0012345678905";

    [Fact]
    public async Task ConsultaPorProducto_ReutilizaElegibilidadExclusionYOrdenSinLimiteDoce()
    {
        await using var test = await DatosAsync(15);
        var libres = await test.Db.UnidadesInventario.OrderBy(x => x.CodigoInterno).ToArrayAsync();
        for (var i = 0; i < libres.Length; i++) libres[i].Costo = 500 - i;
        foreach (var estado in Enum.GetValues<EstadoUnidadInventario>().Where(x => x != EstadoUnidadInventario.Disponible))
        {
            var unidad = await test.CrearUnidadDisponibleAsync("COM-" + estado);
            unidad.Estado = estado;
        }
        var reservada = await test.CrearUnidadDisponibleAsync("COM-RESERVADA");
        var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-RESERVA");
        Assert.True((await new InventarioService(test.Db).ReservarAsync(reservada.Id, pedido.Detalles.Single().Id)).IsSuccess);
        await test.CrearUnidadDisponibleAsync("COM-OTRO", await test.CrearProductoAsync("PRO-OTRO"));
        await test.Db.SaveChangesAsync();
        var servicio = new SeleccionOperativaService(test.Db);
        var encontradas = await servicio.ListarUnidadesDirectasAsync(test.Producto.Id, [libres[0].Id, libres[0].Id]);
        Assert.Equal(libres.Skip(1).Select(x => x.Id), encontradas.Select(x => x.Id));
        Assert.Equal(14, encontradas.Count);
        Assert.Equal(encontradas.Count, encontradas.Select(x => x.Id).Distinct().Count());
        Assert.Empty(await servicio.ListarUnidadesDirectasAsync(int.MaxValue));
        Assert.Empty(await servicio.ListarUnidadesDirectasAsync(test.Producto.Id, libres.Select(x => x.Id).ToArray()));
        Assert.All(encontradas, x => { Assert.Equal(EstadoUnidadInventario.Disponible, x.Estado); Assert.Null(x.DetallePedidoReservaId); });
        Assert.NotNull(reservada.DetallePedidoReservaId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    public async Task VentaDirecta_CeroUnaOMultiples_AgregaExactamenteLasUnidadesConPrecioSugerido(int cantidad)
    {
        await using var test = await DatosAsync(cantidad);
        await using var vista = await Vista.CrearAsync(test);
        await vista.DetectarAsync(Codigo);
        Assert.Equal("1", Get<string>(vista.Componente, "CantidadEscaneada"));
        Assert.Equal(cantidad, Get<IReadOnlyList<UnidadInventarioDto>>(vista.Componente, "UnidadesEscaneadas").Count);
        Assert.Contains(cantidad + " unidades disponibles para agregar", await vista.HtmlAsync());
        Assert.Empty(vista.Articulos);
        if (cantidad > 0) Set(vista.Componente, "CantidadEscaneada", cantidad.ToString());
        else Assert.DoesNotContain("directa-cantidad-scanner", await vista.HtmlAsync());
        await vista.EjecutarAsync("AgregarEscaneadasAsync");
        Assert.Equal(cantidad, vista.Articulos.Count);
        Assert.Equal(cantidad, vista.Articulos.Select(x => x.Unidad.Id).Distinct().Count());
        Assert.All(vista.Articulos, x => { Assert.True(x.Seleccionada); Assert.Equal(test.Producto.PrecioSugerido, x.PrecioFinal); });
        await vista.DetectarAsync(Codigo);
        Assert.Empty(Get<IReadOnlyList<UnidadInventarioDto>>(vista.Componente, "UnidadesEscaneadas"));
        Assert.Equal(0, vista.Externo.Llamadas);
        Assert.Empty(await test.Db.Pedidos.ToListAsync());
        Assert.Empty(await test.Db.Ventas.ToListAsync());
        Assert.All(await test.Db.UnidadesInventario.ToListAsync(), x => { Assert.Equal(EstadoUnidadInventario.Disponible, x.Estado); Assert.Null(x.DetallePedidoReservaId); });
    }

    [Fact]
    public async Task VentaDirecta_DescuentaManualesYEscaneadas_RepiteQuitaRevisaYRegistraUnidadesReales()
    {
        await using var test = await DatosAsync(7);
        await using var vista = await Vista.CrearAsync(test);
        var opciones = await new SeleccionOperativaService(test.Db).ListarUnidadesDirectasAsync(test.Producto.Id);
        await vista.EjecutarAsync("AgregarUnidadAsync", opciones[0]);
        vista.Articulos[0].PrecioFinal = 155m;
        await vista.DetectarAsync(Codigo);
        Assert.Equal(6, Get<IReadOnlyList<UnidadInventarioDto>>(vista.Componente, "UnidadesEscaneadas").Count);
        Set(vista.Componente, "CantidadEscaneada", "2");
        await vista.EjecutarAsync("AgregarEscaneadasAsync");
        Assert.Equal(opciones.Take(3).Select(x => x.Id), vista.Articulos.Select(x => x.Unidad.Id));
        Assert.Equal(155m, vista.Articulos[0].PrecioFinal);
        await vista.DetectarAsync(Codigo);
        Assert.Equal(4, Get<IReadOnlyList<UnidadInventarioDto>>(vista.Componente, "UnidadesEscaneadas").Count);
        Set(vista.Componente, "CantidadEscaneada", "4");
        await vista.EjecutarAsync("AgregarEscaneadasAsync");
        var retirada = vista.Articulos[^1];
        await vista.EjecutarAsync("QuitarUnidad", retirada);
        await vista.DetectarAsync(Codigo);
        Assert.Equal(retirada.Unidad.Id, Assert.Single(Get<IReadOnlyList<UnidadInventarioDto>>(vista.Componente, "UnidadesEscaneadas")).Id);
        await vista.EjecutarAsync("CancelarSeleccionCodigoAsync");
        Get<IReadOnlyList<LoteVentaDirectaFormModel>>(vista.Componente, "Lotes")[1].PrecioFinal = 90m;
        var ids = vista.Articulos.Select(x => x.Unidad.Id).ToArray();
        await vista.EjecutarAsync("RevisarVentaDirectaAsync");
        var html = await vista.HtmlAsync();
        foreach (var articulo in vista.Articulos) Assert.Contains(articulo.Unidad.CodigoInterno, html);
        Assert.Contains(VentaPresentacion.Moneda(695m), html);
        Assert.Empty(await test.Db.Pedidos.ToListAsync());
        await vista.EjecutarAsync("ConfirmarVentaAsync");
        await vista.EjecutarAsync("ConfirmarVentaAsync");
        var venta = Assert.Single(await test.Db.Ventas.Include(x => x.Detalles).ToListAsync());
        Assert.Equal(ids.Order(), venta.Detalles.Select(x => x.UnidadInventarioId!.Value).Order());
        Assert.Equal(695m, venta.Detalles.Sum(x => x.PrecioFinal));
        Assert.Equal(EstadoUnidadInventario.Disponible, (await test.Db.UnidadesInventario.SingleAsync(x => x.Id == retirada.Unidad.Id)).Estado);
        Assert.EndsWith("/ventas/" + venta.Id, vista.Navigation.Uri);
        Assert.Equal(0, vista.Externo.Llamadas);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("1,5")]
    [InlineData("4")]
    [InlineData("1e0")]
    [InlineData("")]
    [InlineData("2147483648")]
    public async Task VentaDirecta_CantidadInvalida_NoAgregaParcialmente(string cantidad)
    {
        await using var test = await DatosAsync(3);
        await using var vista = await Vista.CrearAsync(test);
        await vista.DetectarAsync(Codigo);
        Set(vista.Componente, "CantidadEscaneada", cantidad);
        await vista.EjecutarAsync("AgregarEscaneadasAsync");
        Assert.Empty(vista.Articulos);
        Assert.Contains("entera entre 1 y 3", Get<string>(vista.Componente, "ErrorCodigo"));
        Assert.True(Get<bool>(vista.Componente, "CantidadPendiente"));
        Assert.Matches("<button[^>]*disabled[^>]*>Agregar unidades</button>", await vista.HtmlAsync());
    }

    [Theory]
    [InlineData(false, "0012345678905", true)]
    [InlineData(true, "0012345678905", true)]
    [InlineData(false, "012345678905", false)]
    [InlineData(true, "012345678905", false)]
    [InlineData(false, " 0012345678905 ", false)]
    [InlineData(true, " 0012345678905 ", false)]
    [InlineData(false, "INEXISTENTE", false)]
    [InlineData(true, "INEXISTENTE", false)]
    public async Task CodigoExacto_LocalSinLookupExterno_NavegaInventarioInclusoSinUnidades(bool inventario, string codigo, bool existe)
    {
        await using var test = await DatosAsync(0);
        await using var vista = await Vista.CrearAsync(test, inventario);
        var uri = vista.Navigation.Uri;
        await vista.DetectarAsync(codigo);
        Assert.Equal(codigo, vista.Consulta.UltimoCodigo);
        if (existe && inventario) Assert.EndsWith("/productos/" + test.Producto.Id, vista.Navigation.Uri);
        else Assert.Equal(uri, vista.Navigation.Uri);
        if (!existe) Assert.Contains("No hay ningún producto registrado", Get<string>(vista.Componente, "ErrorCodigo"));
        if (existe && !inventario) Assert.Equal(test.Producto.Id, Get<ProductoDto>(vista.Componente, "ProductoEscaneado").Id);
        Assert.Equal(0, vista.Externo.Llamadas);
        Assert.Empty(await test.Db.Pedidos.ToListAsync());
        Assert.Empty(await test.Db.UnidadesInventario.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelarScanner_DescartaResultadoTardioYConservaVentaOFiltros(bool inventario)
    {
        await using var test = await DatosAsync(2);
        await using var vista = await Vista.CrearAsync(test, inventario);
        if (inventario)
        {
            Set(vista.Componente, "Termino", "Producto");
            Set(vista.Componente, "EstadoFiltro", EstadoUnidadInventario.Disponible);
            await vista.EjecutarAsync("BuscarAsync");
        }
        else
        {
            await vista.EjecutarAsync("AgregarUnidadAsync", (await new SeleccionOperativaService(test.Db).ListarUnidadesDirectasAsync(test.Producto.Id))[0]);
            vista.Articulos[0].PrecioFinal = 137m;
        }
        var antes = inventario ? Get<IReadOnlyList<UnidadInventarioDto>>(vista.Componente, "Unidades") : null;
        await vista.ScannerAsync("AbrirAsync");
        await vista.ScannerAsync("CancelarAsync");
        await vista.ScannerAsync("FinalizarEscaneo", "detected", Codigo);
        Assert.Equal(0, vista.Consulta.Llamadas);
        Assert.Equal(0, vista.Externo.Llamadas);
        if (inventario)
        {
            Assert.Equal("Producto", Get<string>(vista.Componente, "Termino"));
            Assert.Equal(EstadoUnidadInventario.Disponible, Get<EstadoUnidadInventario?>(vista.Componente, "EstadoFiltro"));
            Assert.Same(antes, Get<IReadOnlyList<UnidadInventarioDto>>(vista.Componente, "Unidades"));
        }
        else Assert.Equal(137m, Assert.Single(vista.Articulos).PrecioFinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CodigoDesconocidoOFalloTecnico_PreservaFiltrosListadoYArticulos(bool fallo)
    {
        await using var test = await DatosAsync(2);
        await using var vista = await Vista.CrearAsync(test, inventario: true);
        Set(vista.Componente, "Termino", "Producto");
        Set(vista.Componente, "EstadoFiltro", EstadoUnidadInventario.Disponible);
        await vista.EjecutarAsync("BuscarAsync");
        var unidades = Get<IReadOnlyList<UnidadInventarioDto>>(vista.Componente, "Unidades");
        var uri = vista.Navigation.Uri;
        if (fallo) vista.Consulta.Respuesta = _ => throw new InvalidOperationException("Fallo local de prueba");
        await vista.DetectarAsync("NO-EXISTE");
        Assert.Equal("Producto", Get<string>(vista.Componente, "Termino"));
        Assert.Equal(EstadoUnidadInventario.Disponible, Get<EstadoUnidadInventario?>(vista.Componente, "EstadoFiltro"));
        Assert.Same(unidades, Get<IReadOnlyList<UnidadInventarioDto>>(vista.Componente, "Unidades"));
        Assert.Equal(uri, vista.Navigation.Uri);
        Assert.False(Get<bool>(vista.Componente, "BuscandoCodigo"));
        Assert.NotNull(Get<string?>(vista.Componente, "ErrorCodigo"));
        var html = await vista.HtmlAsync();
        Assert.Contains("role=" + (char)34 + "alert" + (char)34, html);
        Assert.Contains("Unidades de inventario", html);
        Assert.Equal(0, vista.Externo.Llamadas);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConsultaEnCurso_IgnoraDobleToqueYBloqueaAccionesIncompatibles(bool inventario)
    {
        await using var test = await DatosAsync(2);
        await using var vista = await Vista.CrearAsync(test, inventario);
        var respuesta = new TaskCompletionSource<ProductoDto?>();
        vista.Consulta.Respuesta = _ => respuesta.Task;
        var primera = vista.EjecutarAsync("CodigoEscaneadoAsync", Codigo);
        await vista.EjecutarAsync("CodigoEscaneadoAsync", Codigo);
        Assert.Equal(1, vista.Consulta.Llamadas);
        Assert.True(Get<bool>(vista.Componente, "BuscandoCodigo"));
        if (inventario)
        {
            Set(vista.Componente, "Termino", "conservar");
            await vista.EjecutarAsync("LimpiarFiltrosAsync");
            Assert.Equal("conservar", Get<string>(vista.Componente, "Termino"));
        }
        else
        {
            await vista.EjecutarAsync("AgregarUnidadAsync", (await new SeleccionOperativaService(test.Db).ListarUnidadesDirectasAsync(test.Producto.Id))[0]);
            await vista.EjecutarAsync("RevisarVentaDirectaAsync");
            Assert.Empty(vista.Articulos);
            Assert.False(Get<bool>(vista.Componente, "RevisandoVenta"));
        }
        respuesta.SetResult(await new ProductoService(test.Db).ObtenerPorCodigoBarrasAsync(Codigo));
        await primera;
        Assert.False(Get<bool>(vista.Componente, "BuscandoCodigo"));
    }

    [Fact]
    public async Task AgregarEnCurso_IgnoraDobleConfirmacionYCambioDeSeleccion()
    {
        await using var test = await DatosAsync(3);
        await using var vista = await Vista.CrearAsync(test);
        await vista.DetectarAsync(Codigo);
        Set(vista.Componente, "CantidadEscaneada", "2");
        var respuesta = new TaskCompletionSource<bool>();
        vista.Seleccion.Respuesta = respuesta.Task;
        var primera = vista.EjecutarAsync("AgregarEscaneadasAsync");
        await vista.EjecutarAsync("AgregarEscaneadasAsync");
        await vista.EjecutarAsync("CancelarSeleccionCodigoAsync");
        await vista.EjecutarAsync("CodigoEscaneadoAsync", Codigo);
        Assert.True(Get<bool>(vista.Componente, "CantidadPendiente"));
        Assert.Empty(vista.Articulos);
        Assert.Equal(1, vista.Seleccion.Comprobaciones);
        Assert.Equal(1, vista.Consulta.Llamadas);
        respuesta.SetResult(true);
        await primera;
        Assert.Equal(2, vista.Articulos.Count);
        Assert.Equal(2, vista.Articulos.Select(x => x.Unidad.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CambioConcurrente_AntesDeAgregarORegistrar_NoSustituyeNiReduceNiPersiste(bool antesDeRegistrar, bool reserva)
    {
        await using var test = await DatosAsync(3);
        await using var vista = await Vista.CrearAsync(test);
        await vista.DetectarAsync(Codigo);
        Set(vista.Componente, "CantidadEscaneada", "2");
        var esperadas = Get<IReadOnlyList<UnidadInventarioDto>>(vista.Componente, "UnidadesEscaneadas").Take(2).Select(x => x.Id).ToArray();
        if (antesDeRegistrar)
        {
            await vista.EjecutarAsync("AgregarEscaneadasAsync");
            await vista.EjecutarAsync("RevisarVentaDirectaAsync");
        }
        var unidad = await test.Db.UnidadesInventario.SingleAsync(x => x.Id == esperadas[0]);
        if (reserva)
        {
            var pedido = await test.CrearPedidoAsync(TipoPedido.Apartado, "PED-CONCURRENTE");
            Assert.True((await new InventarioService(test.Db).ReservarAsync(unidad.Id, pedido.Detalles.Single().Id)).IsSuccess);
        }
        else { unidad.Estado = EstadoUnidadInventario.Vendida; await test.Db.SaveChangesAsync(); }
        var pedidosAntes = await test.Db.Pedidos.CountAsync();
        await vista.EjecutarAsync(antesDeRegistrar ? "ConfirmarVentaAsync" : "AgregarEscaneadasAsync");
        Assert.Empty(await test.Db.Ventas.ToListAsync());
        Assert.Equal(pedidosAntes, await test.Db.Pedidos.CountAsync());
        if (antesDeRegistrar) Assert.Equal(esperadas, vista.Articulos.Select(x => x.Unidad.Id));
        else Assert.Empty(vista.Articulos);
        Assert.Contains("ya no están disponibles", Get<string>(vista.Componente, antesDeRegistrar ? "ErrorGuardado" : "ErrorCodigo"));
        Assert.Equal(EstadoUnidadInventario.Disponible, (await test.Db.UnidadesInventario.SingleAsync(x => x.Id == esperadas[1])).Estado);
    }

    [Fact]
    public async Task VentaDirecta_FalloLocalYCancelacionDeCantidad_ConservanArticulosYBusquedaManual()
    {
        await using var test = await DatosAsync(3);
        await using var vista = await Vista.CrearAsync(test);
        var opciones = await new SeleccionOperativaService(test.Db).ListarUnidadesDirectasAsync(test.Producto.Id);
        await vista.EjecutarAsync("AgregarUnidadAsync", opciones[0]);
        vista.Articulos[0].PrecioFinal = 135m;
        await vista.DetectarAsync("NO-EXISTE");
        Assert.NotNull(Get<string?>(vista.Componente, "ErrorCodigo"));
        await vista.EjecutarAsync("AgregarUnidadAsync", opciones[1]);
        Assert.Equal(2, vista.Articulos.Count);
        await vista.DetectarAsync(Codigo);
        await vista.EjecutarAsync("CancelarSeleccionCodigoAsync");
        Assert.Null(Get<ProductoDto?>(vista.Componente, "ProductoEscaneado"));
        Assert.Equal(135m, vista.Articulos[0].PrecioFinal);
        Assert.Equal(2, vista.Articulos.Count);
        vista.Consulta.Respuesta = _ => throw new InvalidOperationException("Fallo local de prueba");
        await vista.DetectarAsync(Codigo);
        Assert.False(Get<bool>(vista.Componente, "SeleccionBloqueada"));
        await vista.EjecutarAsync("AgregarUnidadAsync", opciones[2]);
        Assert.Equal(3, vista.Articulos.Count);
        Assert.Equal(0, vista.Externo.Llamadas);
    }

    [Fact]
    public async Task MarkupOperativo_EstadosDeCantidadErroresRevisionYFiltros()
    {
        await using var test = await DatosAsync(3);
        test.Producto.Nombre = "Producto de prueba con un nombre extenso para comprobar el ajuste en pantallas pequeñas";
        await test.Db.SaveChangesAsync();
        await using var venta = await Vista.CrearAsync(test);
        await venta.GuardarHtmlAsync("venta-inicial");
        await venta.DetectarAsync(Codigo);
        var html = await venta.HtmlAsync();
        Assert.Contains("inputmode=" + (char)34 + "numeric", html);
        Assert.Contains("max=" + (char)34 + "3", html);
        Assert.Contains("min=" + (char)34 + "1", html);
        Assert.True(Get<bool>(venta.Componente, "SeleccionBloqueada"));
        await venta.GuardarHtmlAsync("venta-cantidad");
        Set(venta.Componente, "CantidadEscaneada", "0");
        await venta.EjecutarAsync("AgregarEscaneadasAsync");
        await venta.GuardarHtmlAsync("venta-cantidad-error");
        Set(venta.Componente, "CantidadEscaneada", "3");
        await venta.EjecutarAsync("AgregarEscaneadasAsync");
        await venta.GuardarHtmlAsync("venta-agregadas");
        await venta.EjecutarAsync("RevisarVentaDirectaAsync");
        await venta.GuardarHtmlAsync("venta-revision");
        await venta.EjecutarAsync("EditarVentaAsync");
        await venta.DetectarAsync(Codigo);
        await venta.GuardarHtmlAsync("venta-cero");
        await venta.DetectarAsync("NO-EXISTE");
        await venta.GuardarHtmlAsync("venta-no-encontrado");
        await using var inventario = await Vista.CrearAsync(test, inventario: true);
        Set(inventario.Componente, "Termino", "Producto");
        Set(inventario.Componente, "EstadoFiltro", EstadoUnidadInventario.Disponible);
        await inventario.EjecutarAsync("BuscarAsync");
        await inventario.GuardarHtmlAsync("inventario-filtros");
        await inventario.DetectarAsync("NO-EXISTE");
        await inventario.GuardarHtmlAsync("inventario-no-encontrado");
        Assert.Contains("Unidades de inventario", await inventario.HtmlAsync());
        await inventario.ScannerAsync("AbrirAsync");
        await inventario.ScannerAsync("CancelarAsync");
        await inventario.GuardarHtmlAsync("inventario-cancelado");
        Assert.Equal(0, venta.Externo.Llamadas + inventario.Externo.Llamadas);
    }

    private static async Task<TestDatabase> DatosAsync(int cantidad)
    {
        var test = await TestDatabase.CreateAsync();
        test.Producto.CodigoBarras = Codigo;
        await test.Db.SaveChangesAsync();
        if (cantidad > 0)
            Assert.True((await new CompraService(test.Db).RegistrarAsync(test.Compra(OrigenCompra.CompraLocal,
                "COM-SCANNER", new DateOnly(2026, 10, 6), cantidad))).IsSuccess);
        return test;
    }

    private sealed class Vista : IAsyncDisposable
    {
        private readonly ServiceProvider servicios;
        private readonly RenderizadorEstatico renderer;
        private readonly Activador activador = new();
        private HtmlRootComponent raiz;
        public object Componente => activador.Componente;
        public Navegacion Navigation { get; } = new();
        public ConsultaLocal Consulta { get; }
        public SeleccionControlada Seleccion { get; }
        public LookupProhibido Externo { get; } = new();
        public ProductoAltaPanel Panel => activador.Panel!;
        public ProductoForm AltaForm => activador.AltaForm!;
        public ImagenExternaNoDisponible Imagenes { get; } = new();
        public List<UnidadVentaDirectaFormModel> Articulos => Get<List<UnidadVentaDirectaFormModel>>(Componente, "Unidades");

        private Vista(TestDatabase test)
        {
            var productos = new ProductoService(test.Db);
            var conImagen = new ProductoConImagenService(productos, null!, test.Db, NullLogger<ProductoConImagenService>.Instance, Imagenes);
            Consulta = new(productos);
            Seleccion = new(new SeleccionOperativaService(test.Db));
            servicios = new ServiceCollection().AddLogging().AddSingleton(test.Db)
                .AddSingleton<IJSRuntime, JSInerte>().AddSingleton<NavigationManager>(Navigation)
                .AddSingleton<IComponentActivator>(activador).AddSingleton<IConsultaProductoCodigoBarras>(Consulta)
                .AddSingleton<ISeleccionOperativaService>(Seleccion).AddSingleton<IProductoLookupService>(Externo)
                .AddScoped<IProductoService, ProductoService>().AddScoped<IClienteService, ClienteService>()
                .AddScoped<IInventarioService, InventarioService>().AddScoped<IRecepcionCompraService, RecepcionCompraService>()
                .AddScoped<IPedidoService, PedidoService>().AddScoped<IVentaService, VentaService>()
                .AddScoped<IProveedorService, ProveedorService>().AddScoped<ICategoriaService, CategoriaService>()
                .AddSingleton<ITipoCambioReferenciaService, ReferenciaInerte>()
                .AddSingleton<IProductoConImagenService>(conImagen).AddSingleton<IAltaProductoAsistidaService>(conImagen)
                .AddSingleton<IRegistroCompraConComprobanteService>(new RegistroCompraConComprobanteService(
                    new CompraService(test.Db), null!, NullLogger<RegistroCompraConComprobanteService>.Instance))
                .BuildServiceProvider();
            renderer = new(servicios, servicios.GetRequiredService<ILoggerFactory>());
        }
        public static async Task<Vista> CrearAsync(TestDatabase test, bool inventario = false, bool compra = false)
        {
            var vista = new Vista(test);
            vista.raiz = await vista.renderer.Dispatcher.InvokeAsync(async () =>
            {
                var raiz = vista.renderer.BeginRenderingComponent(compra ? typeof(CompraNueva) : inventario ? typeof(Inventario) : typeof(VentaDirectaForm),
                    inventario || compra ? ParameterView.Empty : ParameterView.FromDictionary(new Dictionary<string, object?> { ["ClienteDesdeQuery"] = test.Cliente.Id.ToString() }));
                await raiz.QuiescenceTask;
                return raiz;
            });
            return vista;
        }
        public Task<string> HtmlAsync() => renderer.Dispatcher.InvokeAsync(() => WebUtility.HtmlDecode(raiz.ToHtmlString()));
        public async Task GuardarHtmlAsync(string nombre)
        {
            var carpeta = Environment.GetEnvironmentVariable("SCANNER_OPERATIVO_HTML_DIRECTORY");
            if (string.IsNullOrEmpty(carpeta)) return;
            Directory.CreateDirectory(carpeta);
            var html = await renderer.Dispatcher.InvokeAsync(() => raiz.ToHtmlString());
            await File.WriteAllTextAsync(Path.Combine(carpeta, nombre + ".html"), html);
        }
        public Task EjecutarAsync(string metodo, params object[] args) => renderer.Dispatcher.InvokeAsync(async () =>
        {
            if (Call(Componente, metodo, args) is Task tarea) await tarea;
            Call(Componente, "StateHasChanged");
        });
        public Task ScannerAsync(string metodo, params object[] args) => renderer.Dispatcher.InvokeAsync(async () =>
        {
            if (Call(activador.Scanner, metodo, args) is Task tarea) await tarea;
            Call(activador.Scanner, "StateHasChanged");
        });
        public Task AltaAsync(string metodo, params object[] args) => renderer.Dispatcher.InvokeAsync(async () =>
        {
            if (Call(activador.AltaForm!, metodo, args) is Task tarea) await tarea;
            Call(activador.AltaForm!, "StateHasChanged");
        });
        public Task PanelAsync(string metodo, params object[] args) => renderer.Dispatcher.InvokeAsync(async () =>
        {
            if (Call(activador.Panel!, metodo, args) is Task tarea) await tarea;
        });
        public Task ScannerDetalleAsync(int detalle, string metodo, params object[] args) => renderer.Dispatcher.InvokeAsync(async () =>
        {
            var scanner = activador.Scanners[detalle];
            if (Call(scanner, metodo, args) is Task tarea) await tarea;
            Call(scanner, "StateHasChanged");
        });
        public async Task DetectarCompraAsync(int detalle, string codigo)
        {
            await ScannerDetalleAsync(detalle, "AbrirAsync");
            await ScannerDetalleAsync(detalle, "FinalizarEscaneo", "detected", codigo);
        }
        public async Task DetectarAsync(string codigo)
        {
            await ScannerAsync("AbrirAsync");
            await ScannerAsync("FinalizarEscaneo", "detected", codigo);
        }
#pragma warning disable BL0006 // DisposeAsync se hereda de Renderer.
        public async ValueTask DisposeAsync() { await renderer.DisposeAsync(); await servicios.DisposeAsync(); }
#pragma warning restore BL0006
    }

    // Renderizamos el mismo componente y su markup, sin arrancar un circuito de servidor.
#pragma warning disable BL0006
    private sealed class RenderizadorEstatico(IServiceProvider servicios, ILoggerFactory logger) : StaticHtmlRenderer(servicios, logger)
    {
        protected override IComponent ResolveComponentForRenderMode(Type tipo, int? padre, IComponentActivator activador, IComponentRenderMode modo) =>
            activador.CreateInstance(tipo);
    }
#pragma warning restore BL0006

    private sealed class Activador : IComponentActivator
    {
        public object Componente { get; private set; } = null!;
        public BarcodeScanner Scanner { get; private set; } = null!;
        public List<BarcodeScanner> Scanners { get; } = [];
        public ProductoAltaPanel? Panel { get; private set; }
        public ProductoForm? AltaForm { get; private set; }
        public IComponent CreateInstance(Type tipo)
        {
            var componente = (IComponent)Activator.CreateInstance(tipo)!;
            if (componente is VentaDirectaForm or Inventario or CompraNueva) Componente = componente;
            if (componente is ProductoAltaPanel panel) Panel = panel;
            if (componente is ProductoForm alta) AltaForm = alta;
            if (componente is BarcodeScanner scanner) { Scanner = scanner; Scanners.Add(scanner); }
            return componente;
        }
    }
    private sealed class ConsultaLocal(IConsultaProductoCodigoBarras local) : IConsultaProductoCodigoBarras
    {
        public int Llamadas { get; private set; }
        public string? UltimoCodigo { get; private set; }
        public Func<string, Task<ProductoDto?>>? Respuesta { get; set; }
        public Task<ProductoDto?> ObtenerPorCodigoBarrasAsync(string codigo, CancellationToken ct = default)
        {
            Llamadas++; UltimoCodigo = codigo;
            return Respuesta is null ? local.ObtenerPorCodigoBarrasAsync(codigo, ct) : Respuesta(codigo);
        }
    }
    private sealed class SeleccionControlada(ISeleccionOperativaService local) : ISeleccionOperativaService
    {
        public Task<bool>? Respuesta { get; set; }
        public int Comprobaciones { get; private set; }
        public Task<bool> UnidadesDirectasDisponiblesAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
        { Comprobaciones++; return Respuesta ?? local.UnidadesDirectasDisponiblesAsync(ids, ct); }
        public Task<IReadOnlyList<UnidadInventarioDto>> ListarUnidadesDirectasAsync(int productoId, IReadOnlyCollection<int>? excluir = null, CancellationToken ct = default) => local.ListarUnidadesDirectasAsync(productoId, excluir, ct);
        public Task<IReadOnlyList<UnidadInventarioDto>> BuscarUnidadesAsync(string termino, int? productoId = null, int? pedidoId = null, IReadOnlyCollection<int>? excluir = null, bool soloReservadas = false, CancellationToken ct = default) => local.BuscarUnidadesAsync(termino, productoId, pedidoId, excluir, soloReservadas, ct);
        public Task<IReadOnlyList<PedidoDto>> BuscarPedidosAsync(string termino, CancellationToken ct = default) => local.BuscarPedidosAsync(termino, ct);
        public Task<DisponibilidadReservaDto> DisponibilidadAsync(int productoId, CancellationToken ct = default) => local.DisponibilidadAsync(productoId, ct);
        public Task<IReadOnlyList<UnidadInventarioDto>> ListarReservablesAsync(int productoId, CancellationToken ct = default) => local.ListarReservablesAsync(productoId, ct);
        public Task<IReadOnlyList<PendienteClienteDto>> PendientesClienteAsync(int clienteId, CancellationToken ct = default) => local.PendientesClienteAsync(clienteId, ct);
    }
    private sealed class LookupProhibido : IProductoLookupService
    {
        public int Llamadas { get; private set; }
        public IProductoLookupService? Servicio { get; set; }
        public Task<ProductoLookupRonda> IniciarAsync(string codigo, CancellationToken ct = default)
        { Llamadas++; return Servicio?.IniciarAsync(codigo, ct) ?? throw new InvalidOperationException("Lookup externo no permitido"); }
        public Task ContinuarAsync(ProductoLookupRonda ronda, CancellationToken ct = default)
        { Llamadas++; return Servicio?.ContinuarAsync(ronda, ct) ?? throw new InvalidOperationException("Lookup externo no permitido"); }
    }
    private sealed class ReferenciaInerte : ITipoCambioReferenciaService
    {
        public Task<TipoCambioReferenciaResultado> ConsultarAsync(MonedaCompra moneda, DateOnly fecha, CancellationToken cancellationToken = default) =>
            Task.FromResult(TipoCambioReferenciaResultado.NoDisponible());
    }
    private sealed class ImagenExternaNoDisponible : IImagenProductoExternaService
    {
        public List<string> Solicitudes { get; } = [];
        public Task<ServiceResult<Stream>> DescargarAsync(string url, CancellationToken ct = default)
        { Solicitudes.Add(url); return Task.FromResult(ServiceResult<Stream>.Failure("Imagen no disponible en esta prueba")); }
    }
    private sealed class JSInerte : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult((T)(object)new ModuloInerte());
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<T>(identifier, args);
    }
    private sealed class ModuloInerte : IJSObjectReference
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(identifier == "abrir" ? (T)(object)"ready" : default!);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<T>(identifier, args);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Application.Services;
using ResellManager.Infrastructure.Services;
using ResellManager.Web.Components.Productos;
using static ResellManager.Tests.RevisionOperacionesTests;

namespace ResellManager.Tests;

public sealed class ProductoLookupTests
{
    internal const string Codigo = "0123456789012";
    internal static ProductoLookupCandidato Candidato(string nombre = "Encontrado") =>
        new() { CodigoBarras = Codigo, Nombre = nombre, Fuente = "Prueba" };

    [Fact]
    public async Task CoincidenciaLocalExacta_NoConsultaProveedoresNiCreaProducto()
    {
        await using var db = await TestDatabase.CreateAsync();
        db.Producto.CodigoBarras = Codigo;
        await db.Db.SaveChangesAsync();
        var proveedor = new ProveedorFalso("Primero", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato()));
        var productos = new ProductoService(db.Db);
        var ronda = await new ProductoLookupService(productos, [proveedor]).IniciarAsync(Codigo);
        Assert.Equal(db.Producto.Id, ronda.ProductoLocal!.Id);
        Assert.Equal(0, proveedor.Llamadas);
        Assert.Null(ronda.Candidato);
        Assert.Single(await db.Db.Productos.ToListAsync());
        Assert.Null(await productos.ObtenerPorCodigoBarrasAsync(Codigo[1..]));
        Assert.Null(await productos.ObtenerPorCodigoBarrasAsync(" " + Codigo));
    }

    [Fact]
    public async Task PrimerProveedorEncuentra_SeDetieneHastaLaDecision()
    {
        var primero = new ProveedorFalso("Primero", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato()));
        var segundo = new ProveedorFalso("Segundo", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato("Otro")));
        var ronda = await Servicio(primero, segundo).IniciarAsync(Codigo);
        Assert.Equal("Encontrado", ronda.Candidato!.Nombre);
        Assert.Equal("Primero", ronda.Candidato.Fuente);
        Assert.Equal(1, primero.Llamadas);
        Assert.Equal(0, segundo.Llamadas);
        Assert.False(ronda.Agotada);
    }

    [Theory]
    [InlineData(EstadoLookupProveedor.NoEncontrado)]
    [InlineData(EstadoLookupProveedor.Timeout)]
    [InlineData(EstadoLookupProveedor.LimitePeticiones)]
    [InlineData(EstadoLookupProveedor.NoDisponible)]
    [InlineData(EstadoLookupProveedor.RespuestaInvalida)]
    public async Task AusenciaOFallo_ContinuaConSegundo(EstadoLookupProveedor estado)
    {
        var primero = new ProveedorFalso("Primero", new ProductoLookupRespuesta(estado));
        var segundo = new ProveedorFalso("Segundo", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato()));
        var ronda = await Servicio(primero, segundo).IniciarAsync(Codigo);
        Assert.Equal("Segundo", ronda.Candidato!.Fuente);
        Assert.Equal(estado, ronda.Intentos[0].Estado);
        Assert.Equal(1, primero.Llamadas);
        Assert.Equal(1, segundo.Llamadas);
    }

    [Fact]
    public async Task ExcepcionTecnica_NoBloqueaFallback()
    {
        var primero = new ProveedorFalso("Primero", _ => throw new IOException("Fallo simulado"));
        var segundo = new ProveedorFalso("Segundo", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato()));
        var ronda = await Servicio(primero, segundo).IniciarAsync(Codigo);
        Assert.Equal(EstadoLookupProveedor.NoDisponible, ronda.Intentos[0].Estado);
        Assert.NotNull(ronda.Candidato);
    }

    [Fact]
    public async Task OtraFuente_NoRepiteFuentesDescartadas_AgotamientoPermiteRondaNueva()
    {
        var primero = new ProveedorFalso("Primero", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato()));
        var segundo = new ProveedorFalso("Segundo", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato("Segundo")));
        var servicio = Servicio(primero, segundo);
        var ronda = await servicio.IniciarAsync(Codigo);
        await servicio.ContinuarAsync(ronda);
        Assert.Equal("Segundo", ronda.Candidato!.Nombre);
        await servicio.ContinuarAsync(ronda);
        await servicio.ContinuarAsync(ronda);
        Assert.True(ronda.Agotada);
        Assert.Null(ronda.Candidato);
        Assert.Equal(Codigo, ronda.CodigoConsultado);
        Assert.Equal(1, primero.Llamadas);
        Assert.Equal(1, segundo.Llamadas);
        var nueva = await servicio.IniciarAsync(Codigo);
        Assert.NotNull(nueva.Candidato);
        Assert.Equal(2, primero.Llamadas);
    }

    [Fact]
    public async Task TodosAgotados_ConservaCodigoYSoloRegistraIntentos()
    {
        var servicio = Servicio(new ProveedorFalso("Primero", new ProductoLookupRespuesta(EstadoLookupProveedor.Timeout)),
            new ProveedorFalso("Segundo", new ProductoLookupRespuesta(EstadoLookupProveedor.LimitePeticiones)));
        var ronda = await servicio.IniciarAsync(Codigo);
        Assert.True(ronda.Agotada);
        Assert.Equal(Codigo, ronda.CodigoConsultado);
        Assert.Equal(2, ronda.Intentos.Count);
        Assert.Null(ronda.Candidato);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12\n34")]
    [InlineData("12\u000034")]
    public async Task EntradaInvalida_NoConsultaFuentes(string codigo)
    {
        var primero = new ProveedorFalso("Primero", new ProductoLookupRespuesta(EstadoLookupProveedor.NoEncontrado));
        var ronda = await Servicio(primero).IniciarAsync(codigo);
        Assert.NotNull(ronda.ErrorEntrada);
        Assert.Equal(0, primero.Llamadas);
    }

    [Theory]
    [InlineData("", Codigo)]
    [InlineData("Otro producto", "9999999999999")]
    public async Task RespuestaSinNombreOCodigoDistinto_DescartaYContinua(string nombre, string codigo)
    {
        var primero = new ProveedorFalso("Primero", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado,
            Candidato(nombre) with { CodigoBarras = codigo }));
        var segundo = new ProveedorFalso("Segundo", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato()));
        var ronda = await Servicio(primero, segundo).IniciarAsync(Codigo);
        Assert.Equal(EstadoLookupProveedor.RespuestaInvalida, ronda.Intentos[0].Estado);
        Assert.Equal("Segundo", ronda.Candidato!.Fuente);
    }

    [Fact]
    public async Task Codigo128_ConservaEspaciosYDiferenciaMayusculas()
    {
        await using var db = await TestDatabase.CreateAsync();
        var servicio = new ProductoService(db.Db);
        var input = new ProductoInput(" ABC 123 ", "Manual", null, null, null, null, null, 10, db.Categoria.Id);
        var resultado = await servicio.CrearAsync(input);
        Assert.Equal(" ABC 123 ", resultado.Value!.CodigoBarras);
        Assert.NotNull(await servicio.ObtenerPorCodigoBarrasAsync(" ABC 123 "));
        Assert.Null(await servicio.ObtenerPorCodigoBarrasAsync("ABC 123"));
        Assert.Null(await servicio.ObtenerPorCodigoBarrasAsync(" abc 123 "));
    }

    [Fact]
    public async Task RevisarYCerrar_NoModificaFormularioNiPersiste()
    {
        var modelo = ModeloManual();
        using var formulario = Formulario(modelo, Servicio(
            new ProveedorFalso("Primero", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato()))));
        var antes = modelo.CrearInstantanea();
        await CallAsync(formulario, "BuscarProductoAsync");
        Comparar(antes, modelo);
        Assert.True(Leer<bool>(formulario, "RevisandoLookup"));
        Call(formulario, "DescartarCandidato");
        Comparar(antes, modelo);
        Assert.False(Leer<bool>(formulario, "RevisandoLookup"));
        Assert.False(Leer<ProductoLookupImportacion>(formulario, "Importacion").PuedeDeshacer);
    }

    [Fact]
    public async Task Aceptar_CopiaSoloPresentes_NoImportaPrecioCategoriaNiCodigoDelProveedor_DeshacerExacto()
    {
        var modelo = ModeloManual();
        modelo.ImagenContenido = [1, 2, 3];
        var candidato = Candidato() with
        {
            CodigoBarras = Codigo[1..], Marca = "Marca nueva", PesoGramos = 250,
            CategoriaExterna = "Categoría desconocida", ImagenUrl = "https://images.example.com/producto.png"
        };
        using var form = Formulario(modelo, Servicio(new ProveedorFalso("Primero", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, candidato))));
        // La selección dependiente se inicializa al recibir categorías, antes de iniciar el lookup.
        var antes = modelo.CrearInstantanea();
        Assert.Equal(1, antes.CategoriaPrincipalId);
        Assert.Null(antes.SubcategoriaId);
        await CallAsync(form, "BuscarProductoAsync");
        Call(form, "UsarCandidato");
        Assert.Equal("Encontrado", modelo.Nombre);
        Assert.Equal("Marca nueva", modelo.Marca);
        Assert.Equal(antes.Descripcion, modelo.Descripcion);
        Assert.Equal(antes.Color, modelo.Color);
        Assert.Equal(antes.Presentacion, modelo.Presentacion);
        Assert.Equal(antes.PrecioSugerido, modelo.PrecioSugerido);
        Assert.Equal(antes.CategoriaId, modelo.CategoriaId);
        Assert.Equal(Codigo, modelo.CodigoBarras);
        Assert.Equal(250m, modelo.PesoGramos);
        Assert.Null(modelo.Volumen);
        Assert.Null(modelo.ImagenExternaUrl); // La imagen manual existente tiene prioridad.
        modelo.Nombre = "Corrección posterior";
        modelo.PrecioSugerido = 999;
        modelo.ImagenContenido = [4, 5];
        Call(form, "DeshacerImportacion");
        Comparar(antes, modelo);
        Assert.False(Leer<ProductoLookupImportacion>(form, "Importacion").PuedeDeshacer);
    }

    [Fact]
    public void ImportacionPosterior_DeshaceSoloUltimaAceptacion_ImagenPendienteTambienSeRestaura()
    {
        var modelo = ModeloManual();
        var importacion = new ProductoLookupImportacion();
        importacion.Aplicar(modelo, Candidato("Primero") with { ImagenUrl = "https://images.example.com/primero.png" });
        modelo.Color = "Captura intermedia";
        var antesSegunda = modelo.CrearInstantanea();
        importacion.Aplicar(modelo, Candidato("Segundo") with { ImagenUrl = "https://images.example.com/segundo.png" });
        importacion.Deshacer(modelo);
        Comparar(antesSegunda, modelo);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(100, 200)]
    [InlineData(0, -1)]
    public void MedidasInvalidasOAmbiguas_NoModificanMedidaManual(int peso, int volumen)
    {
        var modelo = ModeloManual();
        var importacion = new ProductoLookupImportacion();
        importacion.Aplicar(modelo, Candidato() with { PesoGramos = peso, ContenidoMl = volumen });
        Assert.Equal(1500m, modelo.ContenidoMl);
        Assert.Null(modelo.Peso);
    }

    [Theory]
    [InlineData("3 x 100 ml")]
    [InlineData("100 ml / 200 g")]
    [InlineData("8 fl oz")]
    [InlineData("1,000 g")]
    [InlineData("mucho")]
    public void MedidasNoConvertibles_NoSeImportan(string texto)
    {
        Assert.Equal((null as decimal?, null as decimal?), ProductoLookupDatos.Medida(texto));
    }

    [Fact]
    public void TextoExterno_NormalizaRecortaYEliminaContenidoVacioOInvalido()
    {
        var candidato = ProductoLookupDatos.Normalizar(Candidato(" <b>Nombre</b> \n nuevo ") with
        {
            Descripcion = new string('D', 600), Marca = "<script>alert(1)</script>",
            Color = new string('C', 60), Talla = "\u0000", Presentacion = new string('P', 110)
        });
        Assert.Equal("Nombre nuevo", candidato.Nombre);
        Assert.Equal(500, candidato.Descripcion!.Length);
        Assert.Equal(50, candidato.Color!.Length);
        Assert.Equal(100, candidato.Presentacion!.Length);
        Assert.Null(candidato.Marca);
        Assert.Null(candidato.Talla);
        Assert.Null(typeof(ProductoLookupCandidato).GetProperty("PrecioSugerido"));
        Assert.Null(typeof(ProductoLookupCandidato).GetProperty("CategoriaId"));
    }

    [Fact]
    public async Task Scanner_AltaIniciaConsulta_EdicionSoloCaptura_ContratoDeCallbackConservado()
    {
        var proveedor = new ProveedorFalso("Primero", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato()));
        using var alta = Formulario(ModeloManual(), Servicio(proveedor));
        await CallAsync(alta, "CodigoEscaneado", Codigo);
        Assert.Equal(1, proveedor.Llamadas);
        Assert.Equal(Codigo, alta.Modelo.CodigoBarras);
        using var edicion = Formulario(ModeloManual(), Servicio(proveedor));
        Set(edicion, "LookupHabilitado", false);
        await CallAsync(edicion, "CodigoEscaneado", " CODE 128 ");
        Assert.Equal(1, proveedor.Llamadas);
        Assert.Equal(" CODE 128 ", edicion.Modelo.CodigoBarras);
    }

    [Fact]
    public async Task BusquedaSimultaneaYDobleAceptacion_SoloUnaConsultaYSoloGuardarPersiste()
    {
        var pendiente = new TaskCompletionSource<ProductoLookupRespuesta>();
        var proveedor = new ProveedorFalso("Primero", _ => pendiente.Task);
        using var form = Formulario(ModeloManual(), Servicio(proveedor));
        var guardados = 0;
        Set(form, "OnGuardar", EventCallback.Factory.Create<ProductoFormModel>(new object(), _ => guardados++));
        var busqueda = CallAsync(form, "BuscarProductoAsync");
        await CallAsync(form, "BuscarProductoAsync");
        await CallAsync(form, "GuardarAsync");
        Assert.Equal(1, proveedor.Llamadas);
        Assert.Equal(0, guardados);
        pendiente.SetResult(new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato()));
        await busqueda;
        await CallAsync(form, "GuardarAsync");
        Assert.Equal(0, guardados);
        Call(form, "UsarCandidato");
        Call(form, "UsarCandidato");
        Assert.Equal(0, guardados);
        await CallAsync(form, "GuardarAsync");
        Assert.Equal(1, guardados);
        Call(form, "DeshacerImportacion");
        Assert.Equal("Manual", form.Modelo.Nombre);
    }

    [Fact]
    public async Task RespuestaTardiaTrasCambiarModeloODispose_NoAplicaNiAbreRevision()
    {
        var pendiente = new TaskCompletionSource<ProductoLookupRespuesta>();
        using var form = Formulario(ModeloManual(), Servicio(new ProveedorFalso("Primero", _ => pendiente.Task)));
        var tarea = CallAsync(form, "BuscarProductoAsync");
        Set(form, "Modelo", new ProductoFormModel { Nombre = "Nuevo formulario", CodigoBarras = Codigo });
        Call(form, "OnParametersSet");
        form.Dispose();
        pendiente.SetResult(new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato()));
        await tarea;
        Assert.False(Leer<bool>(form, "RevisandoLookup"));
        Assert.Null(Leer<ProductoLookupRonda?>(form, "Ronda"));
        Assert.Equal("Nuevo formulario", form.Modelo.Nombre);
    }

    [Fact]
    public async Task TimeoutYLimite_AgotanYPermitenGuardarManualConCodigo()
    {
        using var form = Formulario(ModeloManual(), Servicio(
            new ProveedorFalso("Primero", new ProductoLookupRespuesta(EstadoLookupProveedor.Timeout)),
            new ProveedorFalso("Segundo", new ProductoLookupRespuesta(EstadoLookupProveedor.LimitePeticiones))));
        var guardados = 0;
        Set(form, "OnGuardar", EventCallback.Factory.Create<ProductoFormModel>(new object(), _ => guardados++));
        await CallAsync(form, "BuscarProductoAsync");
        Assert.True(Leer<ProductoLookupRonda>(form, "Ronda").Agotada);
        await CallAsync(form, "GuardarAsync");
        Assert.Equal(1, guardados);
        Assert.Equal(Codigo, form.Modelo.CodigoBarras);
    }

    [Fact]
    public async Task CandidatoSinNombre_ImportaSoloMarcaYConservaNombreManual()
    {
        var modelo = ModeloManual();
        using var form = Formulario(modelo, Servicio(new ProveedorFalso("Primero",
            new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado,
                new ProductoLookupCandidato { Marca = "Marca encontrada" }))));
        await CallAsync(form, "BuscarProductoAsync");
        Assert.True(Leer<bool>(form, "RevisandoLookup"));
        Call(form, "UsarCandidato");
        Assert.Equal("Manual", modelo.Nombre);
        Assert.Equal("Marca encontrada", modelo.Marca);
        Assert.Equal(Codigo, modelo.CodigoBarras);
    }

    [Fact]
    public async Task OtraFuente_RecompruebaLocalSiFueRegistradoDuranteRevision()
    {
        await using var db = await TestDatabase.CreateAsync();
        var productos = new ProductoService(db.Db);
        var primero = new ProveedorFalso("Primero", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato()));
        var segundo = new ProveedorFalso("Segundo", new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato()));
        var servicio = new ProductoLookupService(productos, [primero, segundo]);
        var ronda = await servicio.IniciarAsync(Codigo);
        var creado = await productos.CrearAsync(new(Codigo, "Registro intermedio", null, null, null, null, null, 10, db.Categoria.Id));
        await servicio.ContinuarAsync(ronda);
        Assert.Equal(creado.Value!.Id, ronda.ProductoLocal!.Id);
        Assert.Null(ronda.Candidato);
        Assert.Equal(0, segundo.Llamadas);
    }

    [Fact]
    public async Task LocalExistente_BloqueaGuardarDelFormulario_CambiarCodigoLiberaCapturaManual()
    {
        var local = new ProductoDto(1, "PRO-LOCAL", Codigo, "Existente", null, null, null, null, null, 10, 1, "General");
        using var form = Formulario(ModeloManual(), new ProductoLookupService(new ConsultaFalsa(local), []));
        var guardados = 0;
        Set(form, "OnGuardar", EventCallback.Factory.Create<ProductoFormModel>(new object(), _ => guardados++));
        await CallAsync(form, "BuscarProductoAsync");
        await CallAsync(form, "GuardarAsync");
        Assert.Equal(0, guardados);
        form.Modelo.CodigoBarras = "CODIGO-NUEVO";
        Call(form, "CodigoManualCambiado");
        await CallAsync(form, "GuardarAsync");
        Assert.Equal(1, guardados);
    }
    [Fact]
    public async Task Revision_RecibeCodigoConsultadoRealDelFormulario()
    {
        using var form = Formulario(ModeloManual(), Servicio(new ProveedorFalso("Primero",
            new ProductoLookupRespuesta(EstadoLookupProveedor.Encontrado, Candidato() with { CodigoBarras = Codigo[1..] }))));
        await CallAsync(form, "BuscarProductoAsync");
        using var tree = new RenderTreeBuilder();
        typeof(ProductoForm).GetMethod("BuildRenderTree", Flags)!.Invoke(form, [tree]);
#pragma warning disable BL0006 // Inspección de parámetros Razor como las regresiones UI existentes.
        var frames = tree.GetFrames();
        var componentes = frames.Array.Take(frames.Count).ToArray();
        Assert.Contains(componentes, frame => frame.FrameType == RenderTreeFrameType.Component
            && frame.ComponentType == typeof(ProductoLookupRevision));
        Assert.Contains(componentes, frame => frame.FrameType == RenderTreeFrameType.Attribute
            && frame.AttributeName == "CodigoConsultado" && Equals(Codigo, frame.AttributeValue));
#pragma warning restore BL0006
    }
    internal static ProductoFormModel ModeloManual() => new()
    {
        CodigoBarras = Codigo, Nombre = "Manual", Descripcion = "Descripción manual", Marca = "Marca manual",
        Modelo = "Modelo manual", Color = "Azul", Talla = "M", Presentacion = "Caja",
        Volumen = 1.5m, VolumenUnidad = UnidadVolumen.L, PesoUnidad = UnidadPeso.Lb,
        PrecioSugerido = 125.50m, CategoriaId = 1
    };

    internal static ProductoForm Formulario(ProductoFormModel modelo, IProductoLookupService servicio)
    {
        var form = new ProductoForm();
        Set(form, "Modelo", modelo);
        Set(form, "Categorias", new CategoriaDto[] { new(1, "General", null) });
        Set(form, "LookupHabilitado", true);
        Set(form, "LookupService", servicio);
        Set(form, "LookupLogger", NullLogger<ProductoForm>.Instance);
        Call(form, "OnParametersSet");
        return form;
    }

    internal static T Leer<T>(object instancia, string nombre) =>
        (T)(instancia.GetType().GetField(nombre, Flags)?.GetValue(instancia)
            ?? instancia.GetType().GetProperty(nombre, Flags)?.GetValue(instancia))!;

    internal static void Comparar(ProductoFormModel esperado, ProductoFormModel actual)
    {
        foreach (var propiedad in typeof(ProductoFormModel).GetProperties())
            Assert.Equal(propiedad.GetValue(esperado), propiedad.GetValue(actual));
    }

    internal static ProductoLookupService Servicio(params IProductoLookupProvider[] proveedores) =>
        new(new ConsultaFalsa(), proveedores);

    internal sealed class ConsultaFalsa(ProductoDto? local = null) : IConsultaProductoCodigoBarras
    {
        public Task<ProductoDto?> ObtenerPorCodigoBarrasAsync(string codigo, CancellationToken ct = default) =>
            Task.FromResult(local);
    }

    internal sealed class ProveedorFalso(string fuente, Func<CancellationToken, Task<ProductoLookupRespuesta>> respuesta)
        : IProductoLookupProvider
    {
        public ProveedorFalso(string fuente, ProductoLookupRespuesta respuesta)
            : this(fuente, _ => Task.FromResult(respuesta)) { }
        public string Fuente => fuente;
        public int Llamadas { get; private set; }
        public Task<ProductoLookupRespuesta> ConsultarAsync(string codigo, CancellationToken ct = default)
        {
            Llamadas++;
            return respuesta(ct);
        }
    }
}

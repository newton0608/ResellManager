using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Infrastructure.Persistence;
using SkiaSharp;

namespace ResellManager.Tests;

[Collection("Integración web")]
public sealed class ImportacionTemporalEndpointsIntegracionTests : PruebaWebAislada
{
    private const string Prefijo = "/api/admin/importacion-productos";
    // Credencial ficticia exclusivamente para el host aislado de pruebas.
    private const string Clave = "clave-ficticia-importacion-pruebas-1234567890";

    [Fact]
    public async Task EndpointsDeshabilitados_NoExponenEstadoNiPermitenEscrituras()
    {
        using var cliente = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var estado = await cliente.GetAsync($"{Prefijo}/estado");
        Assert.Equal(HttpStatusCode.NotFound, estado.StatusCode);
        using var validar = await cliente.PostAsJsonAsync($"{Prefijo}/validar",
            new[] { new ProductoInput(null, "Prueba", null, null, null, null, null, 1, 1) });
        Assert.Equal(HttpStatusCode.NotFound, validar.StatusCode);
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent("{}"), "producto");
        using var crear = await cliente.PostAsync($"{Prefijo}/crear", multipart);
        Assert.Equal(HttpStatusCode.NotFound, crear.StatusCode);
        await SinProductosAsync(factory);
    }

    [Theory]
    [InlineData("sin-clave")]
    [InlineData("clave-invalida")]
    [InlineData("caducada")]
    [InlineData("duracion-excesiva")]
    [InlineData("externo")]
    [InlineData("dominio")]
    [InlineData("proxy")]
    public async Task ApiHabilitada_RechazaAccesoNoAutorizadoAntesDeLeerCuerpo(string caso)
    {
        var expira = caso == "caducada" ? DateTimeOffset.UtcNow.AddMinutes(-1)
            : caso == "duracion-excesiva" ? DateTimeOffset.UtcNow.AddDays(2) : DateTimeOffset.UtcNow.AddHours(1);
        using var app = Habilitar(caso == "externo" ? IPAddress.Parse("192.0.2.20") : null, expira);
        using var cliente = Cliente(app);
        if (caso == "sin-clave") cliente.DefaultRequestHeaders.Remove("X-Resell-Import-Key");
        if (caso == "clave-invalida")
        {
            cliente.DefaultRequestHeaders.Remove("X-Resell-Import-Key");
            cliente.DefaultRequestHeaders.Add("X-Resell-Import-Key", "invalida");
        }
        if (caso == "dominio") cliente.DefaultRequestHeaders.Host = "app.resellmanager.tech";
        if (caso == "proxy") cliente.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");
        using var cuerpo = new StringContent("JSON inválido", Encoding.UTF8, "application/json");
        using var respuesta = await cliente.PostAsync($"{Prefijo}/validar", cuerpo);
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains("no-store", respuesta.Headers.CacheControl!.ToString());
        await SinProductosAsync(app);
    }

    [Fact]
    public async Task Validar_DetectaDuplicadosSinEscriturasYPermiteRevisarVariantes()
    {
        using var app = Habilitar();
        using var cliente = Cliente(app);
        var input = await InputAsync(app);
        using var estado = await cliente.GetAsync($"{Prefijo}/estado");
        Assert.Equal(HttpStatusCode.OK, estado.StatusCode);
        using var respuesta = await cliente.PostAsJsonAsync($"{Prefijo}/validar",
            new[] { input, input, input with { ContenidoMl = 50 } });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var filas = json.RootElement.GetProperty("resultados");
        Assert.True(filas[0].GetProperty("valido").GetBoolean());
        Assert.False(filas[1].GetProperty("valido").GetBoolean());
        Assert.True(filas[2].GetProperty("valido").GetBoolean());
        await SinProductosAsync(app);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    public async Task Crear_ConservaGaleriaYPortadaSinCrearInventarioYReintentoNoDuplica(int cantidad)
    {
        using var app = Habilitar();
        using var cliente = Cliente(app);
        var input = await InputAsync(app);
        using var previa = await cliente.PostAsJsonAsync($"{Prefijo}/validar", new[] { input });
        Assert.Equal(HttpStatusCode.OK, previa.StatusCode);
        using var cuerpo = Formulario(input, Enumerable.Range(0, cantidad).Select(i => Imagen(i == 0 ? SKColors.Blue : SKColors.Red)).ToArray());
        cuerpo.Add(new StringContent("1"), "portadaIndice");
        using var creado = await cliente.PostAsync($"{Prefijo}/crear", cuerpo);
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        using var json = JsonDocument.Parse(await creado.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("inventarioCreado").GetBoolean());
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
            var producto = Assert.Single(await db.Productos.Include(x => x.Imagenes).ToListAsync());
            Assert.Equal(cantidad, producto.Imagenes.Count);
            Assert.Equal(producto.Imagenes.Single(x => x.Orden == 1).RutaRelativa, producto.ImagenPrincipalRuta);
            Assert.Empty(await db.UnidadesInventario.ToListAsync());
            Assert.Empty(await db.Compras.ToListAsync());
            Assert.Empty(await db.Pedidos.ToListAsync());
            Assert.Empty(await db.Ventas.ToListAsync());
        }
        using var repetido = Formulario(input);
        using var rechazo = await cliente.PostAsync($"{Prefijo}/crear", repetido);
        Assert.Equal(HttpStatusCode.Conflict, rechazo.StatusCode);
        await using var final = app.Services.CreateAsyncScope();
        Assert.Single(await final.ServiceProvider.GetRequiredService<ResellManagerDbContext>().Productos.ToListAsync());
    }

    [Fact]
    public async Task CrearConcurrenciaSinCodigo_SoloConfirmaUnProducto()
    {
        using var app = Habilitar();
        using var cliente = Cliente(app);
        var input = await InputAsync(app);
        using var primero = Formulario(input);
        using var segundo = Formulario(input);
        var respuestas = await Task.WhenAll(cliente.PostAsync($"{Prefijo}/crear", primero), cliente.PostAsync($"{Prefijo}/crear", segundo));
        try
        {
            Assert.Single(respuestas.Where(x => x.StatusCode == HttpStatusCode.Created));
            Assert.Single(respuestas.Where(x => x.StatusCode == HttpStatusCode.Conflict));
            await using var scope = app.Services.CreateAsyncScope();
            Assert.Single(await scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>().Productos.ToListAsync());
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
    }

    [Fact]
    public async Task CrearSinFotos_NormalizaCodigoYRechazaReusoConNombreDistinto()
    {
        using var app = Habilitar();
        using var cliente = Cliente(app);
        var input = (await InputAsync(app)) with { CodigoBarras = " 7501234567890 " };
        using var cuerpo = Formulario(input);
        using var creado = await cliente.PostAsync($"{Prefijo}/crear", cuerpo);
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        using var otro = Formulario(input with { Nombre = "Otro nombre", CodigoBarras = "7501234567890" });
        using var rechazo = await cliente.PostAsync($"{Prefijo}/crear", otro);
        Assert.Equal(HttpStatusCode.Conflict, rechazo.StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        Assert.Equal("7501234567890", Assert.Single(await db.Productos.ToListAsync()).CodigoBarras);
        Assert.Empty(await db.UnidadesInventario.ToListAsync());
    }

    [Theory]
    [InlineData("sin-confirmacion", HttpStatusCode.Conflict)]
    [InlineData("nueve-fotos", HttpStatusCode.BadRequest)]
    [InlineData("archivo-vacio", HttpStatusCode.BadRequest)]
    [InlineData("archivo-corrupto", HttpStatusCode.Conflict)]
    [InlineData("portada-invalida", HttpStatusCode.BadRequest)]
    [InlineData("campo-desconocido", HttpStatusCode.BadRequest)]
    [InlineData("imagen-grande", HttpStatusCode.BadRequest)]
    public async Task CrearRechazado_NoDejaFilasArchivosNiTemporales(string caso, HttpStatusCode esperado)
    {
        using var app = Habilitar();
        using var cliente = Cliente(app);
        var input = await InputAsync(app);
        if (caso == "sin-confirmacion") cliente.DefaultRequestHeaders.Remove("X-Importacion-Confirmada");
        var fotos = caso switch
        {
            "nueve-fotos" => Enumerable.Range(0, 9).Select(_ => Imagen(SKColors.Blue)).ToArray(),
            "archivo-vacio" => [Array.Empty<byte>()],
            "archivo-corrupto" => [Encoding.UTF8.GetBytes("esto no es una fotografía")],
            "imagen-grande" => [new byte[8 * 1024 * 1024 + 1]],
            _ => Array.Empty<byte[]>()
        };
        using var cuerpo = Formulario(input, fotos);
        if (caso == "portada-invalida") cuerpo.Add(new StringContent("1"), "portadaIndice");
        if (caso == "campo-desconocido") cuerpo.Add(new StringContent("https://example.invalid/foto.png"), "imagenUrl");
        using var respuesta = await cliente.PostAsync($"{Prefijo}/crear", cuerpo);
        Assert.Equal(esperado, respuesta.StatusCode);
        await SinProductosAsync(app);
        Assert.Empty(ArchivosImagenes());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Validar_LimitaCuerpoConYSinContentLength(bool sinLongitud)
    {
        using var app = Habilitar();
        using var cliente = Cliente(app);
        var bytes = Encoding.UTF8.GetBytes("[{\"nombre\":\"" + new string('x', 270000) + "\"}]");
        using HttpContent cuerpo = sinLongitud ? new ContenidoSinLongitud(bytes) : new ByteArrayContent(bytes);
        cuerpo.Headers.ContentType = new("application/json");
        using var respuesta = await cliente.PostAsync($"{Prefijo}/validar", cuerpo);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, respuesta.StatusCode);
        await SinProductosAsync(app);
    }

    [Fact]
    public async Task LimiteFrecuencia_RechazaSolicitudVeintiuno()
    {
        using var app = Habilitar();
        using var cliente = Cliente(app);
        for (var i = 0; i < 20; i++)
        {
            using var respuesta = await cliente.GetAsync($"{Prefijo}/estado");
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }
        using var limitada = await cliente.GetAsync($"{Prefijo}/estado");
        Assert.Equal(HttpStatusCode.TooManyRequests, limitada.StatusCode);
    }

    [Fact]
    public async Task RevocacionConfiguracion_CierraLaSiguienteSolicitud()
    {
        using var app = Habilitar();
        using var cliente = Cliente(app);
        using var antes = await cliente.GetAsync($"{Prefijo}/estado");
        Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        app.Services.GetRequiredService<IConfiguration>()["ImportacionProductos:Habilitada"] = "false";
        using var despues = await cliente.GetAsync($"{Prefijo}/estado");
        Assert.Equal(HttpStatusCode.NotFound, despues.StatusCode);
    }

    [Fact]
    public async Task FalloAlConfirmarSegundaFoto_RollbackYLimpiadoDeArchivos()
    {
        using var app = Habilitar(falloDisco: true);
        using var cliente = Cliente(app);
        var input = await InputAsync(app);
        using var cuerpo = Formulario(input, Imagen(SKColors.Blue), Imagen(SKColors.Red));
        using var respuesta = await cliente.PostAsync($"{Prefijo}/crear", cuerpo);
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        await SinProductosAsync(app);
        Assert.Empty(ArchivosImagenes());
    }

    [Fact]
    public async Task ImportacionLocal_HttpConHttpsForzadoConservaRedireccionEnOtrasRutas()
    {
        using var app = Habilitar();
        using var cliente = Cliente(app);
        cliente.BaseAddress = new Uri("http://localhost:8080");
        using var estado = await cliente.GetAsync($"{Prefijo}/estado");
        Assert.Equal(HttpStatusCode.OK, estado.StatusCode);
        using var salud = await cliente.GetAsync("/health");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, salud.StatusCode);
        Assert.Equal("https", salud.Headers.Location!.Scheme);
        cliente.DefaultRequestHeaders.Host = "app.resellmanager.tech";
        using var dominio = await cliente.GetAsync($"{Prefijo}/estado");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, dominio.StatusCode);
    }

    private WebApplicationFactory<Program> Habilitar(IPAddress? ip = null, DateTimeOffset? expira = null, bool falloDisco = false) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowedHosts"] = "app.resellmanager.tech;localhost",
                ["ImportacionProductos:Habilitada"] = "true",
                ["ImportacionProductos:Token"] = Clave,
                ["ImportacionProductos:ExpiraUtc"] = (expira ?? DateTimeOffset.UtcNow.AddHours(1)).ToString("O")
            }));
            builder.ConfigureTestServices(services =>
            {
                services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options => options.HttpsPort = 443);
                services.AddSingleton<IStartupFilter>(new OrigenDePrueba(ip ?? IPAddress.Loopback));
                if (falloDisco)
                {
                    var registro = services.Last(x => x.ServiceType == typeof(IAlmacenamientoImagenesProducto));
                    services.Remove(registro);
                    services.AddScoped<IAlmacenamientoImagenesProducto>(provider =>
                    {
                        var interior = (IAlmacenamientoImagenesProducto)(registro.ImplementationInstance
                            ?? registro.ImplementationFactory?.Invoke(provider)
                            ?? ActivatorUtilities.CreateInstance(provider, registro.ImplementationType!));
                        return new SegundaConfirmacionFallida(interior);
                    });
                }
            });
        });

    private static HttpClient Cliente(WebApplicationFactory<Program> app)
    {
        var cliente = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost")
        });
        cliente.DefaultRequestHeaders.Add("X-Resell-Import-Key", Clave);
        cliente.DefaultRequestHeaders.Add("X-Importacion-Confirmada", "true");
        return cliente;
    }

    private static async Task<ProductoInput> InputAsync(WebApplicationFactory<Program> app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<ICategoriaService>()
            .CrearAsync(new CategoriaInput("Perfumes ficticios", null));
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        return new(null, "Perfume de prueba", null, "Marca ficticia", null, null, null,
            50, resultado.Value!.Id, ContenidoMl: 100);
    }

    private static async Task SinProductosAsync(WebApplicationFactory<Program> app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResellManagerDbContext>();
        Assert.Empty(await db.Productos.ToListAsync());
        Assert.Empty(await db.ProductoImagenes.ToListAsync());
        Assert.Empty(await db.UnidadesInventario.ToListAsync());
    }

    private string[] ArchivosImagenes() => Directory.Exists(factory.RutaImagenes)
        ? Directory.GetFiles(factory.RutaImagenes, "*", SearchOption.AllDirectories) : [];

    private static MultipartFormDataContent Formulario(ProductoInput input, params byte[][] imagenes)
    {
        var formulario = new MultipartFormDataContent();
        formulario.Add(JsonContent.Create(input), "producto");
        for (var i = 0; i < imagenes.Length; i++)
        {
            var foto = new ByteArrayContent(imagenes[i]);
            foto.Headers.ContentType = new("image/png");
            formulario.Add(foto, "imagenes", $"prueba-{i}.png");
        }
        return formulario;
    }

    private static byte[] Imagen(SKColor color)
    {
        using var bitmap = new SKBitmap(16, 24);
        bitmap.Erase(color);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(SKEncodedImageFormat.Png, 100);
        return datos.ToArray();
    }

    private sealed class OrigenDePrueba(IPAddress ip) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente) => app =>
        {
            app.Use((contexto, next) =>
            {
                contexto.Connection.RemoteIpAddress = ip;
                return next(contexto);
            });
            siguiente(app);
        };
    }

    private sealed class ContenidoSinLongitud(byte[] contenido) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(contenido).AsTask();
    }

    private sealed class SegundaConfirmacionFallida(IAlmacenamientoImagenesProducto interior) : IAlmacenamientoImagenesProducto
    {
        private int confirmaciones;
        public Task<ServiceResult<ImagenProductoPreparada>> PrepararAsync(Stream contenido, CancellationToken ct = default) =>
            interior.PrepararAsync(contenido, ct);
        public Task<ServiceResult<ImagenProductoPreparada>> PrepararGaleriaAsync(Stream contenido, CancellationToken ct = default) =>
            interior.PrepararGaleriaAsync(contenido, ct);
        public Task<ServiceResult<ImagenProductoGuardada>> ConfirmarAsync(ImagenProductoPreparada preparada, int productoId, CancellationToken ct = default) =>
            ++confirmaciones == 2 ? Task.FromResult(ServiceResult<ImagenProductoGuardada>.Failure("Fallo de disco simulado."))
                : interior.ConfirmarAsync(preparada, productoId, ct);
        public Task<ServiceResult> EliminarTemporalAsync(string id, CancellationToken ct = default) => interior.EliminarTemporalAsync(id, ct);
        public Task<ServiceResult> EliminarAsync(string ruta, CancellationToken ct = default) => interior.EliminarAsync(ruta, ct);
        public Task<ServiceResult<ImagenProductoLectura>> AbrirLecturaAsync(string ruta, CancellationToken ct = default) => interior.AbrirLecturaAsync(ruta, ct);
        public Task<ServiceResult<ImagenProductoLectura>> AbrirMiniaturaAsync(string ruta, CancellationToken ct = default) => interior.AbrirMiniaturaAsync(ruta, ct);
        public Task<ServiceResult<bool>> GenerarMiniaturaAsync(string ruta, CancellationToken ct = default) => interior.GenerarMiniaturaAsync(ruta, ct);
    }
}

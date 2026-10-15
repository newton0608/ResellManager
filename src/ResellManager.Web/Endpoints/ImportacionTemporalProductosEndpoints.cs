using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Diagnostics;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;

namespace ResellManager.Web.Endpoints;

/// <summary>
/// Operación local y temporal. No es parte de la API pública del catálogo.
/// El operador ejecuta solicitudes dentro del contenedor contra localhost:8080.
/// </summary>
public static class ImportacionTemporalProductosEndpoints
{
    public const string Prefijo = "/api/admin/importacion-productos";
    private const int MaximoLote = 100;
    private const long MaximoMetadatos = 256 * 1024;
    private const long MaximoSolicitud = 64L * 1024 * 1024;
    private const long MaximoImagen = 8L * 1024 * 1024;
    private static readonly SemaphoreSlim CreacionLocal = new(1, 1);
    private static readonly JsonSerializerOptions JsonOpciones = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapImportacionTemporalProductos(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup(Prefijo);
        grupo.AddEndpointFilter(async (contexto, siguiente) =>
        {
            var http = contexto.HttpContext;
            var paginasEstado = http.Features.Get<IStatusCodePagesFeature>();
            if (paginasEstado is not null) paginasEstado.Enabled = false;
            http.Response.Headers.CacheControl = "no-store";
            var logger = http.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("ImportacionTemporalProductos");
            object? resultado;
            if (!Autorizado(http, http.RequestServices.GetRequiredService<IConfiguration>()))
                resultado = Results.NotFound();
            else
            {
                var limite = http.Request.Path.Value?.EndsWith("/validar", StringComparison.OrdinalIgnoreCase) == true
                    ? MaximoMetadatos : MaximoSolicitud;
                if (http.Request.ContentLength > limite)
                    resultado = Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                else
                {
                    var maximo = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
                    if (maximo is { IsReadOnly: false }) maximo.MaxRequestBodySize = limite;
                    var original = http.Request.Body;
                    http.Request.Body = new CuerpoLimitado(original, limite);
                    try
                    {
                        resultado = await siguiente(contexto);
                    }
                    catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
                    {
                        resultado = Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                    }
                    finally
                    {
                        http.Request.Body = original;
                    }
                }
            }
            logger.LogInformation("Importación temporal {Ruta}: HTTP {Estado}.",
                http.Request.Path.Value, resultado is IStatusCodeHttpResult estado ? estado.StatusCode : http.Response.StatusCode);
            return resultado;
        });
        grupo.RequireRateLimiting("ImportacionProductos");

        grupo.MapGet("/estado", (IConfiguration configuracion) =>
            Results.Ok(new { habilitada = true, expiraUtc = configuracion["ImportacionProductos:ExpiraUtc"] }));

        grupo.MapPost("/validar", async (HttpContext contexto, ICategoriaService categorias,
            IProductoService productos, CancellationToken ct) =>
        {
            if (contexto.Request.ContentLength is > MaximoMetadatos)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            ProductoInput[]? lote;
            try
            {
                lote = await contexto.Request.ReadFromJsonAsync<ProductoInput[]>(JsonOpciones, ct);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException ||
                ex is BadHttpRequestException { StatusCode: not StatusCodes.Status413PayloadTooLarge })
            {
                return Results.BadRequest(new { error = "JSON de productos inválido." });
            }
            if (lote is null || lote.Length is < 1 or > MaximoLote)
                return Results.BadRequest(new { error = "El lote debe tener entre 1 y 100 productos." });

            var catalogoCategorias = await categorias.ListarAsync(ct);
            var existentes = await productos.ListarAsync(ct);
            var resultados = lote.Select((item, i) =>
            {
                var revision = Revisar(item, catalogoCategorias, existentes);
                var errores = revision.Errores.ToList();
                if (item is not null && lote.Take(i).Any(anterior => anterior is not null &&
                    ((!string.IsNullOrWhiteSpace(item.CodigoBarras) &&
                      string.Equals(anterior.CodigoBarras?.Trim(), item.CodigoBarras.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                     MismaDefinicion(anterior, item))))
                    errores.Add("Producto repetido dentro del lote.");
                return new { indice = i, valido = errores.Count == 0,
                    errores, advertencias = revision.Advertencias };
            }).ToArray();
            return Results.Ok(new { valido = resultados.All(x => x.valido), resultados });
        }).WithMetadata(new RequestSizeLimitAttribute(MaximoMetadatos));

        grupo.MapPost("/crear", async (HttpContext contexto, ICategoriaService categorias,
            IProductoService productos, IProductoConImagenService galeria, CancellationToken ct) =>
        {
            if (!string.Equals(contexto.Request.ContentType?.Split(';')[0], "multipart/form-data",
                    StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "Se requiere multipart/form-data." });
            if (contexto.Request.ContentLength is > MaximoSolicitud)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            if (!string.Equals(contexto.Request.Headers["X-Importacion-Confirmada"], "true", StringComparison.Ordinal))
                return Results.Conflict(new { error = "Revisa /validar y confirma mediante X-Importacion-Confirmada: true." });
            IFormCollection formulario;
            try
            {
                formulario = await contexto.Request.ReadFormAsync(new FormOptions
                {
                    ValueCountLimit = 10, // Ocho archivos y dos campos de metadatos.
                    ValueLengthLimit = (int)MaximoMetadatos,
                    MultipartBodyLengthLimit = MaximoImagen
                }, ct);
            }
            catch (Exception ex) when (ex is InvalidDataException ||
                ex is BadHttpRequestException { StatusCode: not StatusCodes.Status413PayloadTooLarge })
            {
                return Results.BadRequest(new { error = "Formulario de importación inválido." });
            }
            if (formulario.Keys.Any(k => k != "producto" && k != "portadaIndice") ||
                formulario["producto"].Count != 1 || formulario["portadaIndice"].Count > 1)
                return Results.BadRequest(new { error = "Se requiere un único campo producto JSON." });
            if (Encoding.UTF8.GetByteCount(formulario["producto"].ToString()) > MaximoMetadatos)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            ProductoInput? input;
            try
            {
                input = JsonSerializer.Deserialize<ProductoInput>(formulario["producto"].ToString(), JsonOpciones);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "JSON de producto inválido." });
            }
            if (input is null) return Results.BadRequest(new { error = "Producto requerido." });
            var archivos = formulario.Files.ToArray();
            if (archivos.Length > 8 || archivos.Any(f => f.Name != "imagenes" || f.Length <= 0 || f.Length > MaximoImagen))
                return Results.BadRequest(new { error = "Se admiten de cero a ocho imágenes, máximo 8 MiB cada una." });
            var indice = 0;
            if (formulario["portadaIndice"].Count == 1 &&
                !int.TryParse(formulario["portadaIndice"].ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out indice))
                return Results.BadRequest(new { error = "Índice de portada inválido." });
            if (indice < 0 || (archivos.Length == 0 && indice != 0) || (archivos.Length > 0 && indice >= archivos.Length))
                return Results.BadRequest(new { error = "Índice de portada fuera de rango." });

            // Serializar únicamente esta herramienta: revalidar después de esperar evita duplicados
            // entre reintentos concurrentes sin cambiar las reglas del servicio administrativo.
            await CreacionLocal.WaitAsync(ct);
            try
            {
                input = input with { CodigoBarras = input.CodigoBarras?.Trim() };
                var revision = Revisar(input, await categorias.ListarAsync(ct), await productos.ListarAsync(ct));
                if (revision.Errores.Count > 0)
                    return Results.Conflict(new { error = "Producto rechazado.", errores = revision.Errores, advertencias = revision.Advertencias });
                var streams = new List<Stream>();
                try
                {
                    foreach (var archivo in archivos)
                        streams.Add(archivo.OpenReadStream());
                    var resultado = await galeria.CrearGaleriaAsync(input, streams, indice, ct, validarCodigoBarras: true);
                    if (!resultado.IsSuccess || resultado.Value is null)
                        return Results.Conflict(new { error = resultado.ErrorMessage ?? "No se pudo crear el producto." });
                    return Results.Created($"/productos/{resultado.Value.Id}",
                        new { id = resultado.Value.Id, codigoInterno = resultado.Value.CodigoInterno,
                            imagenes = archivos.Length, inventarioCreado = false });
                }
                finally
                {
                    foreach (var stream in streams) await stream.DisposeAsync();
                }
            }
            finally
            {
                CreacionLocal.Release();
            }
        }).WithMetadata(new RequestSizeLimitAttribute(MaximoSolicitud));

        return app;
    }

    public static bool Autorizado(HttpContext contexto, IConfiguration configuracion)
    {
        if (!EsOrigenLocal(contexto)) return false;
        if (!bool.TryParse(configuracion["ImportacionProductos:Habilitada"], out var habilitada) || !habilitada)
            return false;
        var token = configuracion["ImportacionProductos:Token"];
        var proporcionado = contexto.Request.Headers["X-Resell-Import-Key"].ToString();
        if (string.IsNullOrWhiteSpace(token) || token.Length < 32 || proporcionado.Length > 512 ||
            token.Length > 512 ||
            !DateTimeOffset.TryParse(configuracion["ImportacionProductos:ExpiraUtc"],
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expira) ||
            expira <= DateTimeOffset.UtcNow || expira > DateTimeOffset.UtcNow.AddHours(24))
            return false;
        var esperado = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var recibido = SHA256.HashData(Encoding.UTF8.GetBytes(proporcionado));
        return CryptographicOperations.FixedTimeEquals(esperado, recibido);
    }

    public static bool EsSolicitudLocal(HttpContext contexto) =>
        contexto.Request.Path.StartsWithSegments(Prefijo, StringComparison.OrdinalIgnoreCase) && EsOrigenLocal(contexto);

    private static bool EsOrigenLocal(HttpContext contexto)
    {
        var ip = contexto.Connection.RemoteIpAddress;
        var host = contexto.Request.Host.Host;
        // El origen, el Host y la ausencia de cabeceras de proxy deben coincidir.
        return ip is not null && IPAddress.IsLoopback(ip) &&
            (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "[::1]") &&
            !contexto.Request.Headers.ContainsKey("X-Forwarded-For") &&
            !contexto.Request.Headers.ContainsKey("X-Forwarded-Host") &&
            !contexto.Request.Headers.ContainsKey("X-Original-For");
    }

    public static RevisionProducto Revisar(ProductoInput? input,
        IReadOnlyList<CategoriaDto> categorias, IReadOnlyList<ProductoDto> existentes)
    {
        var errores = new List<string>();
        var advertencias = new List<string>();
        if (input is null) return new(["Producto requerido."], []);
        if (string.IsNullOrWhiteSpace(input.Nombre)) errores.Add("Nombre requerido.");
        if (input.Nombre?.Length > 200) errores.Add("Nombre demasiado largo.");
        if (input.PrecioSugerido < 0) errores.Add("El precio sugerido no puede ser negativo.");
        var categoria = categorias.FirstOrDefault(x => x.Id == input.CategoriaId);
        if (categoria is null) errores.Add("Categoría inexistente.");
        else
        {
            var raiz = categoria.CategoriaPadreId.HasValue
                ? categorias.FirstOrDefault(x => x.Id == categoria.CategoriaPadreId) : categoria;
            if (raiz is null || raiz.CategoriaPadreId.HasValue)
                errores.Add("La categoría debe ser una raíz o una subcategoría de una raíz.");
            if (input.CategoriaPrincipalId.HasValue && input.CategoriaPrincipalId != raiz?.Id)
                errores.Add("Categoría principal incompatible.");
        }
        if (!string.IsNullOrWhiteSpace(input.CodigoBarras) &&
            existentes.Any(x => string.Equals(x.CodigoBarras?.Trim(), input.CodigoBarras.Trim(), StringComparison.OrdinalIgnoreCase)))
            errores.Add("El código de barras ya está registrado.");
        if (!string.IsNullOrWhiteSpace(input.Nombre) && existentes.Any(x =>
            MismaDefinicion(new ProductoInput(x.CodigoBarras, x.Nombre, x.Descripcion, x.Marca, x.Modelo,
                x.Color, x.Talla, x.PrecioSugerido, x.CategoriaId, x.ContenidoMl, x.PesoGramos, x.Presentacion), input)))
            errores.Add("Ya existe un producto con el mismo nombre y los mismos datos de variante.");
        else if (!string.IsNullOrWhiteSpace(input.Nombre) && existentes.Any(x =>
            string.Equals(x.Nombre.Trim(), input.Nombre.Trim(), StringComparison.OrdinalIgnoreCase)))
            advertencias.Add("Existe un producto con el mismo nombre; revisa si es una variante.");
        if (input.ContenidoMl is <= 0) errores.Add("El contenido en ml debe ser mayor que cero.");
        if (input.PesoGramos is <= 0) errores.Add("El peso en gramos debe ser mayor que cero.");
        if (input.ContenidoMl.HasValue && input.PesoGramos.HasValue)
            errores.Add("No se permiten ml y gramos simultáneamente.");
        if (input.Presentacion is { Length: > 100 }) errores.Add("La presentación no puede superar los 100 caracteres.");
        return new(errores, advertencias);
    }

    private static bool MismoTexto(string? a, string? b) =>
        string.Equals(a?.Trim() ?? "", b?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);

    private static bool MismaDefinicion(ProductoInput a, ProductoInput b) =>
        MismoTexto(a.Nombre, b.Nombre) && MismoTexto(a.Marca, b.Marca) && MismoTexto(a.Modelo, b.Modelo) &&
        MismoTexto(a.Color, b.Color) && MismoTexto(a.Talla, b.Talla) && MismoTexto(a.Presentacion, b.Presentacion) &&
        a.ContenidoMl == b.ContenidoMl && a.PesoGramos == b.PesoGramos;

    // El límite también se aplica sin Content-Length y con TestServer, antes de leer o
    // almacenar un cuerpo ilimitado. No cierra el stream de la solicitud.
    private sealed class CuerpoLimitado(Stream interior, long maximo) : Stream
    {
        private long leidos;
        public override bool CanRead => interior.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) =>
            Registrar(interior.Read(buffer, offset, (int)Math.Min(count, maximo - leidos + 1)));
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            Registrar(await interior.ReadAsync(buffer[..(int)Math.Min(buffer.Length, maximo - leidos + 1)], ct));
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
        private int Registrar(int cantidad)
        {
            leidos += cantidad;
            if (leidos > maximo)
                throw new BadHttpRequestException("Solicitud demasiado grande.", StatusCodes.Status413PayloadTooLarge);
            return cantidad;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    public sealed record RevisionProducto(IReadOnlyList<string> Errores, IReadOnlyList<string> Advertencias);
}

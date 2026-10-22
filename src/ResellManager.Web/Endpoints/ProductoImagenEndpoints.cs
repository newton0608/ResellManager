using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Net.Http.Headers;
using ResellManager.Application.Common;
using ResellManager.Application.Interfaces;

namespace ResellManager.Web.Endpoints;

public static class ProductoImagenEndpoints
{
    public static IEndpointRouteBuilder MapProductoImagenEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/productos/{productoId:int}/imagen", (int productoId, string? v,
            IProductoService productos, IAlmacenamientoImagenesProducto almacenamiento,
            HttpContext contexto, CancellationToken ct) => LeerAsync(productoId, v, false, productos, almacenamiento, contexto, ct))
            .RequireAuthorization();
        app.MapGet("/productos/{productoId:int}/miniatura", (int productoId, string? v,
            IProductoService productos, IAlmacenamientoImagenesProducto almacenamiento,
            HttpContext contexto, CancellationToken ct) => LeerAsync(productoId, v, true, productos, almacenamiento, contexto, ct))
            .RequireAuthorization();
        return app;
    }

    private static async Task<IResult> LeerAsync(int id, string? version, bool miniatura,
        IProductoService productos, IAlmacenamientoImagenesProducto almacenamiento, HttpContext contexto, CancellationToken ct)
    {
        contexto.Response.Headers.CacheControl = "private, no-store";
        var paginasEstado = contexto.Features.Get<IStatusCodePagesFeature>();
        if (paginasEstado is not null) paginasEstado.Enabled = false;
        var producto = await productos.ObtenerPorIdAsync(id, ct);
        var referencia = producto.Value?.ImagenPrincipalRuta;
        if (!producto.IsSuccess || string.IsNullOrWhiteSpace(referencia)) return Results.NotFound();
        var actual = ImagenProductoVersion.Obtener(referencia);
        // Una URL vieja nunca debe almacenar la portada nueva con la versión anterior.
        if (version is not null && version != actual) return Results.NotFound();
        var resultado = miniatura
            ? await almacenamiento.AbrirMiniaturaAsync(referencia, ct)
            : await almacenamiento.AbrirLecturaAsync(referencia, ct);
        if (!resultado.IsSuccess || resultado.Value is null) return Results.NotFound();
        var imagen = resultado.Value;
        // El fallback heredado no se almacena: la siguiente visita puede tener miniatura.
        if (miniatura && !imagen.EsMiniatura)
            return Results.File(imagen.Contenido, imagen.ContentType);
        contexto.Response.Headers.CacheControl = version is null
            ? "private, no-cache" : "private, max-age=86400";
        var etag = new EntityTagHeaderValue($"\"{actual}-{(miniatura ? "miniatura-v1" : "imagen")}\"");
        return Results.File(imagen.Contenido, imagen.ContentType, entityTag: etag);
    }
}

using ResellManager.Application.Interfaces;

namespace ResellManager.Web.Endpoints;

public static class ProductoGaleriaEndpoints
{
    public static IEndpointRouteBuilder MapProductoGaleriaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/productos/{productoId:int}/imagenes", async (
            int productoId, IProductoConImagenService productos, HttpContext contexto, CancellationToken ct) =>
        {
            var galeria = await productos.ObtenerGaleriaAsync(productoId, ct);
            contexto.Response.Headers.CacheControl = "private, no-store";
            return galeria.IsSuccess ? Results.Ok(galeria.Value) : Results.NotFound();
        }).RequireAuthorization();

        app.MapGet("/productos/{productoId:int}/imagenes/{imagenId:guid}", async (
            int productoId, Guid imagenId, IProductoConImagenService productos, HttpContext contexto, CancellationToken ct) =>
        {
            var imagen = await productos.AbrirImagenAsync(productoId, imagenId, ct);
            if (!imagen.IsSuccess || imagen.Value is null) return Results.NotFound();
            contexto.Response.Headers.CacheControl = "private, no-store";
            contexto.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(imagen.Value.Contenido, imagen.Value.ContentType);
        }).RequireAuthorization();
        return app;
    }
}

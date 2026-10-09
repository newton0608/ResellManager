using Microsoft.AspNetCore.Diagnostics;
using ResellManager.Application.Interfaces;

namespace ResellManager.Web.Endpoints;

public static class CatalogoPublicoEndpoints
{
    public static RouteGroupBuilder MapCatalogoPublico(this IEndpointRouteBuilder endpoints)
    {
        var catalogo = endpoints.MapGroup("/api/catalogo/productos").AllowAnonymous();
        catalogo.AddEndpointFilter(async (contexto, siguiente) =>
        {
            // Los errores de esta API no deben reejecutar una página Blazor privada.
            var paginasEstado = contexto.HttpContext.Features.Get<IStatusCodePagesFeature>();
            if (paginasEstado is not null) paginasEstado.Enabled = false;
            contexto.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await siguiente(contexto); }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "Parámetros de paginación inválidos." });
            }
        });

        catalogo.MapGet("", async (string? termino, int? categoriaId, string? marca,
            ICatalogoPublicoService servicio, CancellationToken ct) =>
            Results.Ok(await servicio.ListarAsync(termino, categoriaId, ct, marca)));

        catalogo.MapGet("/pagina", async (string? termino, int? categoriaId, string? marca,
            int? cursor, int? tamano, ICatalogoPublicoService servicio, CancellationToken ct) =>
            Results.Ok(await servicio.ListarPaginaAsync(termino, categoriaId, marca, cursor, tamano ?? 16, ct)));

        catalogo.MapGet("/escaparate", async (int? cursor, int? tamano,
            ICatalogoPublicoService servicio, CancellationToken ct) =>
            Results.Ok(await servicio.LeerEscaparateAsync(cursor, tamano ?? 3, ct)));

        catalogo.MapGet("/raices", async (int? cursor, int? tamano,
            ICatalogoPublicoService servicio, CancellationToken ct) =>
            Results.Ok(await servicio.LeerRaicesAsync(cursor, tamano ?? 16, ct)));

        catalogo.MapGet("/marcas", async (string? cursor, int? tamano,
            ICatalogoPublicoService servicio, CancellationToken ct) =>
            Results.Ok(await servicio.LeerMarcasAsync(cursor, tamano ?? 16, ct)));

        catalogo.MapGet("/categorias/{categoriaId:int}/contexto", async (int categoriaId,
            ICatalogoPublicoService servicio, CancellationToken ct) =>
        {
            var contexto = await servicio.LeerContextoCategoriaAsync(categoriaId, ct);
            return contexto.IsSuccess && contexto.Value is not null
                ? Results.Ok(contexto.Value) : Results.NotFound();
        });

        catalogo.MapGet("/categorias/{raizId:int}/hijas", async (int raizId, int? cursor, int? tamano,
            ICatalogoPublicoService servicio, CancellationToken ct) =>
            Results.Ok(await servicio.LeerSubcategoriasAsync(raizId, cursor, tamano ?? 16, ct)));

        catalogo.MapGet("/{productoId:int}", async (int productoId,
            ICatalogoPublicoService servicio, CancellationToken ct) =>
        {
            var producto = await servicio.ObtenerPorIdAsync(productoId, ct);
            return producto.IsSuccess && producto.Value is not null
                ? Results.Ok(producto.Value) : Results.NotFound();
        });

        catalogo.MapGet("/{productoId:int}/imagen", async (int productoId,
            ICatalogoPublicoService servicio, CancellationToken ct) =>
        {
            var imagen = await servicio.AbrirImagenPrincipalAsync(productoId, ct);
            return imagen.IsSuccess && imagen.Value is not null
                ? Results.File(imagen.Value.Contenido, imagen.Value.ContentType)
                : Results.NotFound();
        });

        catalogo.MapGet("/{productoId:int}/imagenes/{imagenId}", async (int productoId, string imagenId,
            ICatalogoPublicoService servicio, CancellationToken ct) =>
        {
            var imagen = await servicio.AbrirImagenAsync(productoId, imagenId, ct);
            return imagen.IsSuccess && imagen.Value is not null
                ? Results.File(imagen.Value.Contenido, imagen.Value.ContentType)
                : Results.NotFound();
        });

        return catalogo;
    }
}

using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;

namespace ResellManager.Web.Finanzas;

public static class FinanzasEndpoints
{
    public static IEndpointRouteBuilder MapFinanzas(this IEndpointRouteBuilder app)
    {
        app.MapGet("/informes/{informe}/{formato}", async (HttpContext contexto, string informe, string formato,
            int? cliente, DateOnly? desde, DateOnly? hasta, string? tipo, string? referencia, IFinanzasService finanzas, IVentaDirectaService ventas, CancellationToken ct) =>
        {
            var filtro = new FiltroMovimientos(cliente, desde, hasta, tipo, referencia);
            var resultado = informe == "ventas-cf" ? await ventas.ExportarAsync(formato, filtro, ct) : await finanzas.ExportarAsync(informe, formato, filtro, ct);
            contexto.Response.Headers.CacheControl = "no-store";
            contexto.Response.Headers.XContentTypeOptions = "nosniff";
            return resultado.IsSuccess && resultado.Value is { } archivo
                ? Results.File(archivo.Contenido, archivo.TipoContenido, archivo.Nombre)
                : Results.BadRequest(new { error = resultado.ErrorMessage });
        }).RequireAuthorization();
        app.MapGet("/etiquetas/{id:guid}/{formato}", async (Guid id,string formato,IEtiquetasService etiquetas,HttpContext contexto,CancellationToken ct)=>
        {
            contexto.Response.Headers.CacheControl="no-store";
            var resultado=await etiquetas.ExportarAsync(id,formato,ct);
            return resultado.IsSuccess && resultado.Value is {} archivo ? Results.File(archivo.Contenido,archivo.TipoContenido,archivo.Nombre) : Results.BadRequest(new{error=resultado.ErrorMessage});
        }).RequireAuthorization();
        app.MapGet("/costos/informe/{formato}",async (string formato,DateOnly desde,DateOnly hasta,DateTime? corteUtc,bool? original,ICostosService costos,HttpContext contexto,CancellationToken ct)=>
        {
            contexto.Response.Headers.CacheControl="no-store";
            var resultado=await costos.ExportarAsync(formato,desde,hasta,corteUtc,original??false,ct);
            return resultado.IsSuccess && resultado.Value is {} archivo ? Results.File(archivo.Contenido,archivo.TipoContenido,archivo.Nombre) : Results.BadRequest(new{error=resultado.ErrorMessage});
        }).RequireAuthorization();
        return app;
    }
}

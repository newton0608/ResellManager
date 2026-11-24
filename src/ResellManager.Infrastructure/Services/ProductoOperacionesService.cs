using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
namespace ResellManager.Infrastructure.Services;
public sealed class ProductoOperacionesService(ResellManagerDbContext db,ICuentaOperativa acceso,TimeProvider reloj):IProductoOperacionesService
{
    public async Task<ProductoPaginaDto> ListarAsync(ProductoFiltros filtros,string? termino=null,int? categoria=null,ProductoCursorDto? cursor=null,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is null)throw new UnauthorizedAccessException();
        return await new ProductoService(db).ListarFiltradosAsync(filtros,termino,categoria,cursor,ct);
    }
    public async Task<ServiceResult<ProductoDto>> PrecioAsync(int id,decimal precio,decimal esperado,string clave,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is not {} actor)return ServiceResult<ProductoDto>.Failure("Acceso requerido.");
        if(precio<=0||!FinanzasService.Moneda(precio)||!FinanzasService.ClaveValida(clave))return ServiceResult<ProductoDto>.Failure("Introduce un precio mayor que Q0 con hasta dos decimales.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var huella=FinanzasService.Huella(new{Operacion="precio",id,precio,esperado,actor.UsuarioId});
        var anterior=await db.ComandosFinancieros.AsNoTracking().SingleOrDefaultAsync(x=>x.Clave==clave,ct);
        if(anterior is not null)return anterior.Huella==huella?ServiceResult<ProductoDto>.Ok(JsonSerializer.Deserialize<ProductoDto>(anterior.Resultado)!):ServiceResult<ProductoDto>.Failure("Clave usada con otro contenido.");
        var p=await db.Productos.SingleOrDefaultAsync(x=>x.Id==id,ct);
        if(p is not null) await db.Entry(p).ReloadAsync(ct);
        if(p is null||p.PrecioSugerido!=esperado)return ServiceResult<ProductoDto>.Failure("El producto o precio cambió. Recarga su información antes de guardar.");
        db.CambiosPrecioProducto.Add(new(){Id=Guid.NewGuid(),Producto=p,Anterior=esperado,Nuevo=precio,UsuarioId=actor.UsuarioId,RegistradoUtc=reloj.GetUtcNow().UtcDateTime});
        p.PrecioSugerido=precio;await db.SaveChangesAsync(ct);
        var resultado=await new ProductoService(db).ObtenerPorIdAsync(id,ct);
        db.ComandosFinancieros.Add(new(){Clave=clave,Huella=huella,Resultado=JsonSerializer.Serialize(resultado.Value),UsuarioId=actor.UsuarioId,RegistradoUtc=reloj.GetUtcNow().UtcDateTime});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return resultado;
    }
    public async Task<ServiceResult<IReadOnlyList<GrupoVariantesDto>>> GruposAsync(CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is null)return ServiceResult<IReadOnlyList<GrupoVariantesDto>>.Failure("Acceso requerido.");
        var grupos=await db.GruposVariantes.AsNoTracking().Include(x=>x.Variantes).ThenInclude(x=>x.Producto).ThenInclude(x=>x.UnidadesInventario).ThenInclude(x=>x.DetallesVenta).ThenInclude(x=>x.Venta).OrderBy(x=>x.Nombre).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<GrupoVariantesDto>>.Ok(grupos.Select(Map).ToArray());
    }
    private static GrupoVariantesDto Map(GrupoVariantes g)=>new(g.Id,g.Nombre,g.Variantes.OrderBy(x=>x.Producto.Nombre).ThenBy(x=>x.ProductoId).Select(x=>new VarianteDto(x.ProductoId,x.Producto.Nombre,
        Describir(x.AtributosJson,x.Producto),x.Producto.UnidadesInventario.Any(u=>u.Estado==EstadoUnidadInventario.Disponible&&u.DetallePedidoReservaId==null&&!u.DetallesVenta.Any(d=>d.Venta.Estado==EstadoVenta.Registrada)),x.Producto.PrecioSugerido)).ToArray());
    private static SortedDictionary<string,string> Combinar(IReadOnlyDictionary<string,string> extras,string? color,string? talla,string? presentacion,decimal? ml,decimal? gramos)
    {
        var valores=new SortedDictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var a in extras)valores[a.Key]=a.Value;
        if(!string.IsNullOrWhiteSpace(color))valores["Color"]=color;
        if(!string.IsNullOrWhiteSpace(talla))valores["Talla"]=talla;
        if(!string.IsNullOrWhiteSpace(presentacion))valores["Presentación"]=presentacion;
        if(ml.HasValue)valores["Contenido ml"]=ml.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if(gramos.HasValue)valores["Peso g"]=gramos.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return valores;
    }
    private static string Firma(SortedDictionary<string,string> valores)=>FinanzasService.Huella(valores.Select(x=>new[]{x.Key.ToUpperInvariant(),x.Value.ToUpperInvariant()}));
    internal static string Describir(string atributos,Producto producto)=>string.Join(" · ",Combinar(JsonSerializer.Deserialize<Dictionary<string,string>>(atributos)!,producto.Color,producto.Talla,producto.Presentacion,producto.ContenidoMl,producto.PesoGramos).Select(x=>$"{x.Key}: {x.Value}"));
    internal static async Task<string?> ActualizarFirmaAsync(ResellManagerDbContext db,int productoId,ProductoInput input,CancellationToken ct)
    {
        var vinculo=await db.VinculosVariantes.SingleOrDefaultAsync(x=>x.ProductoId==productoId,ct);if(vinculo is null)return null;
        var valores=Combinar(JsonSerializer.Deserialize<Dictionary<string,string>>(vinculo.AtributosJson)!,input.Color,input.Talla,input.Presentacion,input.ContenidoMl,input.PesoGramos);
        var firma=Firma(valores);
        if(valores.Count==0||await db.VinculosVariantes.AnyAsync(x=>x.GrupoId==vinculo.GrupoId&&x.ProductoId!=productoId&&x.Firma==firma,ct))return "Los nuevos atributos no distinguen esta variante dentro del grupo.";
        vinculo.Firma=firma;return null;
    }
    public async Task<ServiceResult<GrupoVariantesDto>> VincularAsync(int productoId,Guid? grupoId,string nombreGrupo,IReadOnlyDictionary<string,string> atributos,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is null)return ServiceResult<GrupoVariantesDto>.Failure("Acceso requerido.");
        if(atributos.Count>12||atributos.Any(x=>string.IsNullOrWhiteSpace(x.Key)||string.IsNullOrWhiteSpace(x.Value)||x.Key.Length>50||x.Value.Length>100))return ServiceResult<GrupoVariantesDto>.Failure("Atributos inválidos (máximo 12).");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var p=await db.Productos.SingleOrDefaultAsync(x=>x.Id==productoId,ct);if(p is null)return ServiceResult<GrupoVariantesDto>.Failure("Producto no encontrado.");
        var vinculo=await db.VinculosVariantes.SingleOrDefaultAsync(x=>x.ProductoId==productoId,ct);
        if(vinculo is not null&&vinculo.GrupoId!=grupoId)return ServiceResult<GrupoVariantesDto>.Failure("Desvincula explícitamente el producto antes de moverlo a otro grupo.");
        var extras=new SortedDictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var a in atributos)
        {
            if(!extras.TryAdd(a.Key.Trim(),a.Value.Trim()))return ServiceResult<GrupoVariantesDto>.Failure("Atributo duplicado.");
        }
        foreach(var maestro in new[]{("Color",p.Color),("Talla",p.Talla),("Presentación",p.Presentacion)})
        {
            if(extras.TryGetValue(maestro.Item1,out var indicado))
            {
                if(string.IsNullOrWhiteSpace(maestro.Item2)||!string.Equals(indicado,maestro.Item2,StringComparison.OrdinalIgnoreCase))
                    return ServiceResult<GrupoVariantesDto>.Failure("Registra ese atributo en el campo maestro del producto antes de vincularlo.");
                extras.Remove(maestro.Item1);
            }
        }
        var valores=Combinar(extras,p.Color,p.Talla,p.Presentacion,p.ContenidoMl,p.PesoGramos);
        if(valores.Count==0)return ServiceResult<GrupoVariantesDto>.Failure("Indica al menos un atributo que distinga esta variante.");
        var firma=Firma(valores);
        GrupoVariantes? grupo;
        if(grupoId.HasValue)grupo=await db.GruposVariantes.SingleOrDefaultAsync(x=>x.Id==grupoId,ct);
        else
        {
            if(string.IsNullOrWhiteSpace(nombreGrupo)||nombreGrupo.Length>150)return ServiceResult<GrupoVariantesDto>.Failure("Nombre de grupo obligatorio.");
            grupo=new(){Id=Guid.NewGuid(),Nombre=nombreGrupo.Trim()};db.GruposVariantes.Add(grupo);
        }
        if(grupo is null)return ServiceResult<GrupoVariantesDto>.Failure("Grupo no encontrado.");
        if(await db.VinculosVariantes.AnyAsync(x=>x.GrupoId==grupo.Id&&x.Firma==firma&&x.ProductoId!=productoId,ct))return ServiceResult<GrupoVariantesDto>.Failure("Otra variante tiene exactamente esos atributos. Añade un diferenciador.");
        if(vinculo is null){vinculo=new(){Producto=p,Grupo=grupo};db.VinculosVariantes.Add(vinculo);}
        vinculo.AtributosJson=JsonSerializer.Serialize(extras);vinculo.Firma=firma;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        var lista=await GruposAsync(ct);return ServiceResult<GrupoVariantesDto>.Ok(lista.Value!.Single(x=>x.Id==grupo.Id));
    }
    public async Task<ServiceResult> DesvincularAsync(int productoId,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is null)return ServiceResult.Failure("Acceso requerido.");
        var vinculo=await db.VinculosVariantes.SingleOrDefaultAsync(x=>x.ProductoId==productoId,ct);
        if(vinculo is not null){db.VinculosVariantes.Remove(vinculo);await db.SaveChangesAsync(ct);}return ServiceResult.Ok();
    }
}

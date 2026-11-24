using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;
using ResellManager.Infrastructure.Storage;
namespace ResellManager.Infrastructure.Services;
public sealed class CostosService(ResellManagerDbContext db,ICuentaOperativa acceso,TimeProvider reloj,IOptions<AlmacenamientoComprobantesOptions>? almacenamiento=null):ICostosService
{
    public async Task<ServiceResult<NotaCostoDto>> CorregirAsync(NotaCostoInput input,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is not {Administrador:true} actor)return ServiceResult<NotaCostoDto>.Failure("Autorización administrativa requerida para costos.");
        if(input.CostoReal<0||!FinanzasService.Moneda(input.CostoReal)||string.IsNullOrWhiteSpace(input.Motivo)||input.Motivo.Length>1000||input.Referencia?.Length>200
            ||!FinanzasService.ClaveValida(input.Clave)||input.FechaConocimiento>FinanzasService.Hoy(reloj))return ServiceResult<NotaCostoDto>.Failure("Costo real, fecha o motivo inválidos. No se admiten estimaciones.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var huella=FinanzasService.Huella(new{Operacion="costo-real",input,actor.UsuarioId});var previo=await db.ComandosFinancieros.AsNoTracking().SingleOrDefaultAsync(x=>x.Clave==input.Clave,ct);
        if(previo is not null)return previo.Huella==huella?ServiceResult<NotaCostoDto>.Ok(JsonSerializer.Deserialize<NotaCostoDto>(previo.Resultado)!):ServiceResult<NotaCostoDto>.Failure("Clave usada con otro contenido.");
        var unidad=await db.UnidadesInventario.SingleOrDefaultAsync(x=>x.Id==input.UnidadId,ct);if(unidad is null)return ServiceResult<NotaCostoDto>.Failure("Unidad no encontrada.");
        var ultima=await db.NotasCosto.AsNoTracking().Where(x=>x.UnidadId==unidad.Id).OrderByDescending(x=>x.RegistradoUtc).FirstOrDefaultAsync(ct);
        var ahora=reloj.GetUtcNow().UtcDateTime;if(ultima is not null&&ahora<=ultima.RegistradoUtc)ahora=ultima.RegistradoUtc.AddTicks(1);
        var nota=new NotaCosto{Id=Guid.NewGuid(),UnidadId=unidad.Id,Anterior=ultima?.Nuevo??(unidad.CostoConocido?unidad.Costo:null),Nuevo=input.CostoReal,
            FechaConocimiento=input.FechaConocimiento,RegistradoUtc=ahora,UsuarioId=actor.UsuarioId,Motivo=input.Motivo.Trim(),Referencia=input.Referencia?.Trim()};
        db.NotasCosto.Add(nota);
        var detalles=await db.DetallesVenta.Where(x=>x.UnidadInventarioId==unidad.Id).Select(x=>x.Id).ToListAsync(ct);
        foreach(var id in detalles)db.CorreccionesDetalleCosto.Add(new(){Nota=nota,DetalleVentaId=id});
        var resultado=Map(nota);Guardar(input.Clave,huella,actor,resultado,ahora);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return ServiceResult<NotaCostoDto>.Ok(resultado);
    }
    private static NotaCostoDto Map(NotaCosto n)=>new(n.Id,n.UnidadId,n.Anterior,n.Nuevo,n.FechaConocimiento,n.RegistradoUtc,n.UsuarioId,n.Motivo,n.Referencia);
    public async Task<ServiceResult<IReadOnlyList<NotaCostoDto>>> HistorialAsync(int unidadId,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is null)return ServiceResult<IReadOnlyList<NotaCostoDto>>.Failure("Acceso requerido.");
        var notas=await db.NotasCosto.AsNoTracking().Where(x=>x.UnidadId==unidadId).OrderBy(x=>x.RegistradoUtc).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<NotaCostoDto>>.Ok(notas.Select(Map).ToArray());
    }
    public async Task<ServiceResult<ResumenUtilidadDto>> UtilidadAsync(DateOnly desde,DateOnly hasta,DateTime? corteUtc=null,bool original=false,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is null)return ServiceResult<ResumenUtilidadDto>.Failure("Acceso requerido.");
        if(desde>hasta||corteUtc is {Kind:DateTimeKind.Local})return ServiceResult<ResumenUtilidadDto>.Failure("Periodo o corte UTC inválidos.");
        return ServiceResult<ResumenUtilidadDto>.Ok(await LeerUtilidadAsync(db,desde,hasta,corteUtc??reloj.GetUtcNow().UtcDateTime,original,ct));
    }
    internal static async Task<ResumenUtilidadDto> LeerUtilidadAsync(ResellManagerDbContext db,DateOnly desde,DateOnly hasta,DateTime corte,bool original,CancellationToken ct)
    {
        var detalles=await db.DetallesVenta.AsNoTracking().Where(x=>x.Venta.Fecha>=desde&&x.Venta.Fecha<=hasta
            &&(x.Venta.RegistradaUtc==null||x.Venta.RegistradaUtc<=corte)
            &&(x.Venta.Estado==EstadoVenta.Registrada||(x.Venta.CanceladaUtc!=null&&x.Venta.CanceladaUtc>corte)))
            .Select(x=>new{x.Id,x.VentaId,x.Venta.CodigoInterno,x.Venta.Fecha,x.PrecioFinal,x.CostoUnitario,x.CostoConocido}).ToListAsync(ct);
        var ids=detalles.Select(x=>x.Id).ToArray();
        var notas=original?[]:await db.CorreccionesDetalleCosto.AsNoTracking().Where(x=>ids.Contains(x.DetalleVentaId)&&x.Nota.RegistradoUtc<=corte)
            .Include(x=>x.Nota).OrderByDescending(x=>x.Nota.RegistradoUtc).ToListAsync(ct);
        var ultimas=notas.GroupBy(x=>x.DetalleVentaId).ToDictionary(x=>x.Key,x=>x.First().Nota);
        var filas=detalles.Select(x=>
        {
            ultimas.TryGetValue(x.Id,out var nota);var costoOriginal=x.CostoConocido?x.CostoUnitario:null;
            return new LineaUtilidadDto(x.VentaId,x.CodigoInterno,x.Fecha,x.Id,x.PrecioFinal,costoOriginal,nota?.Nuevo??costoOriginal,nota?.Id,nota?.UsuarioId,nota?.Motivo,nota?.RegistradoUtc);
        }).ToArray();
        return new(filas.Where(x=>x.CostoRevisado.HasValue).Sum(x=>x.Precio-x.CostoRevisado!.Value),filas.Count(x=>!x.CostoRevisado.HasValue),
            filas.Where(x=>!x.CostoRevisado.HasValue).Sum(x=>x.Precio),filas.Count(x=>x.NotaId.HasValue),corte,original,filas);
    }
    public async Task<ServiceResult<ArchivoInformeDto>> ExportarAsync(string formato,DateOnly desde,DateOnly hasta,DateTime? corteUtc=null,bool original=false,CancellationToken ct=default)
    {
        if(formato is not("xlsx" or "pdf"))return ServiceResult<ArchivoInformeDto>.Failure("Formato inválido.");
        var r=await UtilidadAsync(desde,hasta,corteUtc,original,ct);if(!r.IsSuccess)return ServiceResult<ArchivoInformeDto>.Failure(r.ErrorMessage!);var resumen=r.Value!;
        var filas=resumen.Lineas.Select(x=>new object?[]{x.Fecha,x.Venta,x.DetalleId,x.Precio,x.CostoOriginal is {} o?o:"Costo no registrado",
            x.CostoRevisado is {} v?v:"Costo no registrado",x.CostoRevisado is {} costo?x.Precio-costo:"No calculable",x.NotaId,x.UsuarioId,x.Motivo,x.FechaCorreccion}).ToList();
        filas.Add(["","Utilidad calculable", "",resumen.UtilidadCalculable]);filas.Add(["","Líneas sin costo","",resumen.LineasSinCosto]);filas.Add(["","Ventas sin costo","",resumen.VentasSinCosto]);
        return ServiceResult<ArchivoInformeDto>.Ok(InformeArchivos.Crear("utilidad",formato,["Fecha venta","Venta","Línea","Venta Q","Costo original Q","Costo revisado Q","Utilidad Q","Nota","Responsable","Motivo","Fecha de corrección UTC"],filas,reloj.GetUtcNow().UtcDateTime,
            $"Periodo {desde:yyyy-MM-dd} a {hasta:yyyy-MM-dd}; corte UTC {resumen.CorteUtc:O}; {(original?"Versión original":"Versión revisada")}; correcciones {resumen.Correcciones}.", resumen.CorteUtc));
    }
    internal static async Task<(bool Conocido,decimal Valor)> CostoActualAsync(ResellManagerDbContext db,UnidadInventario u,CancellationToken ct)
    {
        var n=await db.NotasCosto.AsNoTracking().Where(x=>x.UnidadId==u.Id).OrderByDescending(x=>x.RegistradoUtc).Select(x=>(decimal?)x.Nuevo).FirstOrDefaultAsync(ct);
        return(n.HasValue||u.CostoConocido,n??u.Costo);
    }
    public async Task<ServiceResult<Guid>> MigrarHistoricosAsync(IReadOnlyList<int> unidades,string motivo,string clave,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is not{Administrador:true} actor)return ServiceResult<Guid>.Failure("Acceso administrativo requerido.");
        if(unidades.Count is <1 or >10000||unidades.Distinct().Count()!=unidades.Count||string.IsNullOrWhiteSpace(motivo)||motivo.Length>1000||!FinanzasService.ClaveValida(clave))return ServiceResult<Guid>.Failure("Alcance, motivo o clave inválidos.");
        var huella=FinanzasService.Huella(new{Operacion="costos-historicos",Unidades=unidades.Order().ToArray(),motivo,actor.UsuarioId});
        var previo=await db.ComandosFinancieros.AsNoTracking().SingleOrDefaultAsync(x=>x.Clave==clave,ct);
        if(previo is not null)return previo.Huella==huella?ServiceResult<Guid>.Ok(JsonSerializer.Deserialize<Guid>(previo.Resultado)):ServiceResult<Guid>.Failure("Clave usada con otro contenido.");
        // SQLite crea una copia coherente antes de modificar los datos del alcance explícito.
        var carpeta=Path.Combine(almacenamiento?.Value.DirectorioBase??Path.Combine(AppContext.BaseDirectory,"App_Data"),"respaldos-costos");Directory.CreateDirectory(carpeta);
        var ruta=Path.Combine(carpeta,Guid.NewGuid().ToString("N")+".db");
        await db.Database.OpenConnectionAsync(ct);
        using(var copia=new SqliteConnection("Data Source="+ruta+";Pooling=False")){await copia.OpenAsync(ct);((SqliteConnection)db.Database.GetDbConnection()).BackupDatabase(copia);
            using var comprobar=copia.CreateCommand();comprobar.CommandText="PRAGMA quick_check";
            if((await comprobar.ExecuteScalarAsync(ct))?.ToString()!="ok")throw new InvalidOperationException("El respaldo de costos no pasó quick_check.");
        }
        await using var respaldo=File.OpenRead(ruta);var sha256=Convert.ToHexString(await SHA256.HashDataAsync(respaldo,ct));
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        previo=await db.ComandosFinancieros.AsNoTracking().SingleOrDefaultAsync(x=>x.Clave==clave,ct);
        if(previo is not null)return previo.Huella==huella?ServiceResult<Guid>.Ok(JsonSerializer.Deserialize<Guid>(previo.Resultado)):ServiceResult<Guid>.Failure("Clave usada con otro contenido.");
        var alcance=await db.UnidadesInventario.Include(x=>x.DetalleCompra).Where(x=>unidades.Contains(x.Id)).ToListAsync(ct);
        if(alcance.Count!=unidades.Count||alcance.Any(x=>!x.CostoConocido||x.Costo!=0||x.DetalleCompra.CostoUnitario!=0||x.DetalleCompra.CostoUnitarioMonedaOrigen!=0)
            ||await db.NotasCosto.AnyAsync(x=>unidades.Contains(x.UnidadId),ct))return ServiceResult<Guid>.Failure("El alcance contiene registros ambiguos, corregidos o no equivalentes a Q0 histórico.");
        var compras=alcance.Select(x=>x.DetalleCompraId).Distinct().ToArray();
        if(await db.UnidadesInventario.AnyAsync(x=>compras.Contains(x.DetalleCompraId)&&!unidades.Contains(x.Id),ct))return ServiceResult<Guid>.Failure("El alcance debe incluir todas las unidades de cada detalle de compra afectado.");
        var ventas=await db.DetallesVenta.Where(x=>x.UnidadInventarioId!=null&&unidades.Contains(x.UnidadInventarioId.Value)).ToListAsync(ct);
        if(ventas.Any(x=>x.CostoUnitario is not(0 or null)))return ServiceResult<Guid>.Failure("Hay snapshots con costo distinto: revisa la discrepancia.");
        foreach(var u in alcance){u.CostoConocido=false;u.DetalleCompra.CostoConocido=false;}foreach(var d in ventas)d.CostoConocido=false;
        var id=Guid.NewGuid();var ahora=reloj.GetUtcNow().UtcDateTime;
        db.MigracionesCostosHistoricos.Add(new(){Id=id,AlcanceJson=JsonSerializer.Serialize(new{Unidades=unidades,DetallesCompra=compras,DetallesVenta=ventas.Select(x=>x.Id).ToArray(),Anterior="Q0 declarado desconocido",Nuevo="Costo no registrado",RespaldoSha256=sha256}),Respaldo=ruta,Motivo=motivo.Trim(),UsuarioId=actor.UsuarioId,RegistradoUtc=ahora});
        Guardar(clave,huella,actor,id,ahora);await db.SaveChangesAsync(ct);
        if(await db.UnidadesInventario.AsNoTracking().CountAsync(x=>unidades.Contains(x.Id)&&!x.CostoConocido,ct)!=unidades.Count)
            throw new InvalidOperationException("La verificación posterior del alcance no coincide; se revierte la migración.");
        await tx.CommitAsync(ct);return ServiceResult<Guid>.Ok(id);
    }
    private void Guardar<T>(string clave,string huella,CuentaOperativa actor,T resultado,DateTime ahora)=>db.ComandosFinancieros.Add(new(){Clave=clave,Huella=huella,Resultado=JsonSerializer.Serialize(resultado),UsuarioId=actor.UsuarioId,RegistradoUtc=ahora});
}

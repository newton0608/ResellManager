using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Infrastructure.Services;

public sealed class VentaDirectaService(ResellManagerDbContext db, ICuentaOperativa acceso, TimeProvider reloj) : IVentaDirectaService
{
    public async Task<ServiceResult<RevisionVentaDirectaDto>> RevisarAsync(VentaDirectaInput input, CancellationToken ct = default)
    {
        if (await acceso.ObtenerAsync(ct) is null) return ServiceResult<RevisionVentaDirectaDto>.Failure("Inicia sesión para vender.");
        var error = await ValidarAsync(input, ct);
        if(error is not null) return ServiceResult<RevisionVentaDirectaDto>.Failure(error);
        var total=input.Detalles.Sum(x=>x.PrecioFinal); var aplicado=input.Cobros.Sum(x=>x.Aplicado); var entregado=input.Cobros.Sum(x=>x.Entregado);
        var saldo=input.ClienteId.HasValue ? await SaldoConsultas.CalcularAsync(db,input.ClienteId,ct)+total : (decimal?)null;
        return ServiceResult<RevisionVentaDirectaDto>.Ok(new(total,aplicado,entregado,entregado-aplicado,saldo));
    }

    private async Task<string?> ValidarAsync(VentaDirectaInput input,CancellationToken ct)
    {
        if(!FinanzasService.ClaveValida(input.Clave) || input.Detalles.Count is <1 or >1000 || input.Nombre?.Length>100 || input.Apellido?.Length>100 || input.Observaciones?.Length>500)
            return "Datos de venta inválidos.";
        if(input.Detalles.Any(x=>!x.UnidadInventarioId.HasValue || x.PrecioFinal<0 || !FinanzasService.Moneda(x.PrecioFinal))) return "Unidades o importes inválidos.";
        var ids=input.Detalles.Select(x=>x.UnidadInventarioId!.Value).ToArray();
        if(ids.Distinct().Count()!=ids.Length) return "No puede venderse dos veces una misma unidad.";
        var elegibles=await db.UnidadesInventario.AsNoTracking().CountAsync(x=>ids.Contains(x.Id)
            && x.Estado==EstadoUnidadInventario.Disponible && x.DetallePedidoReservaId==null
            && !x.DetallesVenta.Any(d=>d.Venta.Estado==EstadoVenta.Registrada),ct);
        if(elegibles!=ids.Length) return "Una o más unidades ya no están disponibles o recibieron una reserva. Revisa los artículos.";
        if(input.ClienteId.HasValue)
        {
            if(!await db.Clientes.AnyAsync(x=>x.Id==input.ClienteId,ct)) return "Cliente no encontrado.";
            if(input.Cobros.Count!=0) return "Los abonos del cliente registrado se registran por separado.";
        }
        else
        {
            var total=input.Detalles.Sum(x=>x.PrecioFinal);
            if(total<=0 || input.Cobros.Count is <1 or >10) return "CF requiere cobro completo y al menos un medio de pago.";
            foreach(var c in input.Cobros)
                if(!Enum.IsDefined(c.Metodo) || c.Aplicado<=0 || c.Entregado<c.Aplicado || !FinanzasService.Moneda(c.Aplicado)
                    || !FinanzasService.Moneda(c.Entregado) || c.Referencia?.Length>150
                    || (c.Metodo!=MetodoPago.Efectivo && c.Entregado!=c.Aplicado)) return "Cobro inválido. Solo el efectivo admite cambio.";
            if(input.Cobros.Sum(x=>x.Aplicado)!=total) return "Los cobros aplicados deben cubrir exactamente el total de CF.";
        }
        return null;
    }

    public async Task<ServiceResult<VentaDto>> RegistrarAsync(VentaDirectaInput input,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is not {} actor) return ServiceResult<VentaDto>.Failure("Inicia sesión para vender.");
        if(!FinanzasService.ClaveValida(input.Clave)) return ServiceResult<VentaDto>.Failure("Clave de operación inválida.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var huella=FinanzasService.Huella(new{Operacion="venta-directa",input,actor.UsuarioId});
        var previo=await db.ComandosFinancieros.AsNoTracking().SingleOrDefaultAsync(x=>x.Clave==input.Clave,ct);
        if(previo is not null) return previo.Huella==huella ? ServiceResult<VentaDto>.Ok(JsonSerializer.Deserialize<VentaDto>(previo.Resultado)!)
            : ServiceResult<VentaDto>.Failure("La clave ya se utilizó con otro contenido.");
        var error=await ValidarAsync(input,ct); if(error is not null) return ServiceResult<VentaDto>.Failure(error);
        var ids=input.Detalles.Select(x=>x.UnidadInventarioId!.Value).ToArray();
        var unidades=await db.UnidadesInventario.Where(x=>ids.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct);
        var pedido=new Pedido { CodigoInterno="PED-VD-"+Guid.NewGuid().ToString("N").ToUpperInvariant(), Fecha=input.Fecha,
            TipoPedido=TipoPedido.VentaDirecta,CanalVenta=CanalVenta.Presencial,Estado=EstadoPedido.Completado,ClienteId=input.ClienteId,
            Observaciones=input.Observaciones?.Trim() };
        foreach(var g in input.Detalles.GroupBy(x=>unidades[x.UnidadInventarioId!.Value].ProductoId))
            pedido.Detalles.Add(new DetallePedido{ProductoId=g.Key,Cantidad=g.Count(),PrecioUnitario=g.First().PrecioFinal});
        var venta=new Venta { Pedido=pedido,CodigoInterno="VEN-VD-"+Guid.NewGuid().ToString("N").ToUpperInvariant(),Fecha=input.Fecha,Estado=EstadoVenta.Registrada,
            NombreOcasional=input.ClienteId.HasValue?null:input.Nombre?.Trim(),ApellidoOcasional=input.ClienteId.HasValue?null:input.Apellido?.Trim(),
            UsuarioId=actor.UsuarioId,RegistradaUtc=reloj.GetUtcNow().UtcDateTime,Observaciones=input.Observaciones?.Trim() };
        foreach(var item in input.Detalles)
        {
            var u=unidades[item.UnidadInventarioId!.Value]; var costo=await CostosService.CostoActualAsync(db,u,ct); u.Estado=EstadoUnidadInventario.Vendida;
            venta.Detalles.Add(new DetalleVenta{UnidadInventarioId=u.Id,ProductoId=u.ProductoId,CostoUnitario=costo.Valor,CostoConocido=costo.Conocido,PrecioFinal=item.PrecioFinal,Observaciones=item.Observaciones});
        }
        db.Ventas.Add(venta);
        foreach(var c in input.Cobros) db.CobrosVenta.Add(new(){Id=Guid.NewGuid(),Venta=venta,Metodo=c.Metodo,Aplicado=c.Aplicado,Entregado=c.Entregado,
            Referencia=c.Referencia?.Trim(),UsuarioId=actor.UsuarioId,RegistradoUtc=reloj.GetUtcNow().UtcDateTime});
        await db.SaveChangesAsync(ct);
        var resultado=await new VentaService(db).ObtenerPorIdAsync(venta.Id,ct);
        if(!resultado.IsSuccess || resultado.Value is null) throw new InvalidOperationException("No fue posible recuperar la venta registrada.");
        Guardar(input.Clave,huella,actor,resultado.Value); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return resultado;
    }

    public Task<ServiceResult<AnulacionCFDto>> SolicitarAnulacionAsync(int ventaId,string motivo,string clave,CancellationToken ct=default)
        => EjecutarAsync(clave,new{Operacion="solicitar-cf",ventaId,motivo},false,async actor=>
        {
            if(string.IsNullOrWhiteSpace(motivo)||motivo.Length>1000) return (null,"Motivo obligatorio de hasta 1000 caracteres.");
            var venta=await db.Ventas.Include(x=>x.Pedido).Include(x=>x.Detalles).SingleOrDefaultAsync(x=>x.Id==ventaId,ct);
            if(venta is null||venta.Pedido.ClienteId is not null||venta.Estado!=EstadoVenta.Registrada) return (null,"Se requiere una venta CF vigente.");
            if(await db.AnulacionesCF.AnyAsync(x=>x.VentaId==ventaId&&(x.Estado==EstadoAnulacionCF.Pendiente||x.Estado==EstadoAnulacionCF.Aprobada),ct)) return (null,"Ya existe una solicitud activa para esta venta.");
            var a=new AnulacionCF{Id=Guid.NewGuid(),Venta=venta,Estado=EstadoAnulacionCF.Pendiente,Motivo=motivo.Trim(),SolicitanteId=actor.UsuarioId,SolicitadaUtc=reloj.GetUtcNow().UtcDateTime};
            db.AnulacionesCF.Add(a); return (a,null);
        },ct);

    public Task<ServiceResult<AnulacionCFDto>> DecidirAsync(Guid id,bool aprobar,string motivo,string clave,CancellationToken ct=default)
        => EjecutarAsync(clave,new{Operacion="decidir-cf",id,aprobar,motivo},true,async actor=>
        {
            var a=await CargarAsync(id,ct);
            if(a is null||a.Estado!=EstadoAnulacionCF.Pendiente) return (null,"La solicitud no está pendiente.");
            if(string.IsNullOrWhiteSpace(motivo)||motivo.Length>1000) return (null,"Motivo de decisión obligatorio.");
            a.Estado=aprobar?EstadoAnulacionCF.Aprobada:EstadoAnulacionCF.Rechazada;
            a.AdministradorId=actor.UsuarioId;a.DecididaUtc=reloj.GetUtcNow().UtcDateTime;a.MotivoDecision=motivo.Trim();return(a,null);
        },ct);

    public Task<ServiceResult<AnulacionCFDto>> RegistrarDevolucionAsync(DevolucionCFInput input,CancellationToken ct=default)
        => EjecutarAsync(input.Clave,new{Operacion="reembolso-cf",input},true,async actor=>
        {
            var a=await CargarAsync(input.AnulacionId,ct);
            if(a is null||a.Estado!=EstadoAnulacionCF.Aprobada) return(null,"La anulación debe estar aprobada.");
            var pendiente=a.Venta.Detalles.Sum(x=>x.PrecioFinal)-a.Reembolsos.Sum(x=>x.Importe);
            if(input.Importe<=0||input.Importe>pendiente||!FinanzasService.Moneda(input.Importe)||!Enum.IsDefined(input.Metodo)
                ||string.IsNullOrWhiteSpace(input.Referencia)||input.Referencia.Length>200||input.Fecha>FinanzasService.Hoy(reloj)
                ||input.Fecha<DateOnly.FromDateTime(a.SolicitadaUtc.AddHours(-6))) return(null,"Devolución inválida o superior al importe pendiente.");
            db.DevolucionesDineroCF.Add(new(){Anulacion=a,Id=Guid.NewGuid(),Importe=input.Importe,Metodo=input.Metodo,Referencia=input.Referencia.Trim(),FechaEfectiva=input.Fecha,
                UsuarioId=actor.UsuarioId,RegistradoUtc=reloj.GetUtcNow().UtcDateTime});return(a,null);
        },ct);

    public Task<ServiceResult<AnulacionCFDto>> RecibirAsync(Guid id,int unidadId,bool apta,string observacion,string clave,CancellationToken ct=default)
        => EjecutarAsync(clave,new{Operacion="recibir-cf",id,unidadId,apta,observacion},true,async actor=>
        {
            var a=await CargarAsync(id,ct);
            if(a is null||a.Estado!=EstadoAnulacionCF.Aprobada) return(null,"La anulación debe estar aprobada.");
            if(string.IsNullOrWhiteSpace(observacion)||observacion.Length>1000) return(null,"Registra la condición y observación del producto.");
            var u=a.Venta.Detalles.SingleOrDefault(x=>x.UnidadInventarioId==unidadId)?.UnidadInventario;
            if(u is null||a.Recepciones.Any(x=>x.UnidadId==unidadId)||u.Estado is not (EstadoUnidadInventario.Vendida or EstadoUnidadInventario.Entregada)) return(null,"La unidad no pertenece a esta devolución o ya fue recibida.");
            db.RecepcionesDevolucionCF.Add(new(){Anulacion=a,Id=Guid.NewGuid(),Unidad=u,Apta=apta,Observacion=observacion.Trim(),UsuarioId=actor.UsuarioId,RegistradoUtc=reloj.GetUtcNow().UtcDateTime});
            u.Estado=EstadoUnidadInventario.DevueltaEnRevision;return(a,null);
        },ct);

    public Task<ServiceResult<AnulacionCFDto>> CompletarAsync(Guid id,string clave,CancellationToken ct=default)
        => EjecutarAsync(clave,new{Operacion="completar-cf",id},true,async actor=>
        {
            var a=await CargarAsync(id,ct);
            if(a is null||a.Estado!=EstadoAnulacionCF.Aprobada) return(null,"La anulación no está aprobada o ya fue completada.");
            if(a.Reembolsos.Sum(x=>x.Importe)!=a.Venta.Detalles.Sum(x=>x.PrecioFinal)||a.Recepciones.Count!=a.Venta.Detalles.Count)
                return(null,"Falta registrar el dinero realmente devuelto o recibir todos los artículos.");
            foreach(var r in a.Recepciones) r.Unidad.Estado=r.Apta?EstadoUnidadInventario.Disponible:EstadoUnidadInventario.NoApta;
            a.Venta.Estado=EstadoVenta.Cancelada;a.Venta.CanceladaUtc=reloj.GetUtcNow().UtcDateTime;a.Venta.CanceladaPor=actor.UsuarioId;a.Venta.Pedido.Estado=EstadoPedido.Cancelado;
            a.Estado=EstadoAnulacionCF.Completada;a.CompletadaUtc=reloj.GetUtcNow().UtcDateTime;return(a,null);
        },ct);

    private async Task<ServiceResult<AnulacionCFDto>> EjecutarAsync(string clave,object contenido,bool admin,
        Func<CuentaOperativa,Task<(AnulacionCF? Anulacion,string? Error)>> operacion,CancellationToken ct)
    {
        if(await acceso.ObtenerAsync(ct) is not {} actor||admin&&!actor.Administrador) return ServiceResult<AnulacionCFDto>.Failure("Acceso no autorizado.");
        if(!FinanzasService.ClaveValida(clave)) return ServiceResult<AnulacionCFDto>.Failure("Clave inválida.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var huella=FinanzasService.Huella(new{contenido,actor.UsuarioId});var previo=await db.ComandosFinancieros.AsNoTracking().SingleOrDefaultAsync(x=>x.Clave==clave,ct);
        if(previo is not null) return previo.Huella==huella?ServiceResult<AnulacionCFDto>.Ok(JsonSerializer.Deserialize<AnulacionCFDto>(previo.Resultado)!):ServiceResult<AnulacionCFDto>.Failure("La clave ya se utilizó con otro contenido.");
        var (a,error)=await operacion(actor);if(error is not null||a is null)return ServiceResult<AnulacionCFDto>.Failure(error??"Operación inválida.");
        var resultado=Map(a);Guardar(clave,huella,actor,resultado);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return ServiceResult<AnulacionCFDto>.Ok(resultado);
    }
    private Task<AnulacionCF?> CargarAsync(Guid id,CancellationToken ct)=>db.AnulacionesCF.Include(x=>x.Venta).ThenInclude(x=>x.Pedido)
        .Include(x=>x.Venta).ThenInclude(x=>x.Detalles).ThenInclude(x=>x.UnidadInventario)
        .Include(x=>x.Reembolsos).Include(x=>x.Recepciones).ThenInclude(x=>x.Unidad).SingleOrDefaultAsync(x=>x.Id==id,ct);
    private static AnulacionCFDto Map(AnulacionCF a)=>new(a.Id,a.VentaId,a.Estado,a.Motivo,a.SolicitanteId,a.SolicitadaUtc,a.AdministradorId,a.DecididaUtc,a.MotivoDecision,
        a.Venta.Detalles.Sum(x=>x.PrecioFinal),a.Reembolsos.Sum(x=>x.Importe),a.Venta.Detalles.Count,a.Recepciones.Count,a.CompletadaUtc,
        a.Reembolsos.OrderBy(x=>x.RegistradoUtc).Select(x=>new ReembolsoCFDto(x.Id,x.Importe,x.Metodo,x.FechaEfectiva,x.RegistradoUtc,x.UsuarioId,x.Referencia)).ToArray(),
        a.Recepciones.OrderBy(x=>x.RegistradoUtc).Select(x=>new ArticuloDevueltoCFDto(x.UnidadId,x.Apta,x.Observacion,x.RegistradoUtc,x.UsuarioId)).ToArray());
    private void Guardar<T>(string clave,string huella,CuentaOperativa actor,T resultado)=>db.ComandosFinancieros.Add(new(){Clave=clave,Huella=huella,Resultado=JsonSerializer.Serialize(resultado),UsuarioId=actor.UsuarioId,RegistradoUtc=reloj.GetUtcNow().UtcDateTime});

    public async Task<ServiceResult<IReadOnlyList<AnulacionCFDto>>> AnulacionesAsync(int? ventaId=null,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is null) return ServiceResult<IReadOnlyList<AnulacionCFDto>>.Failure("Acceso requerido.");
        var q=db.AnulacionesCF.AsNoTracking().AsQueryable();if(ventaId.HasValue)q=q.Where(x=>x.VentaId==ventaId);
        var a=await q.Include(x=>x.Venta).ThenInclude(x=>x.Detalles).Include(x=>x.Reembolsos).Include(x=>x.Recepciones).OrderByDescending(x=>x.SolicitadaUtc).Take(100).ToListAsync(ct);
        return ServiceResult<IReadOnlyList<AnulacionCFDto>>.Ok(a.Select(Map).ToArray());
    }
    public async Task<ServiceResult<IReadOnlyList<CobroCFDto>>> CobrosAsync(int ventaId,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is null)return ServiceResult<IReadOnlyList<CobroCFDto>>.Failure("Acceso requerido.");
        return ServiceResult<IReadOnlyList<CobroCFDto>>.Ok(await db.CobrosVenta.AsNoTracking().Where(x=>x.VentaId==ventaId)
            .Select(x=>new CobroCFDto(x.Metodo,x.Aplicado,x.Entregado,x.Entregado-x.Aplicado,x.Referencia)).ToListAsync(ct));
    }
    public async Task<ServiceResult<ArchivoInformeDto>> ExportarAsync(string formato,FiltroMovimientos filtro,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is null)return ServiceResult<ArchivoInformeDto>.Failure("Acceso requerido.");
        if(formato is not ("xlsx" or "pdf")||filtro.Desde>filtro.Hasta)return ServiceResult<ArchivoInformeDto>.Failure("Formato o filtros inválidos.");
        var q=db.Ventas.AsNoTracking().Where(x=>x.Pedido.ClienteId==null);
        if(filtro.Desde.HasValue)q=q.Where(x=>x.Fecha>=filtro.Desde);if(filtro.Hasta.HasValue)q=q.Where(x=>x.Fecha<=filtro.Hasta);
        if(!string.IsNullOrWhiteSpace(filtro.Referencia))q=q.Where(x=>x.CodigoInterno.Contains(filtro.Referencia));
        var ventas=await q.Include(x=>x.Detalles).OrderBy(x=>x.Fecha).ThenBy(x=>x.Id).Take(100001).ToListAsync(ct);
        if(ventas.Count>100000)return ServiceResult<ArchivoInformeDto>.Failure("Acota el periodo: más de 100,000 ventas.");
        var ids=ventas.Select(x=>x.Id).ToArray();var cobros=await db.CobrosVenta.AsNoTracking().Where(x=>ids.Contains(x.VentaId)).ToListAsync(ct);
        var anulaciones=await db.AnulacionesCF.AsNoTracking().Where(x=>ids.Contains(x.VentaId)).Include(x=>x.Reembolsos).ToListAsync(ct);
        var filas=new List<object?[]>();
        foreach(var v in ventas)
        {
            var a=anulaciones.Where(x=>x.VentaId==v.Id).OrderByDescending(x=>x.SolicitadaUtc).FirstOrDefault();
            filas.Add([v.Fecha,v.CodigoInterno,((v.NombreOcasional+" "+v.ApellidoOcasional).Trim() is {Length:>0} n?n:"Consumidor Final (CF)"),v.CalcularTotal(),v.Estado,
                string.Join("; ",cobros.Where(x=>x.VentaId==v.Id).Select(x=>$"{x.Metodo}: aplicado {x.Aplicado:0.00}, entregado {x.Entregado:0.00}, cambio {x.Entregado-x.Aplicado:0.00}")),a?.Estado,
                a?.Reembolsos.Sum(x=>x.Importe)??0,v.UsuarioId]);
        }
        filas.Add(["", "Total ventas vigentes", "", ventas.Where(x=>x.Estado==EstadoVenta.Registrada).Sum(x=>x.CalcularTotal())]);
        filas.Add(["", "Total dinero efectivamente devuelto", "", anulaciones.Sum(x=>x.Reembolsos.Sum(r=>r.Importe))]);
        return ServiceResult<ArchivoInformeDto>.Ok(InformeArchivos.Crear("ventas-cf",formato,["Fecha","Venta","Comprador","Total Q","Estado venta","Cobros originales","Anulación","Devuelto Q","Usuario"],filas,reloj.GetUtcNow().UtcDateTime,$"Desde {filtro.Desde}; hasta {filtro.Hasta}; referencia {filtro.Referencia}"));
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Infrastructure.Persistence;
using SkiaSharp;
namespace ResellManager.Infrastructure.Services;
public sealed class EtiquetasService(ResellManagerDbContext db,ICuentaOperativa acceso,TimeProvider reloj):IEtiquetasService
{
    private sealed record Datos(PerfilEtiquetas Perfil,IReadOnlyList<FilaEtiquetaDto> Filas);
    public async Task<ServiceResult<LoteEtiquetasDto>> PrepararAsync(IReadOnlyList<CopiasEtiquetaInput> productos,PerfilEtiquetas perfil,string clave,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is not {} actor)return ServiceResult<LoteEtiquetasDto>.Failure("Acceso requerido.");
        if(!FinanzasService.ClaveValida(clave)||productos.Count is <1 or >500||productos.Any(x=>x.Copias is <1 or >1000)||productos.Sum(x=>x.Copias)>5000
            ||productos.Select(x=>x.ProductoId).Distinct().Count()!=productos.Count)return ServiceResult<LoteEtiquetasDto>.Failure("Selecciona productos distintos, hasta 5,000 etiquetas y 1,000 copias por producto.");
        if(perfil.AnchoMm is <20 or >200||perfil.AltoMm is <25 or >150||perfil.MargenMm is <1 or >5||perfil.Dpi is not(203 or 300 or 600))return ServiceResult<LoteEtiquetasDto>.Failure("Perfil inválido. Ancho 20–200 mm, alto 25–150 mm, margen 1–5 mm y resolución 203/300/600 dpi.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var huella=FinanzasService.Huella(new{Operacion="etiquetas",productos,perfil,actor.UsuarioId});
        var previo=await db.ComandosFinancieros.AsNoTracking().SingleOrDefaultAsync(x=>x.Clave==clave,ct);
        if(previo is not null)return previo.Huella==huella?ServiceResult<LoteEtiquetasDto>.Ok(JsonSerializer.Deserialize<LoteEtiquetasDto>(previo.Resultado)!):ServiceResult<LoteEtiquetasDto>.Failure("Clave usada con otro contenido.");
        var ids=productos.Select(x=>x.ProductoId).ToArray();var filas=await db.Productos.AsNoTracking().Where(x=>ids.Contains(x.Id)).OrderBy(x=>x.Nombre).ToListAsync(ct);
        if(filas.Count!=productos.Count)return ServiceResult<LoteEtiquetasDto>.Failure("Algún producto ya no existe.");
        foreach(var p in filas)
            if(await db.Productos.AnyAsync(x=>x.Id!=p.Id&&(x.CodigoInterno==p.CodigoInterno||x.CodigoBarras==p.CodigoInterno),ct))return ServiceResult<LoteEtiquetasDto>.Failure("Colisión de código interno: resuélvela antes de imprimir.");
        var contenido=filas.Select(p=>new FilaEtiquetaDto(p.Id,p.Nombre,p.CodigoInterno,p.PrecioSugerido,productos.Single(x=>x.ProductoId==p.Id).Copias,string.Join(" · ",new[]{p.Color,p.Talla,p.Presentacion}.Where(x=>!string.IsNullOrWhiteSpace(x))))).ToArray();
        var lote=new LoteEtiquetas{Id=Guid.NewGuid(),ContenidoJson=JsonSerializer.Serialize(new Datos(perfil,contenido)),UsuarioId=actor.UsuarioId,CreadoUtc=reloj.GetUtcNow().UtcDateTime};
        db.LotesEtiquetas.Add(lote);var resultado=new LoteEtiquetasDto(lote.Id,lote.CreadoUtc,null,perfil,contenido,contenido.Sum(x=>x.Copias),false);
        db.ComandosFinancieros.Add(new(){Clave=clave,Huella=huella,Resultado=JsonSerializer.Serialize(resultado),UsuarioId=actor.UsuarioId,RegistradoUtc=lote.CreadoUtc});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return ServiceResult<LoteEtiquetasDto>.Ok(resultado);
    }
    public async Task<ServiceResult<LoteEtiquetasDto>> ObtenerAsync(Guid id,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is null)return ServiceResult<LoteEtiquetasDto>.Failure("Acceso requerido.");
        var lote=await db.LotesEtiquetas.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);if(lote is null)return ServiceResult<LoteEtiquetasDto>.Failure("Lote no encontrado.");
        var datos=JsonSerializer.Deserialize<Datos>(lote.ContenidoJson)!;var ids=datos.Filas.Select(x=>x.ProductoId).ToArray();var actuales=await db.Productos.AsNoTracking().Where(x=>ids.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct);
        var desactualizado=datos.Filas.Any(x=>!actuales.TryGetValue(x.ProductoId,out var p)||p.PrecioSugerido!=x.Precio||p.Nombre!=x.Nombre||p.CodigoInterno!=x.Codigo);
        return ServiceResult<LoteEtiquetasDto>.Ok(new(lote.Id,lote.CreadoUtc,lote.ImpresoUtc,datos.Perfil,datos.Filas,datos.Filas.Sum(x=>x.Copias),desactualizado));
    }
    public async Task<ServiceResult<ArchivoInformeDto>> ExportarAsync(Guid id,string formato,CancellationToken ct=default)
    {
        var r=await ObtenerAsync(id,ct);if(!r.IsSuccess)return ServiceResult<ArchivoInformeDto>.Failure(r.ErrorMessage!);var l=r.Value!;
        if(l.Desactualizado)return ServiceResult<ArchivoInformeDto>.Failure("Precio o producto actualizado. Prepara un nuevo lote para reimprimir.");
        if(formato=="xlsx")
        {
            var rows=l.Filas.SelectMany(x=>Enumerable.Range(0,x.Copias).Select(_=>new object?[]{x.Nombre,x.Precio>0?x.Precio:"Consultar precio",x.Codigo,x.Atributos,1})).ToArray();
            return ServiceResult<ArchivoInformeDto>.Ok(InformeArchivos.Crear("etiquetas",formato,["Nombre","Precio Q","Codigo interno","Atributos","Copias"],rows,l.CreadoUtc,"Una fila por etiqueta. La importación y plantilla NIIMBOT requieren validación con la app real."));
        }
        if(formato!="pdf")return ServiceResult<ArchivoInformeDto>.Failure("Formato inválido.");
        var altoMinimo=2*l.Perfil.MargenMm+71*25.4m/72;
        if(l.Perfil.AltoMm<altoMinimo)return ServiceResult<ArchivoInformeDto>.Failure($"El perfil requiere al menos {decimal.Ceiling(altoMinimo)} mm de alto para nombre, precio, barras y texto legible.");
        foreach(var f in l.Filas)
        {
            decimal minimo;
            try { minimo=Codigo128Etiquetas.AnchoMinimo(f.Codigo,l.Perfil); }
            catch(ArgumentException e) { return ServiceResult<ArchivoInformeDto>.Failure(e.Message); }
            if(l.Perfil.AnchoMm<minimo)return ServiceResult<ArchivoInformeDto>.Failure($"El código completo {f.Codigo} requiere al menos {decimal.Ceiling(minimo)} mm de ancho a {l.Perfil.Dpi} dpi con barras de 2 puntos. Configura un perfil compatible; no se reducirá a barras ilegibles.");
        }
        using var memoria=new MemoryStream();using var pdf=SKDocument.CreatePdf(memoria);using var fuente=new SKFont(SKTypeface.FromFamilyName("DejaVu Sans"),8);using var tinta=new SKPaint{Color=SKColors.Black,IsAntialias=false};
        const float puntosMm=72f/25.4f;var ancho=(float)l.Perfil.AnchoMm*puntosMm;var alto=(float)l.Perfil.AltoMm*puntosMm;var margen=(float)l.Perfil.MargenMm*puntosMm;
        foreach(var fila in l.Filas)
            for(var copia=0;copia<fila.Copias;copia++)
            {
                var c=pdf.BeginPage(ancho,alto);var nombre=fila.Nombre;while(nombre.Length>0&&fuente.MeasureText(nombre)>ancho-2*margen)nombre=nombre[..^1];
                c.DrawText(nombre,margen,margen+9,SKTextAlign.Left,fuente,tinta);c.DrawText(fila.Precio>0?$"Q {fila.Precio:0.00}":"Consultar precio",margen,margen+21,SKTextAlign.Left,fuente,tinta);
                var modulo=2f*72/l.Perfil.Dpi;var patron=Codigo128Etiquetas.Anchuras(fila.Codigo);var x=(ancho-patron.Sum()*modulo)/2;
                var y=margen+28;var altura=Math.Max(28,alto-y-margen-15);
                for(var i=0;i<patron.Length;i++){var w=patron[i]*modulo;if(i%2==0)c.DrawRect(x,y,w,altura,tinta);x+=w;}
                using var pequeña=new SKFont(fuente.Typeface,6);c.DrawText(fila.Codigo,margen,alto-margen,SKTextAlign.Left,pequeña,tinta);pdf.EndPage();
            }
        pdf.Close();return ServiceResult<ArchivoInformeDto>.Ok(new("etiquetas.pdf","application/pdf",memoria.ToArray()));
    }
    public async Task<ServiceResult> ConfirmarImpresionAsync(Guid id,CancellationToken ct=default)
    {
        if(await acceso.ObtenerAsync(ct) is not {} actor)return ServiceResult.Failure("Acceso requerido.");
        var r=await ObtenerAsync(id,ct);if(!r.IsSuccess||r.Value!.Desactualizado)return ServiceResult.Failure(r.ErrorMessage??"El precio cambió. Reimprime desde un lote actualizado.");
        var lote=await db.LotesEtiquetas.SingleAsync(x=>x.Id==id,ct);if(lote.ImpresoUtc is null){lote.ImpresoUtc=reloj.GetUtcNow().UtcDateTime;lote.ImpresoPor=actor.UsuarioId;await db.SaveChangesAsync(ct);}return ServiceResult.Ok();
    }
}

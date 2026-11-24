using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;
using ResellManager.Application.Interfaces;
using ResellManager.Domain.Entities;
using ResellManager.Domain.Enums;
using ResellManager.Infrastructure.Persistence;

namespace ResellManager.Infrastructure.Services;

public sealed class CuentaOperativaDenegada : ICuentaOperativa
{
    public Task<CuentaOperativa?> ObtenerAsync(CancellationToken ct = default) => Task.FromResult<CuentaOperativa?>(null);
}

public sealed class FinanzasService(ResellManagerDbContext db, ICuentaOperativa acceso, TimeProvider reloj) : IFinanzasService
{
    private sealed record Borrador(IReadOnlyList<FilaAperturaInput> Entradas, IReadOnlyList<FilaAperturaDto> Vista);
    internal static bool Moneda(decimal valor) => valor is >= -99999999.99m and <= 99999999.99m && decimal.Round(valor, 2) == valor;
    internal static DateOnly Hoy(TimeProvider reloj) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
        reloj.GetUtcNow().UtcDateTime, TimeZoneInfo.FindSystemTimeZoneById("America/Guatemala")));
    internal static string Huella(object valor) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(valor)));
    internal static bool ClaveValida(string clave) => !string.IsNullOrWhiteSpace(clave) && clave.Length <= 120;
    private static string Telefono(string valor) => new(valor.Where(char.IsDigit).ToArray());

    public async Task<ServiceResult<AperturaDto>> AperturaAsync(CancellationToken ct = default)
    {
        if (await acceso.ObtenerAsync(ct) is not { Administrador: true }) return ServiceResult<AperturaDto>.Failure("Acceso administrativo requerido.");
        var lote = await db.AperturasCartera.AsNoTracking().SingleOrDefaultAsync(ct);
        if (lote is null) return ServiceResult<AperturaDto>.Ok(new(Guid.Empty, false, null, null, null, [], 0, false));
        var borrador = JsonSerializer.Deserialize<Borrador>(lote.FilasJson)!;
        return ServiceResult<AperturaDto>.Ok(Map(lote, borrador.Vista, borrador.Entradas));
    }

    public async Task<ServiceResult<AperturaDto>> PrepararCsvAsync(string csv, CancellationToken ct = default)
    {
        if (await acceso.ObtenerAsync(ct) is not { Administrador: true }) return ServiceResult<AperturaDto>.Failure("Acceso administrativo requerido.");
        if (csv.Length > 1_000_000) return ServiceResult<AperturaDto>.Failure("El CSV excede 1 MB de texto.");
        try
        {
            var tabla = Csv(csv.TrimStart('\uFEFF'));
            if (tabla.Count == 0 || !tabla[0].SequenceEqual(new[] { "clienteId", "telefono", "nombre", "apellido", "deuda", "observacion" }))
                return ServiceResult<AperturaDto>.Failure("Usa la plantilla CSV UTF-8 con encabezado clienteId,telefono,nombre,apellido,deuda,observacion. Decimales con punto, sin separadores de miles.");
            var filas = new List<FilaAperturaInput>();
            for (var i = 1; i < tabla.Count; i++)
            {
                var fila = tabla[i];
                if (fila.Length != 6 || (!string.IsNullOrWhiteSpace(fila[0]) && !int.TryParse(fila[0], out _))
                    || !decimal.TryParse(fila[4], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var importe))
                    return ServiceResult<AperturaDto>.Failure($"Fila {i + 1}: columnas, identificador o importe inválido.");
                filas.Add(new(string.IsNullOrWhiteSpace(fila[0]) ? null : int.Parse(fila[0], CultureInfo.InvariantCulture), fila[1], fila[2], fila[3], importe, fila[5]));
            }
            return await PrepararAsync(filas, ct);
        }
        catch (FormatException e) { return ServiceResult<AperturaDto>.Failure(e.Message); }
    }

    public async Task<ServiceResult<AperturaDto>> PrepararAsync(IReadOnlyList<FilaAperturaInput> filas, CancellationToken ct = default)
    {
        if (await acceso.ObtenerAsync(ct) is not { Administrador: true } actor) return ServiceResult<AperturaDto>.Failure("Acceso administrativo requerido.");
        if (filas.Count > 10000) return ServiceResult<AperturaDto>.Failure("Se permiten hasta 10,000 filas.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var lote = await db.AperturasCartera.SingleOrDefaultAsync(ct);
        if (lote?.ConfirmadaUtc is not null) return ServiceResult<AperturaDto>.Failure("La apertura ya fue confirmada. Utiliza ajustes para corregirla.");
        var vista = await ValidarFilasAsync(filas, ct);
        lote ??= new AperturaCartera();
        if (db.Entry(lote).State == EntityState.Detached) db.AperturasCartera.Add(lote);
        lote.Version = Guid.NewGuid(); lote.UsuarioId = actor.UsuarioId;
        lote.FilasJson = JsonSerializer.Serialize(new Borrador(filas, vista));
        lote.Total = vista.Where(x => x.Error is null && !x.Omitida).Sum(x => x.Deuda);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<AperturaDto>.Ok(Map(lote, vista, filas));
    }

    public async Task<ServiceResult<AperturaDto>> ConfirmarAsync(Guid version, string clave, CancellationToken ct = default)
    {
        if (await acceso.ObtenerAsync(ct) is not { Administrador: true } actor) return ServiceResult<AperturaDto>.Failure("Acceso administrativo requerido.");
        if (!ClaveValida(clave)) return ServiceResult<AperturaDto>.Failure("Clave de operación inválida.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var huella = Huella(new { Operacion = "apertura", version, actor.UsuarioId });
        var anterior = await db.ComandosFinancieros.AsNoTracking().SingleOrDefaultAsync(x => x.Clave == clave, ct);
        if (anterior is not null) return anterior.Huella == huella
            ? ServiceResult<AperturaDto>.Ok(JsonSerializer.Deserialize<AperturaDto>(anterior.Resultado)!)
            : ServiceResult<AperturaDto>.Failure("La clave ya se utilizó con otro contenido.");
        var lote = await db.AperturasCartera.SingleOrDefaultAsync(ct);
        if (lote is null || lote.ConfirmadaUtc is not null || lote.Version != version)
            return ServiceResult<AperturaDto>.Failure("Apertura cerrada o borrador modificado. Revisa la vista previa.");
        if (await db.Ventas.AnyAsync(ct) || await db.Pagos.AnyAsync(ct) || await db.MovimientosCuenta.AnyAsync(ct))
            return ServiceResult<AperturaDto>.Failure("Existe actividad financiera previa. La apertura está bloqueada hasta resolver la discrepancia.");
        var borrador = JsonSerializer.Deserialize<Borrador>(lote.FilasJson)!;
        var vista = await ValidarFilasAsync(borrador.Entradas, ct);
        if (vista.Any(x => x.Error is not null) || !vista.Any(x => !x.Omitida)
            || !vista.SequenceEqual(borrador.Vista))
            return ServiceResult<AperturaDto>.Failure("Hay errores o clientes cambiados desde la vista previa. Prepara el lote nuevamente.");
        var ahora = reloj.GetUtcNow().UtcDateTime; var corte = Hoy(reloj);
        var aplicadas = new List<FilaAperturaDto>();
        foreach (var fila in vista)
        {
            if (fila.Omitida) { aplicadas.Add(fila); continue; }
            var clienteId = fila.ClienteId;
            if (clienteId is null)
            {
                var entrada = borrador.Entradas[fila.Fila - 1];
                var cliente = new Cliente { Nombres = entrada.Nombre.Trim(), Apellidos = entrada.Apellido?.Trim(), Telefono = entrada.Telefono.Trim() };
                db.Clientes.Add(cliente); await db.SaveChangesAsync(ct); clienteId = cliente.Id;
            }
            db.MovimientosCuenta.Add(new MovimientoCuenta { Id = Guid.NewGuid(), ClienteId = clienteId.Value,
                Tipo = TipoMovimientoCuenta.Apertura, Importe = fila.Deuda, FechaEfectiva = corte, RegistradoUtc = ahora,
                UsuarioId = actor.UsuarioId, Motivo = "Saldo inicial / Deuda anterior a ResellManager", Referencia = $"APERTURA-{version:N}:fila-{fila.Fila}" });
            aplicadas.Add(fila with { ClienteId = clienteId });
        }
        lote.ConfirmadaUtc = ahora; lote.Corte = corte; lote.UsuarioId = actor.UsuarioId;
        lote.FilasJson = JsonSerializer.Serialize(new Borrador(borrador.Entradas, aplicadas));
        var resultado = Map(lote, aplicadas, borrador.Entradas);
        GuardarComando(clave, huella, actor, resultado);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<AperturaDto>.Ok(resultado);
    }

    public async Task<ServiceResult<ResultadoAjusteDto>> RevisarAjusteAsync(int clienteId, decimal importe, CancellationToken ct = default)
    {
        if (await acceso.ObtenerAsync(ct) is not { Administrador: true }) return ServiceResult<ResultadoAjusteDto>.Failure("Acceso administrativo requerido.");
        if (importe == 0 || !Moneda(importe) || !await db.Clientes.AnyAsync(x => x.Id == clienteId, ct)) return ServiceResult<ResultadoAjusteDto>.Failure("Cliente o importe inválido.");
        var saldo = await SaldoConsultas.CalcularAsync(db, clienteId, ct);
        return saldo + importe < 0 ? ServiceResult<ResultadoAjusteDto>.Failure("La operación produciría saldo a favor.")
            : ServiceResult<ResultadoAjusteDto>.Ok(new(Guid.Empty, saldo, importe, saldo + importe));
    }

    public Task<ServiceResult<ResultadoAjusteDto>> AjustarAsync(AjusteInput input, CancellationToken ct = default)
        => CambiarAsync(input, null, ct);

    public async Task<ServiceResult<ResultadoAjusteDto>> RevertirAsync(Guid movimientoId, string motivo, string clave, CancellationToken ct = default)
    {
        if (await acceso.ObtenerAsync(ct) is not { Administrador: true }) return ServiceResult<ResultadoAjusteDto>.Failure("Acceso administrativo requerido.");
        var original = await db.MovimientosCuenta.AsNoTracking().SingleOrDefaultAsync(x => x.Id == movimientoId, ct);
        if (original is null || original.Tipo != TipoMovimientoCuenta.Ajuste)
            return ServiceResult<ResultadoAjusteDto>.Failure("Solo pueden revertirse ajustes; ventas, pagos y apertura requieren su proceso propio.");
        return await CambiarAsync(new(original.ClienteId, -original.Importe, motivo, clave, original.Id.ToString()), original.Id, ct);
    }

    private async Task<ServiceResult<ResultadoAjusteDto>> CambiarAsync(AjusteInput input, Guid? revierte, CancellationToken ct)
    {
        if (await acceso.ObtenerAsync(ct) is not { Administrador: true } actor) return ServiceResult<ResultadoAjusteDto>.Failure("Acceso administrativo requerido.");
        if (input.Importe == 0 || !Moneda(input.Importe) || string.IsNullOrWhiteSpace(input.Motivo)
            || input.Motivo.Length > 1000 || input.Referencia?.Length > 200 || !ClaveValida(input.Clave))
            return ServiceResult<ResultadoAjusteDto>.Failure("Importe, motivo o clave inválidos. Usa hasta dos decimales.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var huella = Huella(new { Operacion = "ajuste", input, revierte, actor.UsuarioId });
        var anterior = await db.ComandosFinancieros.AsNoTracking().SingleOrDefaultAsync(x => x.Clave == input.Clave, ct);
        if (anterior is not null) return anterior.Huella == huella
            ? ServiceResult<ResultadoAjusteDto>.Ok(JsonSerializer.Deserialize<ResultadoAjusteDto>(anterior.Resultado)!)
            : ServiceResult<ResultadoAjusteDto>.Failure("La clave ya se utilizó con otro contenido.");
        if (!await db.Clientes.AnyAsync(x => x.Id == input.ClienteId, ct)) return ServiceResult<ResultadoAjusteDto>.Failure("Cliente no encontrado.");
        if (revierte.HasValue && await db.MovimientosCuenta.AnyAsync(x => x.RevierteId == revierte, ct))
            return ServiceResult<ResultadoAjusteDto>.Failure("El ajuste ya fue revertido.");
        var saldo = await SaldoConsultas.CalcularAsync(db, input.ClienteId, ct);
        if (saldo + input.Importe < 0) return ServiceResult<ResultadoAjusteDto>.Failure("La operación produciría saldo a favor; no está permitido.");
        var id = Guid.NewGuid();
        db.MovimientosCuenta.Add(new MovimientoCuenta { Id = id, ClienteId = input.ClienteId,
            Tipo = revierte.HasValue ? TipoMovimientoCuenta.Reversion : TipoMovimientoCuenta.Ajuste,
            Importe = input.Importe, FechaEfectiva = Hoy(reloj), RegistradoUtc = reloj.GetUtcNow().UtcDateTime,
            UsuarioId = actor.UsuarioId, Motivo = input.Motivo.Trim(), Referencia = input.Referencia?.Trim(), RevierteId = revierte });
        var resultado = new ResultadoAjusteDto(id, saldo, input.Importe, saldo + input.Importe);
        GuardarComando(input.Clave, huella, actor, resultado);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return ServiceResult<ResultadoAjusteDto>.Ok(resultado);
    }

    internal void GuardarComando<T>(string clave, string huella, CuentaOperativa actor, T resultado) => db.ComandosFinancieros.Add(new()
    { Clave = clave, Huella = huella, Resultado = JsonSerializer.Serialize(resultado), UsuarioId = actor.UsuarioId, RegistradoUtc = reloj.GetUtcNow().UtcDateTime });

    private async Task<List<FilaAperturaDto>> ValidarFilasAsync(IReadOnlyList<FilaAperturaInput> filas, CancellationToken ct)
    {
        var clientes = await db.Clientes.AsNoTracking().ToListAsync(ct);
        var vista = new List<FilaAperturaDto>(); var ids = new HashSet<int>(); var telefonos = new HashSet<string>();
        for (var i = 0; i < filas.Count; i++)
        {
            var f = filas[i]; var tel = Telefono(f.Telefono); string? error = null; Cliente? cliente = null;
            if (f.Deuda == 0) { vista.Add(new(i + 1, f.ClienteId, f.Nombre, f.Telefono, 0, f.Observacion, null, true)); continue; }
            if (f.Deuda < 0 || !Moneda(f.Deuda)) error = "Deuda inválida: debe ser positiva con hasta dos decimales.";
            else if (tel.Length is < 7 or > 20 || f.Telefono.Length > 30) error = "Teléfono obligatorio e inválido.";
            else if (f.Nombre.Trim().Length is < 1 or > 100 || f.Apellido?.Length > 100 || f.Observacion?.Length > 500) error = "Nombre u observación inválidos.";
            else
            {
                var coincidencias = f.ClienteId.HasValue ? clientes.Where(x => x.Id == f.ClienteId).ToArray()
                    : clientes.Where(x => Telefono(x.Telefono) == tel).ToArray();
                if (coincidencias.Length > 1) error = "Teléfono ambiguo: selecciona un ID de cliente explícito.";
                else if (f.ClienteId.HasValue && coincidencias.Length == 0) error = "ID de cliente no encontrado.";
                else
                {
                    cliente = coincidencias.SingleOrDefault();
                    if (cliente is not null && Telefono(cliente.Telefono) != tel) error = "El teléfono no coincide con el cliente seleccionado.";
                    else if ((cliente is not null && !ids.Add(cliente.Id)) || !telefonos.Add(tel)) error = "Cliente o teléfono duplicado en el lote.";
                }
            }
            vista.Add(new(i + 1, cliente?.Id, cliente?.NombreCompleto() ?? (f.Nombre.Trim() + " " + f.Apellido?.Trim()).Trim(), f.Telefono.Trim(), f.Deuda, f.Observacion, error, false));
        }
        return vista;
    }

    private static AperturaDto Map(AperturaCartera lote, IReadOnlyList<FilaAperturaDto> vista, IReadOnlyList<FilaAperturaInput> entradas) => new(lote.Version,
        lote.ConfirmadaUtc is not null, lote.Corte, lote.ConfirmadaUtc, lote.UsuarioId, vista, lote.Total,
        lote.ConfirmadaUtc is null && vista.Any(x => !x.Omitida) && vista.All(x => x.Error is null), entradas);

    public async Task<ServiceResult<PaginaMovimientosDto>> ConsultarAsync(FiltroMovimientos filtro, CancellationToken ct = default)
    {
        if (await acceso.ObtenerAsync(ct) is null) return ServiceResult<PaginaMovimientosDto>.Failure("Inicia sesión para consultar movimientos.");
        if (!FiltroValido(filtro)) return ServiceResult<PaginaMovimientosDto>.Failure("Filtros inválidos.");
        var limite = checked(filtro.Pagina * filtro.Tamano + 1);
        var filas = await LeerAsync(filtro, limite, ct);
        var pagina = filas.Skip((filtro.Pagina - 1) * filtro.Tamano).Take(filtro.Tamano + 1).ToArray();
        return ServiceResult<PaginaMovimientosDto>.Ok(new(pagina.Take(filtro.Tamano).ToArray(), filtro.Pagina, pagina.Length > filtro.Tamano,
            filtro.ClienteId.HasValue ? await SaldoConsultas.CalcularAsync(db, filtro.ClienteId, ct) : null));
    }

    private static bool FiltroValido(FiltroMovimientos f) => f.Pagina is > 0 and <= 10000 && f.Tamano is > 0 and <= 100
        && (f.Desde is null || f.Hasta is null || f.Desde <= f.Hasta) && f.Referencia?.Length is not > 200;

    private async Task<List<MovimientoDto>> LeerAsync(FiltroMovimientos f, int limite, CancellationToken ct)
    {
        var resultado = new List<MovimientoDto>();
        var movimientos = db.MovimientosCuenta.AsNoTracking().AsQueryable();
        if (f.ClienteId.HasValue) movimientos = movimientos.Where(x => x.ClienteId == f.ClienteId);
        if (f.Desde.HasValue) movimientos = movimientos.Where(x => x.FechaEfectiva >= f.Desde);
        if (f.Hasta.HasValue) movimientos = movimientos.Where(x => x.FechaEfectiva <= f.Hasta);
        if (!string.IsNullOrWhiteSpace(f.Referencia)) movimientos = movimientos.Where(x => x.Referencia != null && x.Referencia.Contains(f.Referencia));
        if (!string.IsNullOrEmpty(f.Tipo)) movimientos = Enum.TryParse<TipoMovimientoCuenta>(f.Tipo, out var tipo)
            ? movimientos.Where(x => x.Tipo == tipo) : movimientos.Where(x => false);
        resultado.AddRange(await movimientos.OrderByDescending(x => x.FechaEfectiva).ThenByDescending(x => x.Id).Take(limite)
            .Select(x => new MovimientoDto(x.Id.ToString(), x.ClienteId, x.Cliente.Nombres + " " + (x.Cliente.Apellidos ?? ""),
                x.Tipo.ToString(), x.FechaEfectiva, x.RegistradoUtc, x.Importe, x.UsuarioId, x.Motivo, x.Referencia ?? "", x.RevierteId == null ? null : x.RevierteId.ToString())).ToListAsync(ct));
        if (string.IsNullOrEmpty(f.Tipo) || f.Tipo is "Venta" or "Cancelacion")
        {
            var ventas = db.Ventas.AsNoTracking().Where(x => x.Pedido.ClienteId != null);
            if (f.ClienteId.HasValue) ventas = ventas.Where(x => x.Pedido.ClienteId == f.ClienteId);
            if (!string.IsNullOrWhiteSpace(f.Referencia)) ventas = ventas.Where(x => x.CodigoInterno.Contains(f.Referencia));
            if (string.IsNullOrEmpty(f.Tipo) || f.Tipo == "Venta")
            {
                var cargos = ventas;
                if (f.Desde.HasValue) cargos = cargos.Where(x => x.Fecha >= f.Desde);
                if (f.Hasta.HasValue) cargos = cargos.Where(x => x.Fecha <= f.Hasta);
                resultado.AddRange(await cargos.OrderByDescending(x => x.Fecha).ThenByDescending(x => "V-" + x.Id).Take(limite)
                    .Select(x => new MovimientoDto("V-" + x.Id, x.Pedido.ClienteId, x.Pedido.Cliente.Nombres + " " + (x.Pedido.Cliente.Apellidos ?? ""),
                        "Venta", x.Fecha, x.RegistradaUtc, x.Detalles.Sum(d => d.PrecioFinal), x.UsuarioId, "Venta original", x.CodigoInterno, null)).ToListAsync(ct));
            }
            if (string.IsNullOrEmpty(f.Tipo) || f.Tipo == "Cancelacion")
            {
                var anuladas = ventas.Where(x => x.Estado == EstadoVenta.Cancelada);
                // Legacy cancellations have no known timestamp; keep that absence explicit.
                var cancelaciones = anuladas.Select(x => new { Venta = x, Fecha = x.CanceladaUtc.HasValue ? DateOnly.FromDateTime(x.CanceladaUtc.Value.AddHours(-6)) : x.Fecha });
                if(f.Desde.HasValue) cancelaciones = cancelaciones.Where(x=>x.Fecha>=f.Desde);
                if(f.Hasta.HasValue) cancelaciones = cancelaciones.Where(x=>x.Fecha<=f.Hasta);
                resultado.AddRange(await cancelaciones.OrderByDescending(x=>x.Fecha).ThenByDescending(x=>"C-" + x.Venta.Id).Take(limite)
                    .Select(x => new MovimientoDto("C-" + x.Venta.Id, x.Venta.Pedido.ClienteId,
                        x.Venta.Pedido.Cliente.Nombres + " " + (x.Venta.Pedido.Cliente.Apellidos ?? ""), "Cancelacion", x.Fecha,
                        x.Venta.CanceladaUtc, -x.Venta.Detalles.Sum(d => d.PrecioFinal), x.Venta.CanceladaPor,
                        x.Venta.CanceladaUtc.HasValue ? "Cancelación de venta" : "Cancelación histórica sin fecha de registro conocida", x.Venta.CodigoInterno, "V-" + x.Venta.Id)).ToListAsync(ct));
            }
        }
        if (string.IsNullOrEmpty(f.Tipo) || f.Tipo == "Abono")
        {
            var pagos = db.Pagos.AsNoTracking().AsQueryable();
            if (f.ClienteId.HasValue) pagos = pagos.Where(x => x.ClienteId == f.ClienteId);
            if (f.Desde.HasValue) pagos = pagos.Where(x => x.Fecha >= f.Desde);
            if (f.Hasta.HasValue) pagos = pagos.Where(x => x.Fecha <= f.Hasta);
            if (!string.IsNullOrWhiteSpace(f.Referencia)) pagos = pagos.Where(x => x.Referencia != null && x.Referencia.Contains(f.Referencia));
            resultado.AddRange(await pagos.OrderByDescending(x => x.Fecha).ThenByDescending(x => "P-" + x.Id).Take(limite)
                .Select(x => new MovimientoDto("P-" + x.Id, x.ClienteId, x.Cliente.Nombres + " " + (x.Cliente.Apellidos ?? ""),
                    "Abono", x.Fecha, x.RegistradoUtc, -x.Monto, x.UsuarioId, "Abono recibido", x.Referencia ?? "", null)).ToListAsync(ct));
        }
        return resultado.OrderByDescending(x => x.Fecha).ThenByDescending(x => x.Id, StringComparer.Ordinal).Take(limite).ToList();
    }

    public async Task<ServiceResult<ArchivoInformeDto>> ExportarAsync(string informe, string formato, FiltroMovimientos filtro, CancellationToken ct = default)
    {
        if (await acceso.ObtenerAsync(ct) is null) return ServiceResult<ArchivoInformeDto>.Failure("Acceso requerido.");
        if (!FiltroValido(filtro) || formato is not ("xlsx" or "pdf")) return ServiceResult<ArchivoInformeDto>.Failure("Formato o filtros inválidos.");
        var rows = new List<object?[]>(); string[] columnas;
        if (informe == "apertura")
        {
            var apertura = await AperturaAsync(ct);
            if (!apertura.IsSuccess || apertura.Value is null) return ServiceResult<ArchivoInformeDto>.Failure(apertura.ErrorMessage ?? "Sin apertura.");
            columnas = ["Fila", "Cliente", "Teléfono", "Deuda", "Estado", "Corte", "Responsable"];
            rows.AddRange(apertura.Value.Filas.Select(x => new object?[] { x.Fila, x.Cliente, x.Telefono, x.Deuda,
                x.Error ?? (x.Omitida ? "Omitida Q0" : apertura.Value.Confirmada ? "Aplicada" : "Borrador"), apertura.Value.Corte, apertura.Value.UsuarioId }));
            rows.Add(["", "Total del lote", "", apertura.Value.Total]);
        }
        else if (informe is "movimientos" or "cliente")
        {
            if (informe == "cliente" && filtro.ClienteId is null) return ServiceResult<ArchivoInformeDto>.Failure("Selecciona un cliente.");
            var filas = await LeerAsync(filtro, 100001, ct);
            if (filas.Count > 100000) return ServiceResult<ArchivoInformeDto>.Failure("Más de 100,000 movimientos: acota los filtros para exportar.");
            columnas = ["Fecha efectiva", "Cliente", "Tipo", "Importe Q", "Referencia", "Motivo", "Registrado UTC", "Usuario", "Original"];
            rows.AddRange(filas.Select(x => new object?[] { x.Fecha, x.Cliente, x.Tipo, x.Importe, x.Referencia, x.Descripcion, x.RegistradoUtc, x.UsuarioId, x.OriginalId }));
            rows.Add(["", "Cambio neto del conjunto filtrado", "", filas.Sum(x => x.Importe)]);
            if (filtro.ClienteId.HasValue) rows.Add(["", "Saldo actual global", "", await SaldoConsultas.CalcularAsync(db, filtro.ClienteId, ct)]);
        }
        else return ServiceResult<ArchivoInformeDto>.Failure("Informe inválido.");
        return ServiceResult<ArchivoInformeDto>.Ok(InformeArchivos.Crear(informe, formato, columnas, rows, reloj.GetUtcNow().UtcDateTime,
            $"Cliente: {filtro.ClienteId?.ToString() ?? "todos"}; desde: {filtro.Desde}; hasta: {filtro.Hasta}; tipo: {filtro.Tipo}; referencia: {filtro.Referencia}"));
    }

    internal static List<string[]> Csv(string csv)
    {
        var filas = new List<string[]>(); var fila = new List<string>(); var celda = new StringBuilder(); bool comillas = false, cerrada = false;
        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];
            if (comillas)
            {
                if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"') { celda.Append('"'); i++; }
                else if (c == '"') { comillas = false; cerrada = true; }
                else celda.Append(c);
            }
            else if (c == '"' && celda.Length == 0 && !cerrada) comillas = true;
            else if (c is ',' or '\n' or '\r')
            {
                fila.Add(celda.ToString()); celda.Clear(); cerrada = false;
                if (c != ',')
                {
                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                    if (fila.Any(x => x.Length > 0)) filas.Add(fila.ToArray()); fila.Clear();
                }
            }
            else { if (cerrada || c == '"') throw new FormatException("CSV con comillas inválidas."); celda.Append(c); }
            if (filas.Count > 10001) throw new FormatException("El CSV excede 10,000 filas.");
        }
        if (comillas) throw new FormatException("CSV con comillas sin cerrar.");
        if (celda.Length > 0 || fila.Count > 0) { fila.Add(celda.ToString()); filas.Add(fila.ToArray()); }
        return filas;
    }
}

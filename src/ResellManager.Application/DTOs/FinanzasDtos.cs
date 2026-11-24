namespace ResellManager.Application.DTOs;

public sealed record FilaAperturaInput(int? ClienteId, string Telefono, string Nombre, string? Apellido, decimal Deuda, string? Observacion = null);
public sealed record FilaAperturaDto(int Fila, int? ClienteId, string Cliente, string Telefono, decimal Deuda, string? Observacion, string? Error, bool Omitida);
public sealed record AperturaDto(Guid Version, bool Confirmada, DateOnly? Corte, DateTime? ConfirmadaUtc, string? UsuarioId,
    IReadOnlyList<FilaAperturaDto> Filas, decimal Total, bool PuedeConfirmar, IReadOnlyList<FilaAperturaInput>? Entradas = null);
public sealed record AjusteInput(int ClienteId, decimal Importe, string Motivo, string Clave, string? Referencia = null);
public sealed record ResultadoAjusteDto(Guid MovimientoId, decimal SaldoAnterior, decimal Cambio, decimal SaldoResultante);
public sealed record FiltroMovimientos(int? ClienteId = null, DateOnly? Desde = null, DateOnly? Hasta = null,
    string? Tipo = null, string? Referencia = null, int Pagina = 1, int Tamano = 30);
public sealed record MovimientoDto(string Id, int? ClienteId, string Cliente, string Tipo, DateOnly Fecha,
    DateTime? RegistradoUtc, decimal Importe, string? UsuarioId, string Descripcion, string Referencia, string? OriginalId);
public sealed record PaginaMovimientosDto(IReadOnlyList<MovimientoDto> Filas, int Pagina, bool HayMas, decimal? SaldoActual);
public sealed record ArchivoInformeDto(string Nombre, string TipoContenido, byte[] Contenido);

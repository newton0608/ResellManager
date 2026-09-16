namespace ResellManager.Application.DTOs;

public sealed record FiltroHistorial(string? Termino = null, DateOnly? Desde = null, DateOnly? Hasta = null)
{
    public bool RangoValido => !Desde.HasValue || !Hasta.HasValue || Desde.Value <= Hasta.Value;
}

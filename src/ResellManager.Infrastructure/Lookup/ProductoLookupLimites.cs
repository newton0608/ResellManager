using Microsoft.Extensions.Options;

namespace ResellManager.Infrastructure.Lookup;

// Estado técnico en memoria, compartido entre sesiones; nunca cachea productos ni códigos.
public sealed class ProductoLookupLimites(IOptions<ProductoLookupOptions> options, TimeProvider reloj)
{
    private sealed class Ventana
    {
        public Queue<DateTimeOffset> Minuto { get; } = new();
        public Queue<DateTimeOffset> Dia { get; } = new();
        public DateTimeOffset BloqueadoHasta { get; set; }
    }
    private readonly Dictionary<string, Ventana> ventanas = [];
    private readonly object candado = new();

    public bool IntentarConsumir(string fuente)
    {
        lock (candado)
        {
            var ahora = reloj.GetUtcNow();
            var ventana = Obtener(fuente);
            while (ventana.Minuto.TryPeek(out var fecha) && fecha <= ahora.AddMinutes(-1)) ventana.Minuto.Dequeue();
            while (ventana.Dia.TryPeek(out var fecha) && fecha <= ahora.AddDays(-1)) ventana.Dia.Dequeue();
            var upc = fuente == "UPCitemdb";
            var trial = string.IsNullOrWhiteSpace(options.Value.UpcitemdbUserKey);
            var porMinuto = upc
                ? Math.Clamp(options.Value.UpcitemdbPeticionesPorMinuto, 1, trial ? 6 : 1000)
                : Math.Clamp(options.Value.OpenFactsPeticionesPorMinuto, 1, 15);
            var porDia = Math.Clamp(options.Value.UpcitemdbPeticionesPorDia, 1, trial ? 100 : 20000);
            if (ventana.BloqueadoHasta > ahora || ventana.Minuto.Count >= porMinuto
                || (upc && ventana.Dia.Count >= porDia)) return false;
            ventana.Minuto.Enqueue(ahora);
            if (upc) ventana.Dia.Enqueue(ahora);
            return true;
        }
    }

    public void RegistrarRespuesta(string fuente, HttpResponseMessage respuesta)
    {
        var ahora = reloj.GetUtcNow();
        DateTimeOffset? hasta = respuesta.Headers.RetryAfter?.Date;
        if (respuesta.Headers.RetryAfter?.Delta is { } delta) hasta = ahora.Add(delta);
        if (respuesta.Headers.TryGetValues("X-RateLimit-Remaining", out var restantes)
            && restantes.FirstOrDefault() == "0"
            && respuesta.Headers.TryGetValues("X-RateLimit-Reset", out var resets)
            && long.TryParse(resets.FirstOrDefault(), out var unix) && unix is > 0 and < 253402300800)
            hasta = DateTimeOffset.FromUnixTimeSeconds(unix);
        if ((int)respuesta.StatusCode == 429 && hasta is null) hasta = ahora.AddMinutes(1);
        if (hasta is null) return;
        lock (candado)
        {
            var ventana = Obtener(fuente);
            var acotado = hasta > ahora.AddDays(1) ? ahora.AddDays(1) : hasta.Value;
            if (acotado > ventana.BloqueadoHasta) ventana.BloqueadoHasta = acotado;
        }
    }

    private Ventana Obtener(string fuente)
    {
        if (!ventanas.TryGetValue(fuente, out var ventana)) ventanas[fuente] = ventana = new();
        return ventana;
    }
}

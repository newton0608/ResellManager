using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;

namespace ResellManager.Infrastructure.Lookup;

public sealed class UpcitemdbProductoLookupProvider(
    HttpClient http, IOptions<ProductoLookupOptions> options, ProductoLookupLimites limites,
    ILogger<UpcitemdbProductoLookupProvider> logger) : ProductoLookupHttpProvider(http, options, limites, logger)
{
    public override string Fuente => "UPCitemdb";
    protected override bool Habilitado => Opciones.UpcitemdbHabilitado;
    protected override Uri Endpoint(string codigo) => new(
        "https://api.upcitemdb.com/prod/" + (string.IsNullOrWhiteSpace(Opciones.UpcitemdbUserKey) ? "trial" : "v1")
        + "/lookup?upc=" + Uri.EscapeDataString(codigo));
    protected override void Configurar(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(Opciones.UpcitemdbUserKey)) return;
        request.Headers.Add("user_key", Opciones.UpcitemdbUserKey);
        request.Headers.Add("key_type", "3scale");
    }

    protected override ProductoLookupRespuesta Leer(JsonElement json, string codigo)
    {
        if (json.ValueKind != JsonValueKind.Object) return new(EstadoLookupProveedor.RespuestaInvalida);
        if (Texto(json, "code") == "NOT_FOUND") return new(EstadoLookupProveedor.NoEncontrado);
        if (Texto(json, "code") != "OK" || !json.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array) return new(EstadoLookupProveedor.RespuestaInvalida);
        if (items.GetArrayLength() == 0) return new(EstadoLookupProveedor.NoEncontrado);
        var coincidentes = items.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object
            && (ProductoLookupDatos.CodigosEquivalentes(codigo, Texto(x, "ean"))
                || ProductoLookupDatos.CodigosEquivalentes(codigo, Texto(x, "upc")))).ToArray();
        if (coincidentes.Length != 1) return new(EstadoLookupProveedor.RespuestaInvalida);
        var item = coincidentes[0];
        var medidaPeso = ProductoLookupDatos.Medida(Texto(item, "weight"));
        var medidaVolumen = ProductoLookupDatos.Medida(Texto(item, "size"));
        string? imagen = null;
        if (item.TryGetProperty("images", out var images) && images.ValueKind == JsonValueKind.Array)
            imagen = images.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()).FirstOrDefault(x => Uri.TryCreate(x, UriKind.Absolute, out var uri) && uri.Scheme == "https");
        return Encontrado(new()
        {
            CodigoBarras = Texto(item, "ean") ?? Texto(item, "upc"), Nombre = Texto(item, "title"),
            Descripcion = Texto(item, "description"), Marca = Texto(item, "brand"),
            Modelo = Texto(item, "model"), Color = Texto(item, "color"),
            Talla = medidaVolumen.ContenidoMl.HasValue ? null : Texto(item, "size"),
            Presentacion = medidaVolumen.ContenidoMl.HasValue ? Texto(item, "size") : null,
            ContenidoMl = medidaVolumen.ContenidoMl, PesoGramos = medidaPeso.PesoGramos,
            CategoriaExterna = Texto(item, "category"), ImagenUrl = imagen, Fuente = Fuente
        });
    }
}

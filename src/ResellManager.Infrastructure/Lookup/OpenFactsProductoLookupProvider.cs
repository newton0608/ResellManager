using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResellManager.Application.Common;
using ResellManager.Application.DTOs;

namespace ResellManager.Infrastructure.Lookup;

public sealed class OpenFactsProductoLookupProvider(
    HttpClient http, IOptions<ProductoLookupOptions> options, ProductoLookupLimites limites,
    ILogger<OpenFactsProductoLookupProvider> logger) : ProductoLookupHttpProvider(http, options, limites, logger)
{
    public override string Fuente => "Open Facts";
    protected override bool Habilitado => Opciones.OpenFactsHabilitado;
    protected override Uri Endpoint(string codigo) => new(
        "https://world.openfoodfacts.org/api/v3/product/" + Uri.EscapeDataString(codigo)
        + "?product_type=all&lc=es&fields=code,product_name,product_name_es,generic_name,generic_name_es,brands,quantity,categories,categories_tags_es,image_front_url,image_url,product_type");

    protected override bool RedireccionPermitida(Uri destino) =>
        destino.Scheme == Uri.UriSchemeHttps && destino.Port == 443 && string.IsNullOrEmpty(destino.UserInfo)
        && destino.Host is "world.openfoodfacts.org" or "world.openbeautyfacts.org"
            or "world.openpetfoodfacts.org" or "world.openproductsfacts.org"
        && destino.AbsolutePath.StartsWith("/api/v3/product/", StringComparison.Ordinal);

    protected override ProductoLookupRespuesta Leer(JsonElement json, string codigo)
    {
        if (json.ValueKind != JsonValueKind.Object
            || Texto(json, "status") is not ("success" or "success_with_warnings" or "success_with_errors")
            || !json.TryGetProperty("product", out var producto) || producto.ValueKind != JsonValueKind.Object)
            return new(EstadoLookupProveedor.RespuestaInvalida);
        var cantidad = Texto(producto, "quantity");
        var medida = ProductoLookupDatos.Medida(cantidad);
        var categoria = Texto(producto, "categories");
        if (categoria is null && producto.TryGetProperty("categories_tags_es", out var tags) && tags.ValueKind == JsonValueKind.Array)
            categoria = string.Join(", ", tags.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()));
        return Encontrado(new()
        {
            CodigoBarras = Texto(producto, "code"),
            Nombre = Texto(producto, "product_name_es") ?? Texto(producto, "product_name"),
            Descripcion = Texto(producto, "generic_name_es") ?? Texto(producto, "generic_name"),
            Marca = Texto(producto, "brands"), Presentacion = cantidad,
            ContenidoMl = medida.ContenidoMl, PesoGramos = medida.PesoGramos,
            CategoriaExterna = categoria, ImagenUrl = Texto(producto, "image_front_url") ?? Texto(producto, "image_url"),
            Fuente = Fuente
        });
    }
}

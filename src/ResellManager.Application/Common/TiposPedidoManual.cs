using ResellManager.Domain.Enums;

namespace ResellManager.Application.Common;

public static class TiposPedidoManual
{
    public static IReadOnlyList<TipoPedido> Permitidos { get; } =
        Array.AsReadOnly(new[] { TipoPedido.Importacion, TipoPedido.Catalogo, TipoPedido.Apartado });

    public static string? Validar(TipoPedido tipo) => Permitidos.Contains(tipo)
        ? null
        : "Selecciona un tipo de pedido manual válido.";
}

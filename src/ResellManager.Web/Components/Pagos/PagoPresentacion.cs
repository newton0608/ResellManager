using ResellManager.Domain.Enums;

namespace ResellManager.Web.Components.Pagos;

public static class PagoPresentacion
{
    public static string Metodo(MetodoPago metodo) => metodo switch
    {
        MetodoPago.Efectivo => "Efectivo",
        MetodoPago.Transferencia => "Transferencia",
        MetodoPago.Deposito => "Depósito",
        MetodoPago.Tarjeta => "Tarjeta",
        MetodoPago.Otro => "Otro",
        _ => "Método no disponible",
    };
}

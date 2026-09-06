# Códigos internos y canales de venta

Estado vigente al cierre de Fase 5.10. Sustituye los pendientes históricos de revisión de códigos y Dashboard de Fase 5.7. No modifica reglas de negocio ni esquema.

## 1. Clasificación y decisión final

Un nombre de propiedad no determina su tratamiento: se revisó `CodigoInterno` en Domain, DTOs, servicios, formularios, pruebas y documentación.

| Campo / flujo | Clase | Captura y formato V1 |
| --- | --- | --- |
| Pedido normal | A: técnico del sistema | Automático: `PED-<GUID>` |
| Pedido de Venta Directa | A: técnico del sistema | Automático: `PED-VD-<GUID>` |
| Venta desde pedido | A: técnico del sistema | Automático: `VEN-<GUID>` |
| Venta Directa | A: técnico del sistema | Automático: `VEN-VD-<GUID>` |
| Compra | A: técnico del sistema | Automático: `COM-<GUID>` |
| Nombre físico del comprobante | A: técnico del sistema (no propiedad CodigoInterno) | Automático: `CMP-<GUID>.<extensión>` |
| UnidadInventario | A: técnico del sistema | Automático en CompraService: `<código-compra>-<detalle:D2>-<unidad:D3>` |
| Producto.CodigoInterno | B: referencia comercial significativa para búsqueda | Manual; conserva validación de unicidad |
| Producto.CodigoBarras | B: referencia externa | Manual y opcional |
| ComprobanteCompra.NumeroDocumento | B: documento externo | Manual y opcional |
| Pago.Referencia | B: referencia de cobro/banco | Manual y opcional |
| Proveedor.CodigoPais | B: referencia externa | Manual |

Cliente, Categoría, Proveedor, DetalleCompra, DetallePedido, DetalleVenta y Pago no tienen una propiedad `CodigoInterno`. No se agregan campos o generadores para ellos.

## 2. Por qué Producto permanece manual

`IProductoService.BuscarAsync` permite localizar un producto por su código; los listados y selectores lo presentan como referencia comercial junto al nombre. Su uso real no se limita a relacionar tablas.

Se conserva manual por ese valor para búsqueda e identificación de mercancía. No se presupone que siempre provenga de un proveedor, ni que todos los códigos existentes sean SKU externos. No hay fundamento para reemplazarlos por GUID por uniformidad estética. El código de barras permanece separado y opcional.

## 3. Generación y estabilidad

`GUID` significa los 32 caracteres hexadecimales de `Guid.NewGuid().ToString("N").ToUpperInvariant()`, sin guiones internos. No se emplean contadores de UI ni `MAX()+1`.

- `CodigosInternos` genera Pedido normal, Venta normal y Compra.
- `PedidoNuevo`, `VentaNueva` y `CompraNueva` conservan sus códigos en campos inicializados una vez por instancia del formulario, no durante cada render ni submit.
- `VentaPresentacion` conserva los prefijos y generadores directos existentes. `VentaDirectaForm` inicializa una sola vez tanto el código de pedido como el de venta.
- Si falla la creación del pedido directo se reintenta con el mismo código. Si el pedido ya quedó creado y falla la venta, se reutiliza ese pedido.
- Cambiar de modo en la misma página conserva el componente de Venta Directa. El cambio queda deshabilitado durante carga/registro para no perder el estado parcial.
- Recargar la página, cerrar el navegador o perder el circuito no constituye un reintento de la misma instancia. No se implementó recuperación persistente ni idempotencia distribuida.
- Los DTOs continúan enviando el código al servicio. Las validaciones de unicidad del backend y los índices únicos se conservan; automatizar UI no los reemplaza.

Los códigos existentes no se renumeran. Se muestran cuando aportan trazabilidad, sin exigir que la usuaria los invente.

## 4. Unidades y comprobantes: estrategia conservada

CompraService crea las unidades junto a sus detalles en la transacción existente. Para una compra `COM-` más GUID, la primera unidad tiene el sufijo `-01-001`: el ordinal de detalle y el ordinal de unidad hacen distinguibles productos y cantidades dentro de esa compra. La combinación con el código único de compra y el índice único de unidad preserva la unicidad; no se regenera al reservar, recibir, vender o cancelar.

La longitud habitual de una unidad generada con el formato actual es 43 caracteres. No se encontró una colisión conocida en el flujo V1 auditado y no se refactorizó el algoritmo. No existe input manual de código de unidad.

El archivo del comprobante se nombra al prepararlo, separado de `NumeroDocumento`. La ruta persistida es relativa: `comprobantes/CMP-...<extensión>`. Se mantienen preparación temporal, confirmación y compensación de Fase 5.8; un archivo rechazado puede requerir una nueva preparación, sin alterar el código de Compra del formulario.

## 5. CanalVenta y TipoPedido son independientes

`TipoPedido` describe la operación: Importación, Catálogo, Apartado o Venta directa. `CanalVenta` describe cómo se originó comercialmente.

Valores persistidos de CanalVenta:

```csharp
public enum CanalVenta
{
    Presencial = 1,
    WhatsApp = 2,
    Facebook = 3,
    Web = 4,
    Otro = 5
}
```

CanalVenta pertenece únicamente a Pedido. Venta lo conoce a través de su Pedido; no se duplica la columna ni la propiedad en Venta, VentaInput o VentaDto. La migración de pedidos históricos usa Otro sin inferirlo desde TipoPedido.

Ejemplos válidos: Apartado/Facebook, Catálogo/WhatsApp. La Venta Directa crea automáticamente TipoPedido.VentaDirecta con CanalVenta.Presencial, sin selector adicional.

Web es un canal conceptual disponible, no una tienda online implementada. Facebook y WhatsApp son valores manuales, no APIs integradas.

## 6. Dashboard por canal: ya implementado

Desde Fase 5.9 el Dashboard en `/` incluye los cinco canales, incluso en cero:

- Pedidos: todos excepto Cancelado.
- Cantidad y monto de ventas: solo Registrada; monto = suma de PrecioFinal.
- Ventas recientes: canal obtenido desde el pedido.

La deuda, inventario disponible y utilidad no se sustituyen por métricas de conversión inventadas. Definiciones completas en [Fase 5.9](17_Fase59_Dashboard.md); regresiones después de compra, venta, pago y cancelación en [Fase 5.10](18_Fase510_CierreV1.md).

## 7. Límites V1 / pendientes V2

Siguen fuera de esta implementación la concurrencia fuerte de saldo/reservas, la posible orquestación atómica Pedido + Venta Directa, una numeración humana más corta, roles, administración de usuarios, devoluciones, ecommerce e integraciones automáticas. Deshabilitar botones evita doble submit del formulario; no es una garantía frente a procesos o usuarios concurrentes.

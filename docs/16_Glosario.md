# Glosario

## Producto

Tipo de artículo que comercializa el negocio. Contiene datos generales como nombre, marca, categoría, códigos y `PrecioSugerido`. No representa una unidad física ni almacena costo.

## Precio sugerido

Valor de referencia/default definido en `Producto`. Debe ser no negativo y no obliga al precio real de una venta.

## UnidadInventario

Unidad física individual de un producto. Conserva código interno, costo de compra, fecha de ingreso, estado físico y una asociación de reserva opcional.

## Estado de UnidadInventario

Estado exclusivamente físico/logístico de una unidad. Valores V1:

- `Comprada`
- `EnTransito`
- `Disponible`
- `Vendida`
- `Entregada`

`Apartada` no es un valor de este enum.

## Reserva o apartado

Asociación comercial opcional desde una `UnidadInventario` hacia un `DetallePedido`. Permite identificar el pedido y cliente de la reserva sin alterar el estado físico. Si todavía no existe una unidad, la intención del apartado permanece en el pedido/detalle hasta que una unidad pueda asociarse.

## Compra

Adquisición de productos a un proveedor. Puede contener varios detalles y un comprobante. Una compra de catálogo puede no generar unidades si la mercancía no pasa por inventario físico.

## DetalleCompra

Producto, cantidad y costo unitario incluidos en una compra. Genera unidades físicas solo para los orígenes que ingresan o ingresarán al inventario.

## ComprobanteCompra

Documento opcional que respalda una compra y puede representar varios artículos.

## Proveedor

Persona o empresa que suministra productos, como Ross, Burlington, Ágora, Andrea o proveedores locales.

## Cliente

Persona que realiza pedidos y puede tener ventas, pagos, reservas y saldo pendiente.

## Pedido

Solicitud del cliente previa a la venta. Sus detalles definen productos y cantidades requeridas. Puede representar venta directa, apartado, importación o catálogo.

## DetallePedido

Producto y cantidad solicitada dentro de un pedido. Es el destino de la asociación de unidades reservadas.

## Venta

Transacción completa registrada a partir de un único pedido. Debe coincidir exactamente por producto y cantidad con el pedido. Una vez registrada no admite agregar artículos. No implica necesariamente entrega física.

## DetalleVenta

Una unidad vendida; no contiene cantidad. En inventario referencia una `UnidadInventario` y guarda snapshots de producto/costo. En catálogo puede usar `ProductoId`, `CostoUnitario` y `PrecioFinal` sin unidad física.

## PrecioFinal

Precio real de un `DetalleVenta`. Puede ser menor, igual o mayor que `Producto.PrecioSugerido` y es el valor usado para saldo e ingresos.

## Pago

Abono global de un cliente. No se asocia a una venta y no puede superar la deuda actual.

## Saldo pendiente

Suma de `PrecioFinal` de ventas `Registrada` menos pagos del cliente. No se almacena como campo fijo. Cancelar una venta se rechaza si excluirla dejaría este resultado negativo.

## Inventario

Conjunto de unidades físicas administradas por el sistema. No constituye una entidad independiente.

## Catálogo

Productos ofrecidos por proveedores externos. Un pedido de catálogo puede generar venta sin `UnidadInventario` cuando la mercancía no pasa por inventario físico.

## Compra local

Compra dentro de Guatemala cuyas unidades ingresan directamente como disponibles con fecha de ingreso.

## Importación

Compra cuyo traslado puede separar `FechaCompra` de `FechaIngreso`. Sus unidades nacen compradas y pueden pasar por tránsito antes de recepción.

## Recepción de mercancía

Caso de uso que registra la llegada física, asigna `FechaIngreso` y cambia unidades `Comprada`/`EnTransito` a `Disponible`, conservando cualquier reserva.

## Entrega

Transición de una unidad `Vendida` a `Entregada`. Una venta con unidades entregadas no admite cancelación simple.

## Devolución o cambio

Flujo futuro para revertir o sustituir productos ya entregados. Está fuera del backend V1 actual.

## Ganancia o utilidad

Diferencia `PrecioFinal - CostoUnitario` para detalles de ventas registradas. Usa el costo de la unidad o el costo de catálogo, nunca `PrecioSugerido`.

## Dashboard

Resumen de deuda, inventario disponible, pedidos pendientes, pagos y ventas recientes.

## Autenticación

Control de acceso completo pendiente para la Fase 5; no forma parte de esta rama de invariantes.

## Código de barras

Código del fabricante que identifica un producto, no una unidad física ni el identificador interno del sistema.

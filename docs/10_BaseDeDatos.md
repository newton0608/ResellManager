# Base de Datos

Este documento describe el modelo lógico de base de datos de ResellManager V1.

El diagrama se encuentra en:
`docs/diagrams/09_BaseDeDatos.drawio`

El DER y el diagrama de clases están sincronizados con los contratos vigentes descritos aquí y en la configuración EF.

## Notas generales

- El saldo del cliente no se almacena directamente; se calcula con ventas registradas y pagos.
- `FechaVenta` no se almacena en `UnidadInventario`; se obtiene desde `Venta.Fecha`.
- `Compra.Origen` define el tipo de abastecimiento. `EnvioHermano` es el identificador persistido; la UI puede mostrar «Envío del hijo».
- `ComprobanteCompra` respalda opcionalmente una compra.
- `Producto.PrecioSugerido` es una referencia comercial y no representa costo.

## Contratos técnicos vigentes

- Las propiedades se llaman `CodigoInterno` y `Producto.CodigoBarras`; no `InternalCode` ni `Barcode`.
- EF configura capacidad de 50 caracteres e índice único para `CodigoInterno` de Producto, Compra, UnidadInventario, Pedido y Venta. `Producto.CodigoBarras` es opcional y tiene longitud configurada de 100 caracteres. Son contratos del modelo; no se propone renombrar ni migrar el esquema.
- `Pedido.CanalVenta` es obligatorio, independiente de `TipoPedido` y se persiste como entero: `Presencial = 1`, `WhatsApp = 2`, `Facebook = 3`, `Web = 4`, `Otro = 5`. Venta consulta el canal mediante su pedido; no tiene una columna propia.
- `UnidadInventario.FechaIngreso` es `DateOnly?`: las importaciones nacen sin fecha de ingreso y la recepción la asigna a las unidades existentes.

## Inventario físico y reservas

- `UnidadInventario` representa una unidad física real.
- `EstadoUnidadInventario` contiene únicamente estados físicos/logísticos: `Comprada`, `EnTransito`, `Disponible`, `Vendida`, `Entregada` y `Perdida`.
- La reserva comercial se almacena en `UnidadInventario.DetallePedidoReservaId`, una FK nullable hacia `DetallePedido`.
- Una unidad sin reserva mantiene `DetallePedidoReservaId = null`.
- La FK permite obtener indirectamente el `Pedido` y `Cliente` de la reserva.
- Cancelar una reserva no modifica el estado físico de la unidad. Marcarla `Perdida` desde `Comprada` o `EnTransito` sí cambia ese estado y deja la FK de reserva en null, conservando unidad, compra y costo. Es irreversible en V1; la unidad no puede recibirse, reservarse ni venderse, y no se cuenta como recibida.

## Pedidos y ventas

- `DetallePedido` almacena producto, cantidad y precio unitario solicitado.
- Toda `Venta` tiene `PedidoId` obligatorio y único: un pedido admite cero o una venta; cada venta pertenece exactamente a un pedido. Venta Directa crea su pedido automáticamente. Cancelar la venta no elimina esa relación ni permite una segunda venta para el mismo pedido.
- Los únicos estados de `Venta` son `Registrada` y `Cancelada`; los pagos pertenecen al cliente.
- `DetalleVenta` representa una unidad vendida y no contiene campo cantidad.
- Para inventario físico, `DetalleVenta.UnidadInventarioId` referencia la unidad vendida.
- `DetalleVenta.UnidadInventarioId` puede ser null únicamente en el flujo de catálogo sin inventario físico.
- `DetalleVenta.ProductoId`, `CostoUnitario` y `PrecioFinal` conservan la información transaccional necesaria para historial y utilidad. `UnidadInventarioId`, `ProductoId` y `CostoUnitario` son nullable en el esquema; el servicio exige producto y costo al registrar catálogo, además del precio, y guarda snapshots de producto/costo también al vender inventario físico.
- La relación de una unidad con `DetalleVenta` admite `0..N` detalles a lo largo del historial cuando ventas anteriores fueron canceladas; no puede pertenecer simultáneamente a dos ventas `Registrada`. El índice de `DetalleVenta.UnidadInventarioId` no es único; el servicio valida el uso en ventas registradas. La reventa se registra mediante otro pedido.

## Catálogo

- Las compras de catálogo nunca generan `UnidadInventario` en V1; sus ventas no utilizan unidades físicas.
- Una venta de catálogo se registra con `ProductoId`, `CostoUnitario` y `PrecioFinal`.
- Los pedidos `Catalogo` no pueden tener reservas de unidades físicas.
- Los futuros porcentajes de comisión por proveedor/categoría deberán almacenarse de forma configurable y conservarse históricamente en las ventas cuando se implemente la automatización.

## Costos y utilidad

- El costo no pertenece a `Producto`.
- En inventario físico, el costo proviene de `UnidadInventario.Costo`, generado desde el detalle de compra.
- En catálogo, el costo utilizado por la venta se conserva en `DetalleVenta.CostoUnitario`.
- La utilidad se calcula con `PrecioFinal - CostoUnitario` para ventas registradas.
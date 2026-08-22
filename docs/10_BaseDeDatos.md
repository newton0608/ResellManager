# Base de Datos

Este documento describe el modelo lógico de base de datos de ResellManager V1.

El diagrama se encuentra en:
`docs/diagrams/09_BaseDeDatos.drawio`

## Notas generales

- El saldo del cliente no se almacena directamente; se calcula con ventas registradas y pagos.
- `FechaVenta` no se almacena en `UnidadInventario`; se obtiene desde `Venta.Fecha`.
- `Compra.Origen` define el tipo de abastecimiento.
- `ComprobanteCompra` respalda opcionalmente una compra.
- `Producto.PrecioSugerido` es una referencia comercial y no representa costo.

## Inventario físico y reservas

- `UnidadInventario` representa una unidad física real.
- `EstadoUnidadInventario` contiene únicamente estados físicos/logísticos: `Comprada`, `EnTransito`, `Disponible`, `Vendida` y `Entregada`.
- La reserva comercial se almacena en `UnidadInventario.DetallePedidoReservaId`, una FK nullable hacia `DetallePedido`.
- Una unidad sin reserva mantiene `DetallePedidoReservaId = null`.
- La FK permite obtener indirectamente el `Pedido` y `Cliente` de la reserva.
- El borrado de la relación de reserva no modifica el estado físico de la unidad.

## Pedidos y ventas

- `DetallePedido` almacena producto, cantidad y precio unitario solicitado.
- `Venta` tiene una relación uno a uno con `Pedido` mediante `PedidoId` único.
- `DetalleVenta` representa una unidad vendida y no contiene campo cantidad.
- Para inventario físico, `DetalleVenta.UnidadInventarioId` referencia la unidad vendida.
- `DetalleVenta.UnidadInventarioId` puede ser null únicamente en el flujo de catálogo sin inventario físico.
- `DetalleVenta.ProductoId`, `CostoUnitario` y `PrecioFinal` conservan la información transaccional necesaria para historial y utilidad.
- Una misma unidad puede aparecer en detalles históricos de ventas canceladas, pero no puede pertenecer simultáneamente a dos ventas `Registrada`.

## Catálogo

- Las compras/ventas de catálogo que no pasan físicamente por inventario no generan `UnidadInventario`.
- Una venta de catálogo se registra con `ProductoId`, `CostoUnitario` y `PrecioFinal`.
- Los pedidos `Catalogo` no pueden tener reservas de unidades físicas.
- Los futuros porcentajes de comisión por proveedor/categoría deberán almacenarse de forma configurable y conservarse históricamente en las ventas cuando se implemente la automatización.

## Costos y utilidad

- El costo no pertenece a `Producto`.
- En inventario físico, el costo proviene de `UnidadInventario.Costo`, generado desde el detalle de compra.
- En catálogo, el costo utilizado por la venta se conserva en `DetalleVenta.CostoUnitario`.
- La utilidad se calcula con `PrecioFinal - CostoUnitario` para ventas registradas.
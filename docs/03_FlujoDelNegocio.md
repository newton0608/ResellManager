# Flujo de Negocio

## 1. Importación

1. Se registra la compra de importación.
2. Las unidades físicas se crean en estado `Comprada` y sin `FechaIngreso`.
3. Opcionalmente pueden pasar a `EnTransito`.
4. Una unidad puede quedar reservada para un pedido no catálogo mientras está `Comprada` o `EnTransito`.
5. Al recibir la mercancía se registra `FechaIngreso` y la unidad pasa a `Disponible`, conservando cualquier reserva.
6. Una venta válida cambia la unidad a `Vendida`.
7. La entrega cambia la unidad de `Vendida` a `Entregada`.

## 2. Compra local

1. Se registra la compra y su fecha de ingreso.
2. Las unidades se crean directamente como `Disponible`.
3. Pueden reservarse para un pedido no catálogo o venderse desde un pedido válido.
4. La venta cambia las unidades a `Vendida` y la entrega posterior a `Entregada`.

## 3. Catálogo

1. El cliente realiza un pedido de catálogo.
2. El producto se solicita al proveedor únicamente bajo pedido.
3. El precio mostrado por el catálogo corresponde al precio final al cliente e incluye la ganancia de la vendedora.
4. El porcentaje de ganancia varía según el tipo/categoría del producto y puede depender del proveedor; los porcentajes y la base exacta de cálculo están pendientes de confirmación.
5. Cuando la mercancía no pasa por inventario físico, no se genera `UnidadInventario` ni reserva física.
6. La venta se registra mediante `ProductoId`, `CostoUnitario` y `PrecioFinal` para controlar deuda, pagos y utilidad.
7. La mercancía se entrega al cliente al recibirse según el flujo real del negocio.

## 4. Reservas y apartados

1. La intención del cliente se registra en `Pedido` y `DetallePedido`.
2. Una unidad física existente puede asociarse al `DetallePedido` mediante una reserva.
3. La reserva no modifica el estado físico de la unidad.
4. Un pedido `Catalogo` no puede reservar unidades físicas.
5. Cancelar la reserva o el pedido libera la asociación sin alterar el estado físico.
6. Una unidad reservada solo puede venderse para el pedido al que pertenece la reserva.

## 5. Cobros

1. Una venta `Registrada` genera deuda para el cliente según sus `PrecioFinal`.
2. Los pagos son globales al cliente y no pertenecen a una venta específica.
3. El saldo se calcula como ventas registradas menos pagos.
4. Un pago no puede superar la deuda actual.
5. Una venta no puede cancelarse si hacerlo dejaría saldo negativo por pagos ya registrados.

## 6. Cancelación y entrega

- Una venta registrada puede cancelarse antes de la entrega si no genera saldo negativo.
- Al cancelar, las unidades todavía `Vendida` vuelven a `Disponible` y el pedido vuelve a `Pendiente`.
- Si alguna unidad está `Entregada`, la cancelación simple se rechaza; devolución/cambio queda como flujo futuro.
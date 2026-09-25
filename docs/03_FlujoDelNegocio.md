# Flujo de Negocio

## 1. Importación

1. Se registra la compra de importación.
2. Al registrar la compra se crean sus unidades físicas en estado `Comprada` y con `FechaIngreso = null`; la recepción posterior actualiza esas mismas unidades, no crea otras.
3. Opcionalmente pueden pasar a `EnTransito`.
4. Una unidad puede quedar reservada para un pedido no catálogo mientras está `Comprada` o `EnTransito`.
5. La recepción se organiza por Compra y Proveedor. Cada confirmación recibe solo las unidades seleccionadas de una misma Compra: registra `FechaIngreso` y las pasa a `Disponible`, conservando reservas vigentes. Puede ser parcial; las pendientes de recibir permanecen `Comprada`/`EnTransito`.
6. Una venta válida cambia la unidad a `Vendida`.
7. La entrega cambia la unidad de `Vendida` a `Entregada`.

Alternativa antes de la recepción: una unidad `Comprada` o `EnTransito` puede marcarse `Perdida`, previa confirmación. Es irreversible en V1 y libera su reserva, sin reducir la cantidad solicitada del pedido ni eliminar unidad, compra, costo o historial. La unidad perdida no cuenta como recibida ni pendiente de recibir y no puede recibirse, reservarse o venderse después. No genera reembolsos ni ajustes contables automáticos.

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
5. En V1 las compras de catálogo no generan `UnidadInventario`; los pedidos no reservan inventario físico y las ventas no utilizan unidades físicas.
6. La venta se registra desde el pedido de catálogo y exige `ProductoId`, `CostoUnitario` y `PrecioFinal` para controlar deuda, pagos y utilidad.
7. La mercancía se entrega al cliente al recibirse según el flujo real del negocio.

## 4. Reservas y apartados

1. La intención del cliente se registra en `Pedido` y `DetallePedido`.
2. Una unidad física existente puede asociarse al `DetallePedido` mediante una reserva.
   Al crear el pedido, la selección explícita se limita a unidades del producto `Comprada`, `EnTransito` o `Disponible`, sin reserva ajena. Tras aceptar reservar, una única unidad elegible puede autoseleccionarse; si hay varias, se eligen hasta la cantidad solicitada. Es posible continuar sin reservar. La intención solo se persiste al confirmar, junto con pedido y detalles en una transacción con revalidación.
3. La reserva no modifica el estado físico de la unidad.
4. Un pedido `Catalogo` no puede reservar unidades físicas.
5. Cancelar la reserva o el pedido libera la asociación sin alterar el estado físico.
6. Una unidad reservada solo puede venderse para el pedido al que pertenece la reserva.
7. Si la unidad reservada A aún está `Comprada` o `EnTransito`, la venta puede usar otra unidad B compatible `Disponible`, sin reserva ajena. Al registrar la venta completa, B pasa a `Vendida` y A pierde únicamente su reserva, conservando su estado físico previo. Lo mismo aplica a una reserva `Disponible` sustituida.
8. En la misma transacción de venta se liberan todas las reservas de los detalles, no solo las de unidades vendidas, y se completa el pedido. Un pedido `Completado` termina sin reservas activas; las unidades no utilizadas no se cancelan ni eliminan.

## 5. Cobros

Los únicos valores de `EstadoVenta` son `Registrada` y `Cancelada`; los pagos no definen estados de cobro de una venta individual.

1. Una venta `Registrada` genera deuda para el cliente según sus `PrecioFinal`.
2. Los pagos son globales al cliente y no pertenecen a una venta específica.
3. El saldo se calcula como ventas registradas menos pagos.
4. Un pago no puede superar la deuda actual.
5. Una venta no puede cancelarse si hacerlo dejaría saldo negativo por pagos ya registrados.

## 6. Cancelación y entrega

- Una venta registrada puede cancelarse antes de la entrega si no genera saldo negativo.
- Al cancelar, las unidades todavía `Vendida` vuelven a `Disponible` y el pedido vuelve a `Pendiente`; no se restauran reservas antiguas.
- La venta cancelada conserva sus detalles. Una unidad puede figurar en varios `DetalleVenta` históricos, pero no en dos ventas `Registrada` simultáneamente. Para revenderla se usa otro pedido: el anterior conserva su única venta, aunque esté cancelada.
- Toda venta tiene pedido; Venta Directa lo crea automáticamente. Un pedido puede existir todavía sin venta.
- Si alguna unidad está `Entregada`, la cancelación simple se rechaza; devolución/cambio queda como flujo futuro.

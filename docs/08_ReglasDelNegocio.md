# Reglas del Negocio

## UnidadInventario
- Una UnidadInventario puede estar apartada.
- Un UnidadInventario solo puede estar disponible si llegó a Guatemala.
- Si una UnidadInventario no es apartado, puede comprarse para inventario.
- Las UnidadInventario comprados en Guatemala ingresan directamente al inventario.
- Las UnidadInventario enviados por el hijo pueden registrarse cuando se reciben, ya que el contenido final puede variar.
- Las ventas por catálogo pueden no pasar por inventario.
- Un cliente puede pagar de contado o mediante varios abonos.
- El origen de una unidad de inventario se determina a partir de la compra de la que proviene.
- Una UnidadInventario vendida no puede volver a venderse.
- Una UnidadInventario solo puede pertenecer a una venta.
- Una UnidadInventario solo puede estar en un estado a la vez.
- Una UnidadInventario apartada no puede venderse a otro cliente hasta cancelar la reserva.
- Una UnidadInventario solo puede estar asociada a un DetalleVenta una única vez.
- Un cliente puede apartar un producto antes de que sea comprado.
- Cuando el producto se adquiere, se genera la unidad de inventario correspondiente.

## Clientes
- Un cliente puede tener varias deudas.
- Un cliente puede realizar varios pagos.
- Un cliente puede tener varios apartados.


## Pagos
- Un pago nunca puede superar la deuda.
- Una deuda queda liquidada cuando el saldo llega a cero.
- Un pago reduce la deuda del cliente, no de una venta específica.

## Reservas
- Una reserva puede cancelarse.

## Ventas
- Una venta puede contener varios artículos.
- El margen de ganancia puede variar según el tipo de artículo o el proveedor.
- La ganancia de una venta se calcula a partir del costo registrado en la compra correspondiente.
- Toda venta debe originarse a partir de un pedido. Nota: En las ventas presenciales, el pedido puede generarse automáticamente por el sistema y no ser visible para el usuario.
- Una venta siempre debe estar asociada a un único pedido.
- Un pedido puede finalizar sin generar una venta si es cancelado.
- Una venta puede generar saldo pendiente para el cliente.

## Compras
- Una compra puede estar respaldada por un Comprobante de compra.

## ComprobanteCompra
- Un comprobante puede contener varios artículos.
- Los comprobantes de Ágora y Andrea pueden utilizarse para cambios únicamente durante los primeros 30 días desde la compra.

## Producto
- Un producto puede tener muchas unidades de inventario.
- El código de barras pertenece al producto y no a la unidad.
- Un producto puede publicarse antes de ser comprado.
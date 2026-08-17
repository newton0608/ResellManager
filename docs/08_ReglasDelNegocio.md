# Reglas del Negocio

## UnidadInventario
- Una UnidadInventario representa una unidad física real de un Producto.
- Una UnidadInventario solo puede estar en un estado a la vez.
- Una UnidadInventario puede estar apartada.
- Una UnidadInventario solo puede estar disponible si ya llegó a Guatemala o fue comprada localmente.
- Las UnidadInventario compradas en Guatemala ingresan directamente al inventario disponible.
- Las UnidadInventario de importación pueden pasar por estados como Comprada o En tránsito antes de estar Disponibles.
- Las UnidadInventario enviadas por el hijo pueden registrarse cuando se reciben, ya que el contenido final puede variar.
- El origen de una UnidadInventario se determina a partir de la Compra de la que proviene.
- Una UnidadInventario apartada no puede venderse a otro cliente hasta cancelar la reserva.
- Una UnidadInventario asociada a una venta registrada no puede venderse de nuevo mientras esa venta siga activa.
- Una UnidadInventario puede conservar historial en DetalleVenta si una venta fue cancelada.
- Si una venta se cancela antes de entregar el producto, la UnidadInventario puede volver a estar Disponible.
- Si una UnidadInventario ya fue Entregada, no debe liberarse automáticamente al cancelar una venta; requiere un proceso futuro de devolución o cambio.
- Un cliente puede apartar un producto antes de que sea comprado.
- Cuando el producto apartado se adquiere, se genera la UnidadInventario correspondiente.

## Clientes
- Un cliente puede tener varias deudas.
- Un cliente puede realizar varios pagos.
- Un cliente puede tener varios apartados.

## Pagos
- Un cliente puede pagar de contado o mediante varios abonos.
- Un pago nunca puede superar la deuda actual del cliente.
- Una deuda queda liquidada cuando el saldo llega a cero.
- Un pago reduce la deuda global del cliente, no de una venta específica.
- El saldo del cliente no se almacena; se calcula automáticamente a partir de ventas registradas menos pagos.

## Reservas
- Una reserva puede cancelarse.
- Una reserva debe identificar de forma clara al cliente o pedido asociado antes de implementarse completamente en interfaz.

## Ventas
- Una venta puede contener varios artículos.
- Toda venta debe originarse a partir de un pedido. Nota: en las ventas presenciales, el pedido puede generarse automáticamente por el sistema y no ser visible para el usuario.
- Una venta siempre debe estar asociada a un único pedido.
- Un pedido puede finalizar sin generar una venta si es cancelado.
- Una venta puede generar saldo pendiente para el cliente.
- Una venta registrada cuenta para el saldo del cliente.
- Una venta cancelada no cuenta para el saldo del cliente.
- Una venta no necesariamente implica que el producto ya fue entregado; la entrega se refleja en el estado de la UnidadInventario.
- Si una venta se cancela antes de entregar los productos, las unidades vendidas pueden volver a Disponible.
- Si una venta contiene unidades Entregadas, no debe cancelarse mediante cancelación simple; requiere un proceso futuro de devolución o cambio.
- El margen de ganancia puede variar según el tipo de artículo o el proveedor.
- La ganancia de una venta de inventario se calcula a partir del costo registrado en la UnidadInventario proveniente de la compra correspondiente.
- La ganancia de una venta de catálogo sin inventario se calcula a partir del CostoUnitario registrado en el DetalleVenta.

## Catálogo
- Las ventas por catálogo pueden no pasar por inventario.
- Una venta de catálogo puede registrar DetalleVenta usando Producto, CostoUnitario y PrecioFinal sin generar UnidadInventario.
- Una venta de catálogo sin inventario solo debe permitirse cuando el Pedido sea de tipo Catálogo.
- Una compra de catálogo no debe generar UnidadInventario automáticamente si el producto no pasa físicamente por el inventario del negocio.

## Compras
- Una compra puede estar respaldada por un ComprobanteCompra.
- Una compra local genera unidades disponibles directamente.
- Una compra de importación puede generar unidades compradas o en tránsito hasta su recepción.
- Una compra o recepción de envío del hijo debe registrarse cuando se conoce el contenido real recibido.
- La fecha de compra y la fecha de ingreso al inventario pueden ser distintas.

## Recepción de mercancía
- La recepción de mercancía representa el momento en que una unidad llega físicamente y puede pasar a Disponible.
- La recepción debe permitir actualizar unidades compradas o en tránsito a disponibles.
- La fecha de ingreso al inventario debe representar la fecha real de recepción, no necesariamente la fecha de compra.

## ComprobanteCompra
- Un comprobante puede contener varios artículos.
- Los comprobantes de Ágora y Andrea pueden utilizarse para cambios únicamente durante los primeros 30 días desde la compra.

## Producto
- Un producto puede tener muchas unidades de inventario.
- El código de barras pertenece al producto y no a la unidad.
- Un producto puede publicarse antes de ser comprado.

# Casos de Uso

## Clientes

- Registrar cliente.
- Buscar cliente.
- Consultar saldo.
- Consultar historial de ventas y pagos.

## Productos e inventario

- Registrar producto.
- Registrar unidad de inventario mediante los flujos de compra que generan unidades físicas.
- Consultar inventario.
- Consultar unidades disponibles.
- Buscar unidades.
- Cambiar estado físico mediante las transiciones permitidas.
- Registrar recepción de mercancía.
- Registrar entrega de una unidad vendida.

## Reservas y apartados

- Registrar pedido/apartado.
- Reservar una unidad física para un `DetallePedido` no catálogo.
- Consultar unidades con reserva activa.
- Cancelar una reserva conservando el estado físico.
- Cancelar un pedido y liberar sus reservas.
- Impedir reservas físicas para pedidos de catálogo.

## Compras

- Registrar compra.
- Registrar compra local.
- Registrar importación.
- Registrar compra de catálogo sin generar inventario físico cuando corresponda.
- Registrar recepción de envío del hijo.
- Registrar proveedor.
- Adjuntar comprobante de compra.
- Consultar compras y comprobantes.

## Pedidos y ventas

- Registrar pedido.
- Registrar venta completa desde un pedido.
- Validar correspondencia exacta de productos y cantidades entre pedido y venta.
- Registrar venta de catálogo sin `UnidadInventario`.
- Cancelar venta antes de entrega cuando no produzca saldo negativo.
- Rechazar cancelación simple si existen unidades entregadas.

## Pagos

- Registrar abono.
- Registrar pago contado.
- Consultar historial de pagos.
- Impedir pagos que superen la deuda actual.

## Dashboard y reportes

- Consultar dashboard.
- Consultar total adeudado.
- Consultar inventario disponible.
- Consultar pedidos pendientes, pagos y ventas recientes.
- Consultar utilidad por período.

## Casos de uso futuros

- Editar una venta respetando sus invariantes, si el negocio lo requiere.
- Gestionar devoluciones y cambios de unidades entregadas.
- Configurar comisiones de catálogo por proveedor/categoría cuando se confirmen los porcentajes y la fórmula real.
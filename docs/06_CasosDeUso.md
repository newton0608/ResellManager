# Casos de Uso

## Clientes

- Registrar cliente.
- Buscar cliente.
- Consultar saldo.
- Consultar ventas y pagos por meses de actividad incrementales e independientes, manteniendo el saldo global y las pendientes actuales sin límite temporal.

## Productos e inventario

- Registrar producto.
- Registrar unidad de inventario mediante los flujos de compra que generan unidades físicas.
- Consultar inventario.
- Consultar unidades disponibles.
- Buscar unidades.
- Cambiar estado físico mediante las transiciones permitidas.
- Marcar como `Perdida`, previa confirmación, una unidad `Comprada` o `EnTransito`: libera su reserva, conserva unidad/compra/costo/historial y no admite reversión, recepción, reserva ni venta posterior en V1.
- Registrar recepción parcial de una compra, sin mezclar compras ni alterar reservas vigentes.
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
- Registrar compra de catálogo sin generar unidades de inventario físico.
- Registrar recepción de envío del hijo.
- Registrar proveedor.
- Adjuntar comprobante de compra.
- Consultar compras y comprobantes.

## Pedidos y ventas

- Registrar pedido.
- Registrar venta completa desde un pedido obligatorio; Venta Directa crea automáticamente ese pedido.
- Sustituir una unidad reservada por otra compatible disponible, sin reserva ajena.
- Completar la venta liberando todas las reservas sobrantes o sustituidas del pedido, sin modificar el estado físico de las unidades no vendidas.
- Validar correspondencia exacta de productos y cantidades entre pedido y venta.
- Registrar venta de catálogo sin `UnidadInventario`, con producto, costo y precio explícitos.
- Cancelar venta antes de entrega cuando no produzca saldo negativo.
- Revender una unidad liberada por cancelación mediante otro pedido, conservando los detalles históricos y sin usarla simultáneamente en dos ventas `Registrada`.
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

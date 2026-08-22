# Requisitos

## Funcionales

- Registrar pagos al contado.
- Registrar pedidos.
- Registrar ventas completas a partir de un pedido.
- Una venta registrada no debe permitir agregar artículos posteriormente; una compra posterior del cliente debe registrarse como otro pedido y otra venta.
- La edición futura de una venta, si se implementa, debe respetar la inmutabilidad de sus artículos y los invariantes de inventario, pagos e historial.
- Cancelar una venta únicamente cuando no existan unidades entregadas y la operación no produzca saldo negativo por pagos ya registrados.
- Registrar reservas de unidades físicas para pedidos no catálogo.
- Cancelar reservas sin modificar el estado físico de la unidad.
- Consultar unidades con reserva activa.
- Un pedido de catálogo no debe reservar ni requerir unidades físicas de inventario.
- Registrar recepción de mercancía.
- Registrar compras locales.
- Registrar compras de importación, catálogo y envíos del hijo según su flujo correspondiente.
- Registrar clientes.
- Registrar productos y unidades de inventario.
- Registrar abonos.
- Buscar clientes.
- Buscar unidades.
- Consultar historial de pagos.
- Consultar historial del cliente.
- El dashboard debe mostrar el total de cuentas por cobrar y el inventario disponible.
- Ver inventario disponible.
- El saldo pendiente del cliente debe calcularse automáticamente a partir de ventas registradas menos pagos.
- Registrar compras.
- Adjuntar fotografías de `ComprobanteCompra`.
- Consultar `ComprobanteCompra`.
- Asociar un `ComprobanteCompra` a una compra.
- Calcular la ganancia de una venta.
- Consultar utilidad por período.
- Registrar proveedores.
- Consultar unidades disponibles.
- Cambiar el estado físico de una unidad únicamente mediante las transiciones permitidas.
- Registrar entrega de una unidad vendida.
- Consultar el historial de una unidad (opcional para una versión futura).
- En ventas de catálogo, registrar `ProductoId`, `CostoUnitario` y `PrecioFinal` sin generar `UnidadInventario` cuando la mercancía no pasa por inventario físico.
- Permitir configurar en el futuro porcentajes de comisión de catálogo por proveedor/categoría una vez confirmados los valores y la fórmula real del negocio.

## No funcionales

- Debe funcionar desde un celular.
- Debe ser rápido.
- Debe ser fácil de usar.
- Debe funcionar con internet lento.
- La información debe persistir aunque se cierre la aplicación.
- Debe adaptarse a diferentes tamaños de pantalla.
- Debe permitir respaldar la información.
- Debe proteger los datos mediante autenticación; la implementación completa de acceso se realizará en la Fase 5.
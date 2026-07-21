# Base de Datos

Este documento describe el modelo lógico de base de datos de ResellManager V1.

El diagrama se encuentra en:
docs/diagrams/09_BaseDeDatos.drawio

Notas:
- El saldo del cliente no se almacena directamente; se calcula con ventas y pagos.
- FechaVenta no se almacena en UnidadInventario; se obtiene desde Venta.Fecha.
- Compra.Origen define el tipo de abastecimiento.
- ComprobanteCompra respalda una compra.
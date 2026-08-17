# Glosario

## Producto

Representa el tipo de artículo que comercializa el negocio.

Contiene información general como nombre, marca, categoría y código de barras.

No representa una unidad física.

---

## UnidadInventario

Representa una unidad física de un producto.

Cada unidad posee su propio ciclo de vida y estado dentro del inventario.

Puede estar comprada, en tránsito, disponible, apartada, vendida o entregada.

---

## Compra

Proceso mediante el cual se adquieren productos a un proveedor.

Puede contener uno o varios productos.

No toda compra genera inventario automáticamente; las compras de catálogo pueden no generar unidades si el producto no pasa físicamente por el inventario.

---

## DetalleCompra

Representa cada producto incluido dentro de una compra.

Incluye cantidades, costos y referencias necesarias para generar unidades de inventario cuando la compra sí ingresa al inventario.

---

## ComprobanteCompra

Documento que respalda una compra.

Puede almacenarse mediante una fotografía.

Un comprobante de compra puede respaldar una compra con varios productos.

---

## Proveedor

Persona o empresa que suministra productos.

Ejemplos:

- Ross
- Burlington
- Ágora
- Andrea
- Compras locales

---

## Cliente

Persona que compra productos al negocio.

Puede tener pedidos, ventas, pagos y saldo pendiente.

---

## Pedido

Solicitud realizada por un cliente antes de concretar la venta.

Puede representar una venta presencial generada automáticamente, un apartado o un pedido de catálogo.

---

## Venta

Transacción registrada a partir de un pedido.

Puede ser al contado o a crédito.

Una venta registrada genera saldo para el cliente si no se paga completamente.

Una venta no necesariamente significa que el producto ya fue entregado; la entrega se controla con el estado de la UnidadInventario.

---

## DetalleVenta

Representa cada artículo incluido dentro de una venta.

En ventas de inventario, referencia una UnidadInventario.

En ventas de catálogo, puede registrar Producto, CostoUnitario y PrecioFinal sin UnidadInventario.

---

## Pago

Registro de un abono o pago realizado por un cliente.

Reduce automáticamente el saldo pendiente.

No se asocia a una venta específica, sino al cliente.

No puede superar la deuda actual del cliente.

---

## Categoría

Clasificación utilizada para organizar los productos.

Ejemplos:

Splash
Zapatos
Carteras
Ropa

---

## Inventario

Conjunto de todas las unidades de inventario disponibles para la venta.

No constituye una entidad independiente.

---

## Apartado

Reserva realizada por un cliente antes de la compra o entrega del producto.

Debe estar asociada de forma clara a un cliente o pedido antes de implementarse completamente en la interfaz.

---

## Catálogo

Conjunto de productos ofrecidos por proveedores externos como Ágora o Andrea.

Generalmente se venden bajo pedido.

Puede generar una venta sin UnidadInventario cuando el producto no pasa físicamente por inventario.

---

## Compra Local

Compra realizada dentro de Guatemala.

Las unidades ingresan directamente al inventario disponible.

---

## Importación

Compra realizada en Estados Unidos.

Las unidades pueden transportarse en equipaje o enviarse mediante caja.

La fecha de compra puede ser distinta a la fecha de ingreso al inventario.

---

## Recepción de Mercancía

Proceso mediante el cual las unidades llegan físicamente al negocio o a Guatemala y pasan a estar disponibles.

Puede aplicar a importaciones, envíos del hijo o compras que inicialmente no estaban disponibles.

---

## Dashboard

Pantalla principal del sistema.

Muestra información resumida como:

- Total por cobrar.
- Inventario disponible.
- Productos apartados.
- Indicadores del negocio.

---

## Estado de UnidadInventario

Estado actual de una unidad física.

Valores:

- Apartado
- Comprado
- En tránsito
- Disponible
- Vendido
- Entregado

---

## Código de Barras

Código asignado por el fabricante para identificar un producto.

No representa el identificador interno del sistema.

---

## Saldo Pendiente

Monto que un cliente aún debe pagar por sus compras.

Se calcula automáticamente con ventas registradas menos pagos.

No se almacena como campo fijo.

---

## Ganancia

Diferencia entre el costo registrado y el precio de venta.

En inventario se calcula usando el costo de la UnidadInventario.

En catálogo sin inventario se calcula usando el CostoUnitario registrado en el DetalleVenta.
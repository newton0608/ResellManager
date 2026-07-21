# Glosario

## Producto

Representa el tipo de artículo que comercializa el negocio.

Contiene información general como nombre, marca, categoría y código de barras.

No representa una unidad física.

---

## UnidadInventario

Representa una unidad física de un producto.

Cada unidad posee su propio ciclo de vida y estado dentro del inventario.

---

## Compra

Proceso mediante el cual se adquieren productos a un proveedor.

Puede contener uno o varios productos.

---

## DetalleCompra

Representa cada producto incluido dentro de una compra.

Incluye cantidades, costos y referencias necesarias para generar las unidades de inventario.

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

Puede representar un apartado o un pedido de catálogo.

---

## Venta

Transacción mediante la cual uno o varios productos son entregados a un cliente.

Puede ser al contado o a crédito.

---

## DetalleVenta

Representa cada unidad de inventario incluida dentro de una venta.

---

## Pago

Registro de un abono o pago realizado por un cliente.

Reduce automáticamente el saldo pendiente.

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

---

## Catálogo

Conjunto de productos ofrecidos por proveedores externos como Ágora o Andrea.

Generalmente se venden bajo pedido.

---

## Compra Local

Compra realizada dentro de Guatemala.

Las unidades ingresan directamente al inventario.

---

## Importación

Compra realizada en Estados Unidos.

Las unidades pueden transportarse en equipaje o enviarse mediante caja.

---

## Recepción de Mercancía

Proceso mediante el cual las unidades pasan de "En tránsito" a "Disponibles".

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

Se actualiza automáticamente con cada venta y cada pago.

---

## Ganancia

Diferencia entre el costo registrado de la unidad y su precio de venta.
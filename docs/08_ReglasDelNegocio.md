# Reglas del Negocio

## UnidadInventario

- Una `UnidadInventario` representa una unidad física real de un `Producto`.
- `EstadoUnidadInventario` describe únicamente el ciclo físico/logístico: `Comprada`, `EnTransito`, `Disponible`, `Vendida` o `Entregada`.
- Una unidad solo puede estar en un estado físico a la vez; una reserva no es un estado físico.
- Las unidades de compra local y de `EnvioHermano` se generan `Disponible` y con `FechaIngreso`.
- Las unidades de importación se generan `Comprada`, sin `FechaIngreso`, y pueden pasar manualmente a `EnTransito`.
- `Comprada` y `EnTransito` solo pasan a `Disponible` mediante recepción de mercancía.
- `Disponible` pasa a `Vendida` únicamente al registrar una venta válida.
- `Vendida` puede pasar manualmente a `Entregada`; solo una cancelación válida de venta puede devolverla a `Disponible`.
- `Entregada` no cambia en el flujo V1 actual.
- El origen de una unidad se obtiene desde su `DetalleCompra` y `Compra`.
- Una unidad asociada a una venta registrada no puede pertenecer a otra venta activa.
- Los `DetalleVenta` de ventas canceladas permanecen como historial.

## Reservas y apartados

- Una reserva comercial se representa mediante la asociación nullable de `UnidadInventario` con `DetallePedido`, no mediante `EstadoUnidadInventario`.
- La asociación permite conocer el `DetallePedido`, `Pedido` y `Cliente` de la reserva.
- Una unidad puede no tener reserva y solo puede tener una reserva activa a la vez.
- Pueden reservarse unidades físicas `Comprada`, `EnTransito` o `Disponible`.
- Un pedido `Catalogo` nunca puede reservar una unidad física; los demás tipos de pedido pueden hacerlo cuando el flujo lo requiera.
- La reserva no está restringida exclusivamente a pedidos `Apartado`.
- Una unidad `Vendida` o `Entregada` no puede reservarse.
- La unidad y el detalle reservado deben corresponder al mismo producto.
- Una unidad reservada no puede venderse para un pedido distinto.
- Una unidad reservada puede venderse para el pedido de su reserva; registrar esa venta consume la asociación de reserva.
- Cancelar una reserva elimina la asociación y conserva el estado físico.
- Cancelar un pedido libera sus asociaciones de reserva y no cambia estados físicos.
- Al recibir una unidad reservada se registra `FechaIngreso`, pasa de `Comprada` o `EnTransito` a `Disponible` y conserva la reserva.
- Si aún no existe una unidad física, la intención del apartado vive en `Pedido`/`DetallePedido`; la unidad puede asociarse después.

## Clientes y pagos

- Un cliente puede tener varias ventas, deudas, reservas y pagos.
- Un pago pertenece globalmente al cliente, nunca a una venta.
- Un pago debe ser mayor que cero y no puede superar la deuda actual.
- El saldo no se almacena: se calcula como la suma de `PrecioFinal` de ventas `Registrada` menos la suma de pagos.

## Pedidos y ventas

- Toda venta se origina en un único pedido. Una venta presencial puede generar su pedido técnico automáticamente en una futura UI.
- Un pedido cancelado no puede convertirse en venta.
- Los detalles del pedido agrupan la cantidad solicitada por `ProductoId`.
- Los detalles de la venta representan unidades individuales y no tienen campo cantidad.
- Al registrar una venta, las cantidades vendidas deben coincidir exactamente por producto con las cantidades del pedido.
- En inventario físico, el producto vendido se deriva de cada `UnidadInventario`; en catálogo se usa `DetalleVenta.ProductoId`.
- Solo una venta completa cambia el pedido a `Completado`.
- Una venta se registra como transacción completa y no admite agregar artículos posteriormente.
- Una compra posterior del mismo cliente requiere otro pedido y otra venta.
- Una venta `Registrada` cuenta para el saldo; una venta `Cancelada` no cuenta.
- Antes de cancelar una venta se calcula el saldo del cliente excluyéndola. Si quedaría negativo por pagos ya registrados, la cancelación se rechaza hasta ajustar o devolver esos pagos.
- Una cancelación válida conserva detalles históricos, cambia la venta a `Cancelada`, devuelve el pedido a `Pendiente` y libera a `Disponible` las unidades que siguen `Vendida`.
- Si alguna unidad está `Entregada`, la cancelación simple se rechaza y requiere un flujo futuro de devolución o cambio.

## Catálogo

- Una venta de catálogo puede registrar `DetalleVenta` con `ProductoId`, `CostoUnitario` y `PrecioFinal` sin `UnidadInventario`.
- Solo un pedido `Catalogo` permite vender sin unidad física.
- Una compra de catálogo no genera unidades automáticamente si la mercancía no pasa por inventario físico.
- En el flujo real, los productos de catálogo se gestionan bajo pedido y normalmente se entregan al cliente al recibirse, por lo que no forman inventario disponible para venta general.
- El catálogo muestra directamente el precio final al cliente.
- Dentro de ese precio final ya está incluida la ganancia o comisión de la vendedora.
- El porcentaje de comisión no es único: varía según el tipo o categoría del producto y puede depender también del proveedor de catálogo.
- Se conocen al menos grupos diferenciados de comisión para ropa y calzado, electrodomésticos y productos de limpieza como jabón o desinfectante.
- Los porcentajes exactos y la base exacta utilizada por cada proveedor para calcular la comisión están pendientes de confirmación; el sistema no debe inventar ni asumir esos valores.
- La futura automatización de comisiones debe permitir configurar reglas por proveedor y categoría.
- Si una regla de comisión cambia con el tiempo, las ventas históricas deben conservar el porcentaje y/o monto aplicado en el momento de la venta para no alterar su utilidad histórica.
- Hasta confirmar las reglas de comisión, `CostoUnitario` y `PrecioFinal` siguen registrándose explícitamente para poder calcular deuda y utilidad sin depender de porcentajes no verificados.

## Compras y recepción

- Una compra puede incluir varios detalles y un `ComprobanteCompra` opcional.
- Una compra local genera unidades disponibles directamente.
- Una importación genera unidades compradas, todavía no disponibles.
- `FechaCompra` y `FechaIngreso` pueden ser distintas.
- La recepción registra la fecha real de ingreso y cambia `Comprada`/`EnTransito` a `Disponible`.
- Recibir una unidad ya `Disponible`, `Vendida` o `Entregada` se rechaza.
- La recepción no crea unidades nuevas.

## Producto, precios y utilidad

- `Producto` describe el tipo de artículo y puede tener muchas unidades físicas.
- `Producto.PrecioSugerido` debe ser mayor o igual que cero y es solo referencia/default para captura.
- `DetalleVenta.PrecioFinal` es el precio real y puede ser menor, igual o mayor que `PrecioSugerido`.
- El costo no pertenece al producto: en inventario es un snapshot de `UnidadInventario.Costo`; en catálogo es `DetalleVenta.CostoUnitario`.
- La utilidad usa `PrecioFinal - CostoUnitario`; nunca usa `PrecioSugerido`.
- Para catálogo, una futura regla de comisión podrá ayudar a derivar o verificar la ganancia, pero no debe sustituir el historial transaccional ni recalcular ventas antiguas con porcentajes nuevos.

## Fuera del flujo V1 actual

- La autenticación está implementada desde Fase 5.1; las páginas de negocio y los comprobantes requieren sesión.
- Devoluciones y cambios requieren un caso de uso futuro y no se resuelven con transiciones manuales arbitrarias.
- La automatización de comisiones de catálogo queda pendiente hasta confirmar porcentajes y fórmula real por proveedor/categoría.
- Las reglas se mantienen en los servicios; las pantallas V1 de Fase 5 las consumen sin duplicarlas.

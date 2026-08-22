# Decisiones

## 001 La aplicación será una Web App.

Debe funcionar desde iPhone, Android y computadora sin desarrollar aplicaciones nativas.

Alternativas consideradas:
- MAUI
- Flutter
- Aplicación iOS

Resultado:
ASP.NET Core + Blazor

## 002 SQLite como base de datos inicial

Es sencilla, ligera y suficiente para la primera versión.

En el futuro:
Migrar a PostgreSQL si el proyecto crece

## 003 El sistema debe permitir múltiples orígenes de abastecimiento.

- Importación EE.UU.
- Compra local.
- Catálogo (Ágora / Andrea)
- Envíos del hijo

## 004 No desarrollar una aplicación móvil nativa.

Motivo:
Una Web App cubre todos los dispositivos.

Resultado:
Blazor.

## 005 Las ventas por catálogo pueden no generar inventario.

Motivo:
En el flujo real del negocio, las ventas por catálogo pueden gestionarse como pedido y venta sin que el producto pase físicamente por inventario.

Resultado:
Una venta de catálogo puede registrar DetalleVenta con Producto, CostoUnitario y PrecioFinal sin UnidadInventario.

Restricción:
Solo los pedidos de tipo Catálogo pueden generar ventas sin UnidadInventario.

## 006 Una compra de catálogo no genera unidades automáticamente.

Motivo:
No tiene sentido crear UnidadInventario cuando el producto de catálogo no pasa físicamente por el inventario del negocio.

Resultado:
Las compras de catálogo no deben generar UnidadInventario automáticamente, salvo que se decida registrar recepción física de mercancía.

## 007 La recepción de mercancía será un caso de uso explícito.

Motivo:
La fecha de compra y la fecha real de ingreso al inventario pueden ser distintas, especialmente en importaciones.

Resultado:
Se debe implementar un caso de uso para registrar recepción de mercancía y mover unidades compradas o en tránsito a disponibles.

## 008 Las ventas canceladas conservan historial.

Motivo:
El sistema debe conservar trazabilidad de lo que ocurrió, pero también permitir revender una unidad si la venta fue cancelada antes de la entrega.

Resultado:
Si una venta se cancela antes de entregar, sus unidades pueden volver a Disponible y los DetalleVenta quedan como historial. Si ya existen unidades Entregadas, la cancelación simple no debe permitirse y debe manejarse con un flujo futuro de devolución o cambio.

## 009 La reserva comercial no es un estado físico

Motivo:
Una unidad puede estar `EnTransito` o `Disponible` y, al mismo tiempo, reservada para un cliente. Un único enum no puede representar ambos conceptos sin perder información.

Resultado:
`EstadoUnidadInventario` conserva solo el ciclo físico y `UnidadInventario.DetallePedidoReservaId` identifica opcionalmente la reserva activa. No se crea una entidad `Reserva` independiente en V1. Los pedidos `Catalogo` no reservan inventario físico; cualquier otro tipo de pedido puede reservarlo cuando el flujo lo requiera.

## 010 Una venta se registra completa e inmutable en sus artículos

Motivo:
Agregar artículos después del registro rompe la correspondencia con el pedido y dificulta proteger inventario, saldo e historial.

Resultado:
Las cantidades vendidas deben coincidir exactamente por `ProductoId` con el pedido. Solo entonces el pedido pasa a `Completado`. Una venta registrada no permite agregar detalles; una compra posterior genera otro pedido y otra venta.

## 011 Cancelar una venta no puede producir saldo negativo

Motivo:
Los pagos pertenecen globalmente al cliente. Excluir una venta ya pagada puede dejar más pagos que deuda registrada.

Resultado:
Antes de cancelar se calcula el saldo sin esa venta. Si resulta negativo, se rechaza la operación hasta ajustar o devolver pagos. Las unidades entregadas continúan requiriendo devolución/cambio futuro.

## 012 Precio sugerido y precio real permanecen separados

Motivo:
El negocio necesita una referencia de captura sin restringir negociaciones ni distorsionar utilidad.

Resultado:
`Producto.PrecioSugerido` es referencia/default y debe ser no negativo. `DetalleVenta.PrecioFinal` es el precio real y puede diferir. La utilidad usa el costo transaccional y `PrecioFinal`.

## 013 Autenticación y devoluciones quedan fuera de esta fase

Resultado:
La autenticación completa se mantiene para la Fase 5. Devoluciones y cambios se diseñarán como casos de uso futuros; no se habilitan mediante cambios arbitrarios de estado ni mediante UI en esta rama.

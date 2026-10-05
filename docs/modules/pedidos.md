# Pedidos y reservas

**Estado: implementado.** Apartado se resuelve con Pedido + DetallePedido +
reserva de unidad; no existe una entidad Apartado ni una segunda reserva.

## Contratos y documentación canónica

- [Pedidos y ventas](../architecture/domain-rules.md#pedidos-y-ventas).
- [Reservas y apartados](../architecture/domain-rules.md#reservas-y-apartados).
- [Códigos y canales](../15_CodigosYCanalesVenta.md): TipoPedido es distinto
  de CanalVenta; Web no implica que existan pedidos desde la tienda.
- Pedido manual permite Importacion, Catalogo y Apartado. VentaDirecta es
  exclusivo del pedido técnico del flujo de venta directa.
- Al crear un pedido físico, reservar es opcional/expreso y se confirma junto
  al pedido. Catálogo bajo pedido no reserva unidades físicas.
- Pedido Completado termina sin reservas; la venta libera también las unidades
  sustituidas conservando su estado físico.

## Dónde trabajar y validar

[PedidoService](../../src/ResellManager.Infrastructure/Services/OperacionServices.cs)
y [RegistroPedidoConReservasService](../../src/ResellManager.Infrastructure/Services/RegistroPedidoConReservasService.cs)
reutilizan contratos de Application. UI en `Pages/PedidoNuevo.razor`,
`PedidoDetalle.razor`, `Pedidos.razor` y `Components/Pedidos/`, dentro de Web.

Pruebas existentes: `PedidoModuloTests`, `ReservaInventarioTests`,
`CreacionPedidoProductoTests`, `VentaInvariantesTests` y `CanalVentaTests`, en
[ResellManager.Tests](../../tests/ResellManager.Tests/).
Edición avanzada/reactivación y concurrencia fuerte son planes V2, no contratos
actuales. Para registrar/cancelar una venta lee [Ventas](ventas.md).

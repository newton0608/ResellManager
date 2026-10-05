# Inventario y recepción

**Estado: implementado.** Una unidad física conserva compra, producto, costo e
historial. Su estado físico y su reserva comercial son dimensiones distintas.

## Contratos y documentación canónica

- [Estados y transiciones](../architecture/domain-rules.md#unidadinventario).
- [Reservas](../architecture/domain-rules.md#reservas-y-apartados): asociación
  nullable con DetallePedido; liberar una reserva no cambia el estado físico.
- [Recepción](../architecture/domain-rules.md#compras-y-recepción): actualiza
  unidades existentes, permite parcialidad y no mezcla compras en una confirmación.
- La pérdida previa a recepción es irreversible en el flujo actual; no inventes
  recuperación, reembolsos ni ajustes automáticos.
- El listado Disponible y el Dashboard filtran estado físico. Eso no equivale
  a disponibilidad comercial pública, que también exige unidad libre de reserva
  y de venta registrada; consulta [Catálogo](catalogo.md) para publicación.

## Dónde trabajar y validar

[InventarioService](../../src/ResellManager.Infrastructure/Services/CompraInventarioServices.cs),
[RecepcionCompraService](../../src/ResellManager.Infrastructure/Services/RecepcionCompraService.cs)
y [SeleccionOperativaService](../../src/ResellManager.Infrastructure/Services/SeleccionOperativaService.cs)
concentran las operaciones/consultas. UI en `Pages/Inventario.razor` y
`Components/Inventario/` de Web.

Pruebas existentes en [ResellManager.Tests](../../tests/ResellManager.Tests/):
`InventarioModuloTests`, `ReservaInventarioTests`, `RecepcionPerdidasTests`,
`UnidadesPerdidasTests` y `SeleccionOperativaTests`.
La concurrencia fuerte por unidad sigue [planificada en V2.3](../19_V2_Pendientes.md#concurrencia-de-reservas-de-inventario);
deshabilitar botones no la implementa.

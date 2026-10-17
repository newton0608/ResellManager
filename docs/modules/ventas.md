# Ventas, pagos y Dashboard

**Estado: implementado.** Toda venta pertenece a un pedido; Venta Directa crea
el pedido técnico automáticamente. Las ventas son completas en sus artículos.

## Contratos y documentación canónica

- [Pedidos y ventas](../architecture/domain-rules.md#pedidos-y-ventas): cantidades,
  snapshots, reservas, cancelación y reventa conservando historial.
- [Clientes y pagos](../architecture/domain-rules.md#clientes-y-pagos): pagos
  globales del cliente, no de una venta; consulta saldo en backend.
- [Catálogo bajo pedido](../architecture/domain-rules.md#catálogo): venta sin
  unidad física y costo/precio explícitos. Es distinto del catálogo público.
- [Precio y utilidad](../architecture/domain-rules.md#producto-precios-y-utilidad)
  y [Dashboard](../17_Fase59_Dashboard.md): métricas con datos reales, no agregados
  financieros reconstruidos en Razor.
- Venta Directa conserva dos operaciones (crear Pedido y registrar Venta);
  no presupongas atomicidad conjunta ni idempotencia distribuida. Las
  protecciones de concurrencia adicionales continúan en V2.3.
- **Scanner operativo V2.1 y lotes implementados; QA físico pendiente:**
  [contrato operativo](scanner-operativo-v2-1.md). Cada acción de agregado forma
  un lote independiente con Producto `×N`, códigos físicos, precio por unidad y
  subtotal en formulario/revisión. Editar el precio afecta sus N unidades; quitar
  una conserva las restantes. La persistencia y la revalidación siguen por unidad
  física, sin nueva política de selección/costo ni migración.

## Dónde trabajar y validar

[OperacionServices](../../src/ResellManager.Infrastructure/Services/OperacionServices.cs)
contiene VentaService, PagoService y DashboardService;
[SaldoConsultas](../../src/ResellManager.Infrastructure/Services/SaldoConsultas.cs)
comparte la consulta de saldo. UI en páginas Ventas/VentaNueva/VentaDetalle,
Pagos/Home y `Components/Ventas/`, `Components/Pagos/` de Web.

Pruebas existentes en [ResellManager.Tests](../../tests/ResellManager.Tests/):
`VentaInvariantesTests`, `VentaPagoReporteTests`, `Fase57VentaPagoTests`,
`PagoBusquedaRevisionTests`, `DashboardTests`, `DashboardUxTests` y
[ScannerOperativoTests](../../tests/ResellManager.Tests/ScannerOperativoTests.cs).
La evidencia y el criterio físico pendiente del scanner se registran en el
[contrato operativo](scanner-operativo-v2-1.md#ajustes-implementados-y-validados--06102026).
Para cambios compartidos de dinero/inventario, ejecuta también la suite completa
y revisa la [decisión 015](../11_DecisionesDeDiseño.md#015-el-saldo-requiere-endurecimiento-de-concurrencia-después-de-completar-la-ui).

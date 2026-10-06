# Clientes y actividad

**Estado: implementado.** Cliente es un maestro comercial, distinto de la cuenta
Identity que inicia sesión. Nombres y teléfono son obligatorios. El módulo ofrece
alta, edición, búsqueda por nombre/teléfono, listado y detalle con saldo/actividad.

## Contratos y límites

- El saldo es global; las reglas financieras canónicas están en
  [Clientes y pagos](../architecture/domain-rules.md#clientes-y-pagos).
- El detalle consulta meses con actividad de ventas y pagos de forma independiente;
  los pendientes actuales y el saldo no se limitan a esos meses.
- No existe `Cliente.CodigoInterno`, estado activo ni operación de borrar,
  desactivar o reactivar en `IClienteService`.
- La política acordada es no eliminar físicamente clientes desde la aplicación,
  tengan o no historial: la operación prevista es **Desactivar / Reactivar**.
  Sigue **pendiente de implementación**; véase
  [decisión 025](../11_DecisionesDeDiseño.md#025-preservar-clientes-y-productos-con-historial).

## Dónde trabajar y validar

- [Contrato IClienteService](../../src/ResellManager.Application/Interfaces/Services.cs)
  y [IActividadClienteService](../../src/ResellManager.Application/Interfaces/ConsultasCierre.cs).
- [ClienteService](../../src/ResellManager.Infrastructure/Services/MasterDataServices.cs),
  [ActividadClienteService](../../src/ResellManager.Infrastructure/Services/ActividadClienteService.cs)
  y [SaldoConsultas](../../src/ResellManager.Infrastructure/Services/SaldoConsultas.cs).
- UI: `Components/Clientes/`, `Pages/Clientes.razor`, `ClienteDetalle.razor` y
  `ClienteEdicion.razor`, dentro de Web.
- Pruebas existentes: `ClienteModuloTests`, `EstadoClienteTests`,
  `ClientesFiltroVisibleTests` e `HistorialConsultaTests`, en
  [ResellManager.Tests](../../tests/ResellManager.Tests/).

Para una tarea de pagos lee también [Ventas](ventas.md). El nombre
`EstadoClienteTests` no significa que exista borrado lógico.

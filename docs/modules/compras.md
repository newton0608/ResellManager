# Compras, proveedores y comprobantes

**Estado: implementado**, incluidos GTQ/USD y referencia opcional de Banguat.
GTQ continúa siendo la moneda base del resto del negocio.

## Contratos y documentación canónica

- [Compras y recepción](../architecture/domain-rules.md#compras-y-recepción) y
  [flujo por origen](../03_FlujoDelNegocio.md): importación crea unidades Comprada;
  local/EnvioHermano, Disponible con ingreso; Catalogo nunca crea inventario físico.
- [Fase 5.8](../16_Fase58_ComprasYComprobantes.md): proveedor, detalles,
  comprobante opcional privado y estrategia de preparación/compensación.
- [Monedas de compra](../28_MonedasDeCompra.md): costos de origen, tipo aplicado
  congelado, redondeo en backend, compatibilidad histórica y fallback manual.
- Flete, courier, impuestos y prorrateo no están incluidos en esa conversión:
  [landed cost sigue en análisis](landed-cost.md).
- **Scanner en Nueva compra implementado; QA físico pendiente:** ver
  [contrato operativo](scanner-operativo-v2-1.md#compras--integración-aprobada-tras-probar-preview).
  Un código local selecciona el Producto en el detalle y conserva el modelo
  Producto + Cantidad + Costo unitario. Si no existe, puede ofrecer **Registrar
  producto** reutilizando el alta asistida existente; sólo ese subflujo explícito
  puede consultar Open Facts/UPCitemdb. Al guardar, el Producto nuevo vuelve
  autoseleccionado al detalle original sin perder la Compra en preparación.
  Cancelación/fallo conservan el formulario y el comprobante; evidencia en el
  [cierre de ajustes](scanner-operativo-v2-1.md#ajustes-implementados-y-validados--06102026).

## Dónde trabajar y validar

- [CompraService](../../src/ResellManager.Infrastructure/Services/CompraInventarioServices.cs)
  es autoridad sobre importes y generación de unidades.
- [RegistroCompraConComprobanteService](../../src/ResellManager.Infrastructure/Services/RegistroCompraConComprobanteService.cs)
  coordina el archivo privado; no implementes su flujo en Razor.
- [ProveedorService](../../src/ResellManager.Infrastructure/Services/MasterDataServices.cs),
  [ConversionMonedaCompra](../../src/ResellManager.Application/Common/ConversionMonedaCompra.cs)
  y [TipoCambioReferenciaBanguatService](../../src/ResellManager.Infrastructure/Services/TipoCambioReferenciaBanguatService.cs).
- UI: `Pages/CompraNueva.razor`, `CompraDetalle.razor`, `Compras.razor`,
  `Components/Compras/` y `Components/Proveedores/`, dentro de Web.
- Pruebas existentes: `CompraInventarioTests`, `CompraMonedasTests`,
  `CompraMonedasMigracionTests`, `TipoCambioReferenciaBanguatTests`,
  `CompraMonedaUiTests`, `Fase58AlmacenamientoComprobantesTests` y
  [ScannerOperativoComprasTests](../../tests/ResellManager.Tests/ScannerOperativoComprasTests.cs), en
  [ResellManager.Tests](../../tests/ResellManager.Tests/).

Las pruebas de Banguat usan respuestas falsas; no necesitan Internet.
Si cambias recepción, lee [Inventario](inventario.md).

# Moneda de origen en compras

## Alcance y moneda base

GTQ sigue siendo la moneda base del negocio. Compras admite únicamente `MonedaCompra.GTQ = 0` y `MonedaCompra.USD = 1` como moneda de origen. `OrigenCompra` describe abastecimiento y es independiente: cualquier origen puede usar GTQ o USD. Importación solo muestra una sugerencia editable en la UI.

Ventas, pedidos, pagos, saldos, Dashboard, precios de venta, utilidad e inventario siguen en GTQ. Catálogo no genera unidades físicas; sus detalles de compra también conservan el costo convertido a GTQ.

## Contratos e importes

| Campo | Significado |
| --- | --- |
| `Compra.Moneda` | Moneda en la que se capturan los costos |
| `Compra.TipoCambio` | GTQ por una unidad de moneda de origen; exactamente 1 para GTQ |
| `Compra.TipoCambioReferencia` | Sugerencia externa opcional, separada del tipo aplicado |
| `Compra.FechaTipoCambioReferencia` | Fecha efectiva de la sugerencia, nunca posterior a FechaCompra |
| `Compra.FuenteTipoCambio` | Fuente opcional de esa referencia |
| `Compra.TotalMonedaOrigen` | Suma de Cantidad × CostoUnitarioMonedaOrigen |
| `Compra.Total` | Suma de Cantidad × CostoUnitario GTQ |
| `DetalleCompra.CostoUnitarioMonedaOrigen` | Costo original capturado |
| `DetalleCompra.CostoUnitario` | Costo convertido a GTQ |
| `UnidadInventario.Costo` | El mismo costo unitario GTQ del detalle |

La conversión definitiva ocurre en `CompraService`, con el helper compartido `ConversionMonedaCompra.CostoUnitarioGtq`:

```csharp
decimal.Round(costoMonedaOrigen * tipoCambio, 2, MidpointRounding.AwayFromZero)
```

Después se suman los costos unitarios redondeados multiplicados por cantidad. Nunca se convierte el total original independientemente. Ejemplo: US$12.50 × 7.67 = Q95.875, costo asignado Q95.88; dos unidades totalizan Q191.76. Una venta de Q175.00 con esa unidad tiene utilidad Q79.12.

Los nuevos costos originales aceptan hasta dos decimales. Costos y totales nuevos deben estar entre 0 y 99,999,999.99, coherente con decimal(10,2); importes históricos fuera de esa escala no se alteran. Se usan `decimal` y validaciones en el servicio: moneda/origen reconocidos, USD con tipo positivo, GTQ con tipo exactamente 1, costos no negativos y operaciones sin desbordamiento. `decimal` no representa NaN ni infinito. Los importes mantienen precisión EF (10,2); el tipo de cambio tiene precisión EF (18,8) y se almacena como TEXT en SQLite para preservar el decimal exacto. SQLite no impone esa precisión física y no sustituye las validaciones de aplicación.

El tipo aplicado se congela al registrar la compra. Ni una consulta futura ni la cotización del día pueden modificar compras registradas. Una referencia diferente del costo bancario real es válida: una referencia 7.64136 y un tipo usado 7.78 calculan inventario con 7.78.

## Compatibilidad de datos

La migración `20261003155826_AddPurchaseCurrencies` agrega columnas y hace backfill SQL directo:

- compras anteriores: GTQ, TipoCambio=1 y TotalMonedaOrigen=Total;
- detalles anteriores: CostoUnitarioMonedaOrigen=CostoUnitario;
- Total, CostoUnitario y UnidadInventario.Costo anteriores permanecen intactos, incluso si contienen más de dos decimales.

No se reconstruyen tablas para el upgrade ni se recalculan históricos. El test de migración aplica la versión anterior sobre SQLite con datos, captura valores antes/después y verifica la preservación de compras, detalles e inventario.

## Referencia Banco de Guatemala

La Web depende de `ITipoCambioReferenciaService` de Application. Infrastructure usa un typed HttpClient a `https://www.banguat.gob.gt/variables/ws/TipoCambio.asmx`, sin API key ni credenciales. Application recibe un resultado normalizado con moneda, fecha solicitada, fecha efectiva, valor y fuente; no recibe SOAP/XML.

La fuente visible es **Banco de Guatemala**. Toda referencia es una sugerencia opcional. GTQ no consulta el proveedor.

- Día actual: operación [`TipoCambioDia`](https://www.banguat.gob.gt/variables/ws/TipoCambio.asmx?op=TipoCambioDia), campo explícito `CambioDolar/VarDolar/referencia`.
- Histórico: operación [`TipoCambioRango`](https://www.banguat.gob.gt/variables/ws/TipoCambio.asmx?op=TipoCambioRango), únicamente registros USD (`moneda=2`). El contrato entrega `compra`/`venta`. Se acepta un valor único solo cuando ambos son positivos e idénticos. Si difieren, no se elige un lado ni se promedia: referencia no disponible y entrada manual.

Esta interpretación conservadora se apoya en la [resolución del TCR](https://www.banguat.gob.gt/tipo_cambio/cambio/cambio.pdf), que identifica las referencias de compra/venta como un único tipo de cambio de referencia. El [historial del Web Service](https://banguat.gob.gt/tipo_cambio/TipoCambio/OtrosServicios) documenta la publicación de referencia desde 30/11/2006 y el uso exclusivo de HTTPS desde 22/04/2024.

Para fechas sin publicación se consulta un rango de siete días previos y se elige la fecha publicada más reciente menor o igual a la fecha solicitada; se muestra siempre esa fecha efectiva. No se toman valores posteriores ni se inventa una referencia histórica.

El cliente tiene timeout aproximado de 4 segundos, respeta CancellationToken y no realiza retries agresivos. Parseo numérico invariant y fechas con formato explícito del contrato (`dd/MM/yyyy`). XML con DTD prohibida y resolver nulo; tamaño de respuesta limitado a 256 KiB, incluso sin Content-Length. No se siguen redirecciones que puedan degradar HTTPS. Se registran fallos con ILogger sin mostrar detalles técnicos en la UI.

La caché en memoria guarda solo resultados exitosos, con clave moneda/fecha: 15 minutos para hoy y 30 días para históricos. No se persiste en SQLite ni se cachean errores. El día se determina en America/Guatemala mediante TimeProvider.

Timeout, DNS, HTTP no exitoso, SOAP Fault, XML inválido/vacío, fecha inexistente o valor inválido producen un resultado controlado. La compra sigue disponible con el mensaje: «No pudimos consultar la referencia del Banco de Guatemala. Ingresa el tipo de cambio manualmente.»

## Formulario y revisión

GTQ es inicial, usa Q y TipoCambio=1; no muestra referencia ni sección de conversión. USD muestra costos y total originales con US$, equivalentes GTQ por unidad redondeada, referencia/fecha efectiva y tipo aplicado editable. «Usar referencia» permite adoptar explícitamente la sugerencia.

Cambiar FechaCompra en USD vuelve a consultar la referencia. Si el usuario ya modificó el tipo aplicado, se conserva; solo se actualiza la sugerencia. Las consultas se cancelan y se identifican por generación para que una respuesta anterior no sobrescriba la fecha vigente. Cantidad, costo y tipo recalculan la vista localmente sin llamar a Banguat. El resto del formulario sigue disponible mientras carga.

La revisión USD muestra moneda, tipo aplicado, referencia opcional y fecha efectiva, costos/subtotales en ambas monedas y total registrado GTQ. Explica que inventario y utilidad utilizarán el costo convertido a GTQ. Lista y detalle conservan ambas cantidades sin cambiar filtros ni agrupación mensual.

## Fuera del alcance

EUR u otras monedas, flete/courier/impuestos/aranceles/prorrateo, costo puesto en Guatemala, edición de compras registradas, conversión de ventas/precios/pagos/saldos y actualización automática de históricos.

## Validación

La suite Banguat usa HttpMessageHandler falso y fixtures XML; no depende de Internet. Las consultas reales se reportan por separado y nunca son requisito del build.

Validación de esta entrega (03/10/2026):

- `npm run css:build`: correcto.
- `dotnet build -c Release`: correcto, cero errores y advertencias.
- `dotnet test -c Release --no-build`: 670 aprobadas, cero fallos/omisiones; 100 casos nuevos (31 dominio, 1 migración, 49 Banguat y 19 UI).
- Cuatro suites JavaScript existentes: 116 aprobadas, cero fallos (scanner, feedback, revisión y reconexión).
- QA navegador Edge/Chromium con SQLite y referencia falsas aisladas: 37 capturas/comprobaciones en 320, 390 y 1440 px, sin overflow horizontal; GTQ/USD, loading, fallback, fecha rápida, tipo manual, Usar referencia, múltiples detalles, revisión con/sin referencia, guardado real y listado/detalle/inventario. Touch y teclado/focus verificados, botones de al menos 44 px. La emulación no sustituye una prueba física Safari/iPhone.
- EF `has-pending-model-changes` correcto; [SQL del upgrade](20261003_AddPurchaseCurrencies.sql) revisado y probado con importes históricos.

Prueba externa manual separada: POST HTTPS `TipoCambioDia` respondió fecha 03/10/2026 y referencia 7.64136; `TipoCambioRango` (25/09/2026..02/10/2026) devolvió para 02/10/2026 compra=venta=7.64136. Esta comprobación no pertenece a la suite ni condiciona el build.

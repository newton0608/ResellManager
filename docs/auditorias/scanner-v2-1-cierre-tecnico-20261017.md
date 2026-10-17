# Scanner V2.1 — cierre técnico del plan, 17/10/2026

**Estado: correcciones verificadas e integradas en `develop` y `main` tras la
validación física confirmada por el usuario en iPhone 14 Plus con Brave el
17/10/2026. Sin afirmar despliegue.** Este registro continúa el
[diagnóstico original](scanner-v2-1-integration-audit-20261017.md); sus resultados
de fallo se conservan como evidencia anterior a la corrección.

## Correcciones y cobertura

### Compilación Razor

`ProductoForm.razor` compara las mismas firmas RIFF/WEBP mediante arrays de bytes,
siguiendo el patrón de PNG ya existente, en lugar de literales con sufijo `u8`
dentro del bloque Razor. Se elimina el RZ1000 observado con SDK 10.0.112 sin
cambiar formatos admitidos, límites, procesamiento ni validación del servidor.
No se cambió SDK, framework, esquema ni dependencias para corregirlo.

### Alta asistida con galería y código registrado durante la edición

La prueba nueva reprodujo un defecto adicional: después de abrir el alta por
scanner y preparar fotos manuales, otra operación podía registrar ese código;
el guardado de galería no activaba la comprobación de duplicados propia del alta
asistida. El producto podía guardarse y autoseleccionarse pese al código existente.

`ProductoAltaPanel` ahora activa el parámetro existente `validarCodigoBarras` de
`CrearGaleriaAsync` cuando el lookup está habilitado. El servicio vuelve a
comprobar el código, también dentro de su transacción. La prueba falla antes del
cambio y pasa después: no se crea otro Producto, no quedan fotos persistidas y
la Compra sigue intacta. No se introduce una garantía global nueva de concurrencia
ni se cambia la semántica del alta manual anterior.

Las cuatro pruebas de
[ScannerOperativoGaleriaTests](../../tests/ResellManager.Tests/ScannerOperativoGaleriaTests.cs)
reutilizan el renderizador del scanner, SQLite sintético y almacenamiento temporal
real. Cubren:

- alta aceptada con galería PNG/WebP, portada elegida y subcategoría;
- fallo de validación seguido de reintento, sin duplicados ni archivos parciales;
- código registrado entre consulta inicial y guardado con galería;
- cancelación después de preparar fotos, conservando Compra y comprobante.

En éxito se conserva el detalle original, cantidades, costo, moneda/tipo aplicado,
observaciones, proveedor y el mismo archivo de comprobante. La imagen manual
tiene prioridad; no se descarga la imagen externa y no se genera inventario antes
de confirmar la Compra.

### CSS

CSS regenerado con el lockfile existente y conservado en el repositorio.
SHA-256: `66058860fa438390da4c2bf453a10d57583943c966e0d34ef0e80cd1f1b5acdd`.
Coincide con la generación temporal del diagnóstico. Se retiran las cuatro
reglas sobrantes allí identificadas. El bundle Quagga, su licencia y
package-lock.json permanecen sin cambios.

## Validación final

Entorno: Ubuntu 26.04 x64; .NET SDK 10.0.112/runtime 10.0.12; Node 24.21.0;
Playwright 1.64.1 con Chromium **156.0.8078.4** headless. Playwright y sus navegadores
son herramientas temporales de QA, fuera de las dependencias de la aplicación.

| Validación | Resultado |
| --- | --- |
| Compilación de ResellManager.sln | Correcta: 0 errores y 0 advertencias. |
| ScannerOperativo + CompraProductoFlujo + CompraRevisionProveedor + ProductoCategoriaDependiente + ProductoLookupPreview | 106 pruebas correctas, sin omisiones. |
| Suite .NET completa | 1.037 pruebas correctas, sin fallos ni omisiones. |
| Restauración npm y generación de CSS | Correctas; asset generado conservado. |
| `npm run test:js` | Correcto; reporte agregado de 7 archivos. |
| Mismos tests JS con `--test-isolation=none` | 134 casos correctos, incluidos 98 del scanner. |
| QA óptico | 80 comprobaciones y 28 decodificaciones, sin errores JS. |
| QA operativo estático | 84 vistas: 21 estados × 320/390/768/1440 px; sin overflow ni controles menores de 44 × 44 px. |
| QA del catálogo | 193 comprobaciones correctas. |
| QA operativo con circuito real | Recorrido completo correcto; cinco hitos funcionales y capturas a 390/1440 px sin overflow ni errores JS. |

El nuevo
[recorrido interactivo](../../tests/scanner-operativo-interactivo.browser.mjs)
arranca una instancia local con base y archivos temporales. Prepara maestros por
la UI, autentica un usuario sintético y usa el decoder real sobre `ean.jpg`:

1. Compra: cancelar scanner/alta conserva datos; fotografía de código inexistente
   abre explícitamente el alta y permite galería de dos fotos, portada y subcategoría.
2. Guardar regresa al detalle, con cantidad 5, costo Q8.25 y comprobante intactos.
3. Inventario, en otro circuito autenticado, abre ese Producto antes de existir
   unidades físicas, conservando la Compra abierta.
4. Confirmar la Compra registra las unidades y el comprobante mediante servicios reales.
5. Venta Directa escanea de nuevo, agrega dos unidades y luego una; el segundo
   escaneo muestra tres restantes. Conserva dos lotes con precios Q75 y Q65,
   revisa y registra Q215 por sus tres unidades físicas.

Los proveedores externos están deshabilitados en esa instancia: el camino de
candidato externo y su prioridad de imagen se verifica con proveedores falsos en
.NET. **Circuito real y fotografía de fixture no equivalen a cámara física.**

Evidencia local no versionada:

- `.artifacts/scanner-qa/report.json` y capturas ópticas;
- `.artifacts/scanner-operativo-ui/report.json` y sus 84 capturas;
- `.artifacts/catalogo-qa/1792277468826/report.json`;
- `.artifacts/scanner-operativo-interactivo/1792277772965/report.json` y capturas;
- resultados TRX del área y suite en `tests/ResellManager.Tests/TestResults/`.

Se inspeccionaron capturas móviles de lotes/alta y de escritorio con el circuito
real. No se utilizó producción, datos reales ni infraestructura de despliegue.

## Validación física comunicada por usuario — 17/10/2026

El usuario confirmó haber realizado la validación física de la rama corregida
en **iPhone 14 Plus con navegador Brave**, después de su publicación en GitHub.
Esta confirmación cierra la aceptación física pendiente para ese entorno y
acompaña la integración solicitada en `develop` y `main`.

La evidencia es declarada por el usuario. No se comunicaron versiones exactas
de iOS/Brave, desglose de casos por flujo ni SHA de la instancia probada; no se
añaden esos datos ni se certifica Safari u otros dispositivos/navegadores.
Los resultados automatizados anteriores se conservan con su alcance original.

## Estado del plan

- [x] Recuperar compilación y diferenciar el bloqueo previo de la integración del scanner.
- [x] Validar área y suite .NET completa.
- [x] Cubrir scanner + alta asistida + galería, fallo/reintento/cancelación y código concurrente.
- [x] Regenerar y conservar CSS sin cambiar vendor.
- [x] Completar QA óptico, operativo, catálogo y circuito real con datos sintéticos.
- [x] Reconciliar contrato, planificación e historial, conservando evidencia histórica.
- [x] Aceptación física de la rama corregida confirmada por el usuario en iPhone
  14 Plus con Brave el 17/10/2026; alcance y metadatos disponibles registrados arriba.

El diagnóstico documental se integró en develop antes de estas correcciones.
La rama corregida se publicó en GitHub y se integró en `develop` y `main` tras
la aceptación física, sin conflictos. No se creó tag/release ni se inició despliegue.

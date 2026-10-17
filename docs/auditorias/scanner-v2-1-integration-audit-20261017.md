# Verificación de develop y scanner V2.1 — 17/10/2026

**Seguimiento posterior:** el [cierre técnico del plan](scanner-v2-1-cierre-tecnico-20261017.md)
registra las correcciones, la aceptación física en iPhone 14 Plus con Brave y
su integración en `develop` y `main`. Este documento conserva el diagnóstico original anterior a esos cambios.

## Resultado y alcance

**La integración existe en Git, pero su validación técnica está bloqueada por
la compilación de Web en el entorno auditado.** No hay conflictos Git pendientes.
Esta revisión identifica hallazgos y propone su cierre; no implementa correcciones
de código ni certifica producción.

Se realizó `fetch`, sin `pull`, merge, push ni despliegue. Se conservaron los
archivos locales sin seguimiento `MEMORY.md` e `ia/`. El único cambio de fuentes
de esta revisión es documental.

## Referencias verificadas

| Referencia | SHA | Estado observado |
| --- | --- | --- |
| `develop` y `origin/develop` | `0c7cead714d4b7a99c46fc9463ddfa4195378ee5` | Coinciden tras fetch; incluye el scanner operativo. |
| Integración del scanner | `cda41f4c620e20c31435f6c0335546e007f2d7d3` | Combina `cd015d6` con `871dab8`. |
| Feature del scanner | `871dab8` | Incorporada al historial de develop. |
| `origin/main` | `1658bea` | Referencia remota de release; no contiene esta integración. |
| `main` local | `7cca3e5` | Atrasada 118 commits respecto de origin/main; no es una base actual para esta comparación. |

La comparación de historiales remotos muestra un commit exclusivo de main y
quince de develop. El exclusivo de main es el merge documental `1658bea`; develop
contiene el cierre equivalente mediante `cd015d6`. Esa divergencia de historial
no demuestra pérdida de funcionalidad ni exige un merge en esta revisión.

El diff de árboles entre origin/main y develop coincide con el alcance neto de
la integración: 27 archivos, 1.980 inserciones y 203 eliminaciones. Se conservan
las implementaciones previas del catálogo y de la API temporal. El scanner no
modifica Domain, migraciones, Program.cs, motor JS ni vendor óptico.

## Hallazgos

### H1 — Alta: compilación de Web bloqueada

Con SDK .NET **10.0.112**, runtime **10.0.12**, Ubuntu **26.04 x64**, la compilación
integrada termina con **282 errores y 0 advertencias**. Los primeros diagnósticos
son `RZ1000` en
[ProductoForm.razor](../../src/ResellManager.Web/Components/Productos/ProductoForm.razor),
líneas 397–398: literales `"RIFF"u8` y `"WEBP"u8` en la detección de formato.
Después aparecen errores de generación C# y miembros inexistentes en cascada;
la cifra total no representa 282 defectos independientes.

Ese bloque procede de `1823e7e` (galería V1.4), anterior al scanner operativo,
y es idéntico en origin/main y develop. No corresponde atribuirlo automáticamente
a una resolución incorrecta del merge del scanner. La compatibilidad de esa
sintaxis con el parser Razor del SDK utilizado debe resolverse antes de validar
los flujos integrados. El repositorio no fija SDK mediante global.json.

La compilación de una copia temporal de **origin/main (`1658bea`) reproduce los
mismos dos errores iniciales RZ1000**, con 278 errores totales y 0 advertencias,
usando el mismo SDK. Esto confirma un bloqueo previo a la integración, observable
en este entorno. No demuestra un fallo en la imagen de producción ni descarta
otros problemas que sólo podrán detectarse tras recuperar la compilación.

### H2 — Media: resoluciones históricas pendientes de regresión integrada

La inspección de las resoluciones del merge existente identifica conflictos de
contenido ya resueltos en `CHANGELOG.md`, `docs/README.md`, `package.json`,
`ProductoAltaPanel.razor` y el CSS generado. No quedan entradas sin fusionar en
el índice ni marcadores de conflicto en src/tests.

La resolución de
[ProductoAltaPanel.razor](../../src/ResellManager.Web/Components/Productos/ProductoAltaPanel.razor)
conserva el callback de preparación, el bloqueo de cancelación mientras se
preparan fotos y el guardado de galería V1.4; incorpora el código inicial,
lookup explícito y aviso de imagen del scanner. `package.json` conserva tanto
QA de catálogo como QA operativo.

Esto es evidencia de inspección, no de ejecución correcta. Las pruebas existentes
de galería y scanner cubren caminos separados; queda por demostrar la combinación
**alta desde scanner + galería manual + portada/subcategoría + retorno al detalle
de Compra con comprobante intacto**, incluidos fallo y reintento. No se confirma
un defecto funcional en esa combinación durante esta auditoría.

### H3 — Media: CSS versionado distinto de una generación limpia

La generación con las dependencias del lockfile termina correctamente. El resultado
se conservó en `/tmp/resellmanager-scanner-audit-tailwind.css` para comparar sin
sustituir el asset versionado.

| CSS | Bytes | SHA-256 |
| --- | --- | --- |
| Versionado | 69.239 | `71a795adfe11b4b12f7ae353fde54b74cb77bdb16b1a777f48383e39ac2667a8` |
| Generado en esta revisión | 68.989 | `66058860fa438390da4c2bf453a10d57583943c966e0d34ef0e80cd1f1b5acdd` |

Al normalizar saltos de línea, la diferencia consiste en cuatro reglas sobrantes
en el versionado: `sm:grid-cols-[minmax(0,1fr)_10rem]`,
`xl:grid-cols-[minmax(0,1fr)_12rem_auto]`, `xl:items-end` y `xl:self-center`.
No se encontraron reglas nuevas ausentes del CSS integrado mediante esta
comparación. Por tanto, se confirma falta de sincronización reproducible del
asset, sin demostrar un defecto visual por falta de estilos del scanner.
El vendor de Quagga permanece sin cambios tras su sincronización.

### H4 — Media: evidencia actual insuficiente para cerrar QA integrado

Las cifras de 878 pruebas .NET, 115 JS y 84 vistas del contrato corresponden a la
validación histórica del 06/10/2026, anterior a la integración con el catálogo
reciente. No prueban el árbol de develop auditado.

La aceptación funcional comunicada por el usuario el 17/10/2026 se conserva:
recuerda haber probado Venta Directa, Inventario y Nueva compra. Sigue faltando
bitácora reproducible por flujo/dispositivo/versiones. La comprobación anterior
de Producto en iPhone 14 Plus + Brave no certifica Safari ni se extrapola a otros
consumidores. No se solicita repetir esa aceptación como condición de este informe.

### H5 — Baja: estados documentales de integración obsoletos

ROADMAP y Backlog todavía presentaban V2.1 como independiente/sin integrar,
mientras CHANGELOG y Git ya registraban su incorporación a develop. Esta revisión
reconcilia ese estado y enlaza el diagnóstico actual sin reescribir resultados
históricos ni dar por terminada la validación pendiente.

## Validaciones realizadas

| Comprobación | Resultado de esta revisión |
| --- | --- |
| Fetch, comparación de referencias/árboles y resoluciones históricas | Completados; sin pull ni merge. |
| Índice Git y marcadores en src/tests | Sin conflictos pendientes ni marcadores. |
| Compilación de la solución integrada | Falla con RZ1000 y errores en cascada; detalle en H1. |
| Compilación de origin/main en copia temporal | Reproduce los mismos RZ1000; 278 errores y 0 advertencias. |
| Pruebas .NET del área y suite completa | No ejecutadas: el proyecto Web necesario no compila. |
| `npm ci` | Correcto al disponer de red; el primer intento aislado falló por DNS. |
| `npm run test:js` | Correcto; el runner informó 7 archivos satisfactorios y 0 fallidos. |
| Generación CSS con salida temporal | Correcta; diferencia descrita en H3, sin modificar CSS versionado. |
| `npm run qa:scanner-operativo` | No arrancó: falta el paquete Playwright en este entorno. |
| QA óptico, catálogo, circuito Blazor real y cámara física | No ejecutados; build y herramientas pendientes. |
| Producción, Docker y datos reales | No inspeccionados ni utilizados. |
| Enlaces locales y revisión documental | 122 destinos existentes; diff revisado y sin errores de espacios. |

## Plan propuesto y criterios de cierre

El orden expresa dependencias técnicas; las correcciones de producto siguen
pendientes y no forman parte de esta revisión documental.

| Orden / prioridad | Trabajo pendiente | Criterio de aceptación |
| --- | --- | --- |
| 1 / Alta | Resolver la compatibilidad Razor del bloque WebP y contrastar el SDK local con el de validación habitual. Mantener firmas admitidas y validación de imágenes. | Solución integrada compilable sin errores/advertencias; causa identificada y diferenciada de un defecto del scanner. |
| 2 / Alta | Validar ScannerOperativo, CompraProductoFlujo, CompraRevisionProveedor y ProductoCategoriaDependiente; después la suite completa. | Todas las pruebas vigentes pasan; cualquier fallo queda atribuido y cubierto sin relajar expectativas. |
| 3 / Media | Completar cobertura de la combinación scanner/alta asistida/galería V1.4 según H2. | Alta y retorno al detalle original preservan fotos, portada, categoría, cantidades, costos, moneda y comprobante; cancelación y fallo no dejan guardados parciales ni duplicados. |
| 4 / Media | Sincronizar el CSS mediante el generador existente y revisar el resultado junto con el vendor. | Asset generado conservado y reproducible; scanner, catálogo y formularios previos sin regresiones visuales. |
| 5 / Media | Habilitar Playwright como herramienta de QA y validar scanner óptico, consumidores operativos y catálogo a 320/390/768/1440 px. Añadir comprobación del circuito interactivo. | Informes nuevos sobre el árbol corregido, sin overflow/errores JS ni pérdida de estado; mocks y cámara real identificados por separado. |
| 6 / Media | Completar la evidencia física reproducible de los tres consumidores y la regresión específica de Safari cuando esté disponible ese dispositivo. | Modelo, sistema, navegador/versiones y resultado registrados para escaneo repetido, lotes/precios, inexistente, cancelación, Inventario sin unidades y Compra con comprobante. |
| 7 / Baja | Cerrar el estado documental con los resultados posteriores y sus límites. | Contrato, planificación e historial coherentes; resultados del 06/10 conservados como históricos. |

No se considera validada una release mientras persista H1. Este plan no autoriza
merge a main/develop, publicación ni despliegue.

# Validación de movimientos ajustes CF y productos

Fecha de cierre: 23/11/2026, Guatemala. Implementación en rama, sin release, merge, push ni despliegue. Contrato canónico: [operaciones financieras y productos](../modules/operaciones-financieras-y-productos.md).

## Base y recuperación

La base documental es `6889a32` de `origin/docs/requisitos-movimientos-ajustes-cf`; `origin/develop` y `origin/main` continúan en `687dd2a` tras fetch. Se conservó el árbol original de `chore/agent-workflow`, incluidos AGENTS y archivos relacionados en edición.

Un reinicio vació el worktree temporal. Los archivos se recuperaron reproduciendo únicamente sus escrituras desde el registro de esta misma sesión, sin repetir el análisis funcional ni modificar el árbol original. Se regeneraron CSS y migraciones EF desde el modelo recuperado; no se aplicaron a datos reales. Los commits locales mantienen una copia durable del trabajo. El worktree de entrega se conserva junto al proyecto en `ResellManager-movimientos-ajustes-cf`, rama `feature/movimientos-ajustes-cf-productos`, para no depender de `/tmp`.

## Verificación técnica

- .NET SDK 10.0.115 y EF Core 10.0.12, Linux Ubuntu 26.04; compilación sin advertencias ni errores.
- La comprobación EF no encuentra cambios de modelo pendientes de migración. Las pruebas web recorren migraciones reales sobre bases sintéticas; se conserva el default histórico de CanalVenta y los Q0 anteriores permanecen conocidos hasta una migración acotada.
- `npm ci`, regeneración de CSS y distribución local del scanner completadas. Pruebas JS: 8 archivos correctos, sin fallos.
- Permisos, CF y concurrencia: 32 pruebas correctas. Se verifican descargas anónimas denegadas, cuenta Identity sin concesión administrativa, comandos repetidos y escrituras obsoletas en inventario, apertura y precio.
- La primera suite tras recuperación tuvo 1086 casos correctos y 2 fallos: una fecha fija de reembolso dependía del reloj real y una petición de autenticación excedió 100 s durante la ejecución concurrente con QA. Se fijó el reloj del caso CF; ambos casos pasaron en la ejecución del área. Regresión final completa: **1089 correctas, 0 fallidas y 0 omitidas**, en 4 minutos, con máximo 4 hilos xUnit y sin navegadores simultáneos; no se alteró la configuración del producto. El registro TRX está en `.artifacts/validacion-finanzas/regresion-cierre.trx`.

## Navegador y alcance

Chromium 141.0.7390.37 con Playwright 1.56.1; instalación temporal externa al package del repositorio. La plataforma se identifica explícitamente como Ubuntu 24.04 para el binario de compatibilidad de Playwright. No se añadió una dependencia de producción.

| Validación | Evidencia y resultado |
| --- | --- |
| Finanzas y productos, circuito real | 6 flujos correctos a 390/1440 px: precio con foto y filtro preservado; apertura CSV/manual y XLSX/PDF; ajuste; CF con cambio; aprobación/reembolso por otro método/recepciones apta y no apta; nota de costo e informe; etiquetas con perfil válido. Saldo final conservado, sin errores JS. |
| Scanner óptico | 80 comprobaciones y 28 decodificaciones correctas a 320/390/768/1440 px; fixtures locales, sin cámara física. |
| Scanner, markup responsive | 84 estados correctos a 320/390/768/1440 px, con objetivos táctiles de 44 px; 3 pruebas .NET producen el HTML real. |
| Scanner con Blazor y SQLite | 5 escenarios correctos: alta con galería/subcategoría, comprobante conservado, consulta en Inventario, recepción de Compra y Venta Directa por lotes. |
| Productos | 9 comprobaciones correctas: miniaturas, navegación, errores, scroll 20 → 40 → 43, búsqueda y filtro SQL y fallback sin IntersectionObserver. El script espera los eventos del formulario para evitar un POST sobre HTML aún prerenderizado. |
| Catálogo público | 193 comprobaciones correctas con respuestas sintéticas; galerías, categorías, paginación, móvil/escritorio, contacto y reconexión. |

Informes y capturas están en `.artifacts/finanzas-productos/1795492014534`, `.artifacts/productos/1795491858636`, `.artifacts/catalogo-qa/1795491680315` y `.artifacts/scanner-operativo-interactivo/1795491683296`. Se inspeccionaron las capturas de precio, movimientos y devolución CF. Las bases, cuentas, imágenes y comprobantes utilizados son sintéticos.

El QA del markup operativo encontró objetivos táctiles pequeños en los radios nuevos; se ampliaron sus etiquetas y la del costo conocido. La comprobación mide la etiqueta clicable para radios y checkboxes con el mismo mínimo de 44 px. Resultado final: 84 estados correctos (21 a cada ancho de 320/390/768/1440 px), sin errores JS; markup de componentes reales y SQLite sintético, sin circuito vivo en esta comprobación.

## Revisión pública REG-AC-02 y límites

La revisión de código y pruebas cubre explícitamente el nuevo acceso directo al detalle e imágenes agotadas y la lista de campos públicos. Los listados conservan disponibilidad. Las variantes nunca recibidas tampoco se exponen mediante el grupo de una ficha pública. No se publican costos, proveedores, unidades, reservas, clientes ni rutas privadas. El código interno comercial y los metadatos mínimos de variantes están permitidos por los requisitos aprobados.

Antes de publicar debe revisarse la API pública y el acceso por enlace a fichas agotadas/variantes conforme a REG-AC-02. Esta revisión local no acredita su comportamiento detrás del proxy de producción ni modifica el alcance público autorizado.

Quedan fuera de esta evidencia la cámara real, Safari/iPhone y la impresión/lectura física de etiquetas NIIMBOT. La aceptación física previa del scanner V1.4 no certifica las nuevas etiquetas. Un código PRO-GUID completo necesita aproximadamente 117 mm de ancho a 203 dpi con dos puntos por módulo; no se ha declarado compatibilidad con 50 mm.

La configuración de IDs administrativos, preparación del alcance real de costos Q0, respaldo/restauración del entorno y ejecución de migraciones sobre producción son tareas operativas no ejecutadas. No se diseñó un sistema general de roles, ni cierres fiscales/contables, ni devoluciones parciales o de ventas ajenas a CF.

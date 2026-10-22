# Productos: miniaturas, caché y diagnóstico de navegación — 22/10/2026

## Alcance y estado histórico de la implementación

Implementación local en `hotfix/productos-imagenes-navegacion`, base
`e9ab3cfb6616c0352d7ac37892a5dc5182795481` de `develop`.
Al comenzar, `develop` y `main` tenían el mismo árbol de archivos; la diferencia
era un commit de sincronización documental. Se leyó y respetó `AGENTS.md`.
No se hizo push, merge, despliegue ni generación sobre datos de producción.

Implementados los derivados, su ciclo de vida, endpoints autenticados, caché
privada versionada, fallback heredado y reintento acotado. Contratos en
[Imágenes](../24_ImagenPrincipalProducto.md#miniaturas-administrativas-y-caché);
ejecución de generación existente en el [runbook](../deployment/deployment.md#generación-de-miniaturas-existentes).

## Validación automatizada

| Comprobación | Resultado |
| --- | --- |
| Compilación de la solución | Correcta, cero warnings y errores |
| Suite .NET completa | 1045 aprobadas, cero fallos |
| Regresión del área tras el ajuste final de compensación de archivos | 81 aprobadas, cero fallos |
| `npm run test:js` | 134 aprobadas, cero fallos |
| `npm run qa:productos` | Correcto en Chromium headless, 390 y 1440 px |
| `git diff --check` | Sin errores |

En este entorno MSBuild necesitó `-m:1 -p:BuildInParallel=false` para compilar;
no se cambió la configuración del proyecto. SDK .NET 10.0.402. No hubo cambios
de estilos ni nuevas utilidades CSS que requirieran regenerar assets.

Las pruebas del almacenamiento verifican WebP, límite 240 px, proporción,
transparencia, EXIF, no ampliación, rechazo de rutas inválidas, cancelación y
limpieza. Las regresiones existentes de galería/compra/lookup verifican ahora el
par de archivos; se conservan sus comprobaciones de rollback y ausencia de huérfanos.
Los endpoints comprueban autenticación incluso con If-None-Match, ETag/304,
cambio de portada, separación de imagen/miniatura, versiones antiguas y 404
vacíos sin caché. El catálogo público conserva sus reglas anteriores.

El navegador creó categoría y productos mediante formularios reales sobre SQLite
y archivos temporales. Se comprobó nombre → detalle, botón → detalle, retorno sin
recarga, miniatura de hasta 240 px y grande en detalle, ausencia de overflow y
reutilización de caché al volver. Se simularon 503 transitorio y 404 permanente:
un reintento por imagen en el componente interactivo y fallback sin bucle.
El prerender y la hidratación pueden solicitar por separado una URL inicial
fallida; la prueba distingue esas peticiones de los reintentos explícitos.
Se inspeccionaron capturas móvil/escritorio del listado y detalle.

## Generación desde un producto existente

`ProductoMiniaturasTests` prepara una referencia persistida a WebP sin derivado,
presente tanto en portada como en galería. Antes se sirve la grande; después
se obtiene una miniatura 160 × 240 desde una fotografía 600 × 900. La segunda
ejecución no cambia bytes ni fecha de la derivada, no duplica archivos y conserva
el hash de la imagen grande y las referencias SQLite.

También se ejecutó el comando real de mantenimiento contra una base de QA
existente, retirando sólo una miniatura sintética para representar el estado
anterior al hotfix:

- Primera ejecución: **1 creada, 1 existente, 0 fallidas**, salida 0.
- Segunda ejecución: **0 creadas, 2 existentes, 0 fallidas**, salida 0.
- Hashes de imágenes grandes y referencias de productos sin cambios.

## Incidencia de navegación pendiente de reproducir

El usuario reportó que pulsar el nombre dejaba visible el listado hasta recargar,
mientras «Ver detalle» funcionaba. En el código original ambos son enlaces a la
misma ruta. No se reprodujo el fallo ni con este hotfix ni con una compilación
aislada de `main` (`3fabf95edddf82721ad7f3059a4b8384e4cc8adb`), usando el mismo
QA por nombre y botón en ambas anchuras.

No se alteró el router ni se forzó una recarga completa para ocultar el síntoma.
La prueba de regresión queda incorporada, pero esto **no demuestra que el fallo
del dispositivo afectado esté corregido**. Falta contrastar la versión realmente
desplegada y capturar URL, consola y solicitudes durante ese fallo; tampoco se
confirmó la causa de cada imagen fallida en la sesión real. Los dispositivos
conectables de laptop y VPS estaban offline durante el trabajo.

Pendientes operativos: publicación del hotfix cuando se indique, generación del
lote en el entorno objetivo y QA físico en el navegador afectado. La emulación
Chromium no certifica Safari/iPhone ni el comportamiento detrás de Caddy.

## Reconstrucción autorizada y listado incremental

El commit local `9d32ae6` comunicado para el scroll infinito dejó de estar
presente en la copia disponible y no pudo recuperarse del reflog ni de objetos
sin referencia. El usuario autorizó reconstruir ese cambio sobre `f0c24c0` y
publicarlo únicamente en `hotfix/productos-imagenes-navegacion`. La reconstrucción
genera un SHA nuevo; no se afirma haber recuperado el objeto Git anterior.

Se restableció la carga de 20 productos por página, con filtros y orden en SQL,
cursor por prioridad/nombre/ID, límite de 21 filas para detectar continuidad,
solicitudes serializadas, reintento sin perder resultados y cancelación al navegar.
La búsqueda sigue siendo global y el contador expresa productos cargados.
Contrato: [listado incremental](../modules/productos.md#listado-administrativo-incremental).

Validación de la reconstrucción:

- Solución compilada con cero warnings y errores.
- Suite completa: **1053 pruebas .NET aprobadas**.
- Suite JS: **140 pruebas aprobadas**.
- QA real en navegador: **20 → 40 → 43** productos en 390 y 1440 px, sin
  duplicados; búsqueda global por nombre/código, filtro de categoría y Limpiar.
- Fallback manual sin IntersectionObserver, reintento de imagen y reutilización
  de caché comprobados. Se usaron SQLite y archivos sintéticos en un directorio
  temporal aislado mediante `PRODUCTOS_QA_DIRECTORY`.
- `git diff --check` sin errores. Sin migraciones ni dependencias nuevas.

La paginación reduce la carga inicial, pero no prueba que las imágenes fueran
la causa del fallo de navegación del dispositivo afectado. Ese diagnóstico sigue
pendiente; no se fuerza una recarga completa ni se declara corregido el fallo.

## Cierre posterior en producción — 22/10/2026

El código publicado `5e16db0` se desplegó a las 19:11:59 UTC. Se generaron 124
miniaturas existentes, sin fallos y sin modificar originales; una segunda
ejecución no recreó ninguna. Las 17 comprobaciones técnicas fueron correctas.
Después del despliegue, Pablo confirmó la mejora de velocidad y que el fallo
de navegación desapareció, y dio por terminado el hotfix. Se cierra como
`v1.4.1`, con integración por PR #30.

La confirmación posterior resuelve el pendiente funcional descrito en las fases
anteriores; no demuestra una causa raíz aislada ni añade trazas que no se
capturaron. Los resultados históricos de este documento se conservan como tales.
Respaldo, incidente inicial ya recuperado, imagen exacta y límites en el
[registro de producción](../deployment/produccion-hotfix-productos-20261022.md).

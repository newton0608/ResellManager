# Producción verificada — v1.4.1, hotfix de productos, 22/10/2026

## Cierre y aceptación

Pablo confirmó después del despliegue una mejora marcada de velocidad y que el
fallo de navegación también desapareció; dio por terminado el hotfix y autorizó
el cierre de changelog, documentación, tag e integración del PR #30 a `main`.
La versión global **`v1.4.1`** identifica el commit exacto que originó la imagen:
`5e16db03067fa4f4c93227cb59ce713c4cf255c1`. Los commits posteriores son de
documentación y cierre; no requieren reconstruir ni recrear la aplicación.

La aceptación confirma el comportamiento observado. No identifica por sí sola
la causa original del fallo ni certifica pruebas individuales no comunicadas.

## Estado histórico durante la activación

El código del [PR #30](https://github.com/newton0608/ResellManager/pull/30)
quedó desplegado en producción. En esa fase el PR permaneció abierto; no se hizo
merge a `main` ni se modificó el checkout operativo. La aceptación funcional aún
estaba pendiente. Por instrucción del responsable no se actualizó
`CHANGELOG.md` durante esta operación ni se crearon tags Git o releases.

La prueba física inicial comunicada por Pablo en iPhone 14 Plus con Brave
correspondía al candidato anterior al despliegue. La confirmación posterior en
producción se registra por separado en el cierre de este documento.

## Identificación y continuidad

| Referencia | Evidencia |
| --- | --- |
| Código de la imagen | `5e16db03067fa4f4c93227cb59ce713c4cf255c1` |
| Tag global | `v1.4.1`, sobre el mismo commit de la imagen |
| Imagen activa | `sha256:40160c8066df39a0e2ea04fcc022a603ef44edf1210d99c770d90052cb8880d6` |
| Activación correcta | 22/10/2026, 19:11:59 UTC, health `OK` |
| Construcción | Docker Release desde un archivo Git del SHA fijado; sin cambios de código adicionales |
| Esquema | Sin diferencias en migraciones respecto al código anterior; arranque sin migraciones pendientes |
| Respaldo | Aplicación detenida; cuatro directorios persistentes completos, SHA-256 y SQLite `quick_check` correctos |
| Infraestructura | Solo se recreó la aplicación; Caddy conservó identidad y hashes de Compose/Caddyfile/configuración privada sin cambios |

El primer intento, a las 19:08 UTC, no arrancó: el código se había extraído con
permisos restrictivos que dejaban `appsettings.json` ilegible para el usuario
de la aplicación. Se recuperó la imagen anterior, con health correcto a las
19:09:03 UTC. Se corrigieron los permisos del contexto de construcción, se
reconstruyó y se comprobó el arranque aislado con HTTP 200 antes del segundo
intento. Este incidente no requirió cambios en el código ni en Dockerfile.

La activación correcta tuvo su propio respaldo consistente y recuperó health
unos cinco segundos después de comenzar la parada. Se retuvo la imagen anterior.
No se restauraron datos ni se certificó un ejercicio completo de recuperación.
Las referencias de respaldo, configuración y logs permanecen en la bitácora
operativa privada; no se incorporan secretos ni datos comerciales al repositorio.

## Verificación técnica

Las 17 comprobaciones posteriores fueron correctas: health, login, cabeceras
básicas, autenticación de listado/detalle/imagen/miniatura, portada y API pública,
hashes de JavaScript de productos y scanner, CSS y Quagga, imagen exacta activa,
ausencia de reinicios, ausencia de migraciones pendientes y de excepciones en
los logs observados. Los assets publicados coinciden con los del SHA construido.

El comando de mantenimiento produjo **124 miniaturas, 0 fallidas**. Una segunda
ejecución produjo **0 nuevas, 124 existentes, 0 fallidas**. La inspección de los
archivos verificó 124 WebP con dimensiones máximas de 240 px y los 124 originales
idénticos al respaldo previo. Los derivados suman 1.071.558 bytes, frente a
17.454.706 bytes de los originales. No se modificaron productos ni referencias
para realizar estas comprobaciones.

La evidencia automatizada previa del código se conserva en el
[cierre técnico](../auditorias/productos-miniaturas-20261022.md). No se volvió a
ejecutar la suite completa durante la activación; sí se construyó la imagen y
se ejecutó el procesamiento nativo de imágenes en el contenedor productivo.

## Alcance de la aceptación posterior

La confirmación del responsable cierra el hotfix del listado, miniaturas y
navegación en producción. No se comunicó un desglose de cada filtro, caché o
gesto ni versiones exactas del navegador/sistema en esa comprobación posterior;
se conservan los resultados automatizados y sus límites. No se extiende esta
aceptación a los casos físicos pendientes de otros módulos.

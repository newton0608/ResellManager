# Dominios, catálogo y previews

**Actualizado: 05/10/2026.** Los dominios siguientes fueron confirmados por el
responsable en la solicitud de esta auditoría. La configuración externa efectiva
del VPS no se inspeccionó; no se certifican DNS, TLS ni reglas del proxy desde Git.

| Host | Uso confirmado | Evidencia versionada / límites |
| --- | --- | --- |
| `https://virtuosagt.com` | Dominio público real de Virtuosa Store | No aparece en Caddyfile/Compose versionados; no inferir el routing externo. |
| `https://app.resellmanager.tech` | Sistema administrativo | Host presente en Caddyfile y runbook. |
| `https://preview.newtonlab.dev` | Previews/pruebas, incluido catálogo | No tiene configuración propia versionada en este repositorio. Debe seguir siendo compatible. |

`resellmanager.tech` no sustituye al host administrativo confirmado
`app.resellmanager.tech`. No hay una transición de administración a otro host
aprobada por este trabajo.

## Rutas existentes y objetivo pendiente

| Contrato | Estado en el repositorio |
| --- | --- |
| `/catalogo` y `/catalogo/{ProductoId:int}` | Páginas anónimas existentes. Enlaces actuales relativos al origen. |
| `/api/catalogo/productos`, `/{id}` y `/{id}/imagen` bajo ese prefijo | API pública de lectura existente. |
| `https://virtuosagt.com/producto/{id}` | **URL canónica deseada; pendiente de implementación/verificación de routing.** No existe un `@page /producto/...` en la base auditada. |

La raíz del dominio público, redirects, aliases, metadatos canonical y eventual
compatibilidad con `/catalogo/{id}` requieren una tarea específica; no se deducen
de la URL deseada. En preview deben seguir funcionando catálogo, detalle, API,
imágenes y circuito Blazor sin codificar el dominio productivo en las lecturas.

## Separación y operación

La [decisión 026](../11_DecisionesDeDiseño.md#026-separar-dominios-público-y-administrativo)
mantiene las identidades pública/privada sobre los servicios y datos existentes.
El dominio público debe ofrecer catálogo y recursos necesarios sin habilitar
rutas administrativas, autenticación o comprobantes. Identity sigue siendo
autoridad para rutas privadas; el routing por host no reemplaza autorización.

En una tarea de deployment revisar la configuración **efectiva** y el
[runbook](deployment.md): `AllowedHosts`, TLS, proxy confiable, recursos estáticos
y `/_blazor` (negociación/WebSocket/reconexión). La UI es InteractiveServer;
permitir únicamente HTML e imágenes no basta.

La topología prevista reutiliza la misma aplicación interna y los mismos datos/
volúmenes para ambos dominios, sin un servicio paralelo de catálogo. Mantener
el Host externo y el esquema HTTPS correctos, confiar solo en el proxy conocido
y aceptar los hosts necesarios mediante configuración del entorno.

Inventario de recursos para revisar contra el HTML efectivo: `branding/virtuosa/*`,
`app.css`, `css/tailwind.css`, `app.js`, `form-feedback.js`, `reconnect.js`,
`catalogo-publico.js`, `_framework/*` y el circuito `/_blazor`. Si una versión
incorpora recursos `_content/*` o CSS aislado, incluir únicamente los que consume;
no copiar una allowlist antigua sin comprobarla. Conservar cookies de Identity
restringidas a su host y revisar CSP/conexiones WebSocket al configurar dominios.

Antes de cerrar cambios de routing, probar ambos hosts y preview con datos
aislados, enlaces directos/404, imágenes ausentes, filtros/reconexión y denegación
de rutas privadas desde el host público. Registrar entorno, commit y evidencia.
No ejecutar cambios de Caddy, DNS, Docker o DeployManager por una tarea de catálogo
que no incluya despliegue.

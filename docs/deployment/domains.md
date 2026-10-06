# Dominios, catálogo y previews

**Actualizado: 05/10/2026.** Además de la auditoría del repositorio, se verificó
operacionalmente el routing público del VPS. Esta guía distingue explícitamente
el comportamiento externo efectivo del routing que declara Blazor y de la
configuración que hoy está versionada en Git.

| Host | Estado operativo verificado | Límite / fuente |
| --- | --- | --- |
| `https://virtuosagt.com` | HTTPS activo; `/` sirve el catálogo público. | Routing efectivo verificado en el VPS. |
| `https://www.virtuosagt.com` | Redirige al host canónico `https://virtuosagt.com/`. | Redirect externo de Caddy. |
| `https://app.resellmanager.tech` | Sistema administrativo sigue operativo. | Host administrativo separado. |
| `https://preview.newtonlab.dev` | `/catalogo` y su API pública siguen operativos para pruebas. | Preview conserva las rutas Blazor originales. |

`resellmanager.tech` no sustituye al host administrativo confirmado
`app.resellmanager.tech`. No hay una transición de administración a otro host.

## Routing externo frente a routing Blazor

La aplicación Blazor sigue declarando:

- `/catalogo`
- `/catalogo/{ProductoId:int}`

El dominio público expone externamente:

- `https://virtuosagt.com/` → catálogo;
- `https://virtuosagt.com/producto/{id}` → detalle limpio;
- `https://virtuosagt.com/catalogo` → redirect a `/`;
- `https://virtuosagt.com/catalogo/{id}` → redirect a `/producto/{id}`;
- `https://www.virtuosagt.com/*` → redirect al host canónico.

La URL `/producto/{id}` **es operativa**, pero no existe como `@page` en el
código Blazor. Caddy la reescribe internamente a `/catalogo/{id}`. Por tanto,
documentar la URL limpia no autoriza a afirmar que se implementó un alias en
Razor ni a cambiar los componentes sin una tarea específica.

La API pública conserva `/api/catalogo/productos`, su detalle e imagen. La UI
usa rutas relativas al origen para que producción y preview puedan compartir el
mismo código.

## Frontera pública verificada

En la comprobación operativa del 05/10/2026:

- el catálogo y sus assets/API respondieron correctamente por
  `virtuosagt.com`;
- rutas administrativas probadas como `/login`, `/clientes`, `/compras`,
  `/ventas` y `/productos` respondieron 404 desde el host público;
- `preview.newtonlab.dev/catalogo` siguió funcionando;
- TLS del dominio público fue válido.

El routing por host reduce la superficie pública, pero no sustituye Identity para
las rutas privadas del sistema administrativo.

## Deuda de infraestructura/versionado

La configuración efectiva de Caddy que habilita el dominio público y sus rewrites
fue aplicada y verificada en el VPS. Sin embargo, el estado operativo no estaba
completamente representado por la configuración versionada en la rama remota del
repositorio durante esta verificación. No asumir que clonar el repositorio y
levantar su Caddyfile reproduce automáticamente el routing público actual.

Antes de modificar o desplegar infraestructura, comparar la configuración
versionada con la efectiva y resolver esa deuda sin borrar `preview.newtonlab.dev`
ni afectar `app.resellmanager.tech`.

## Separación y operación

La [decisión 026](../11_DecisionesDeDiseño.md#026-separar-dominios-público-y-administrativo)
mantiene las identidades pública/privada sobre los mismos servicios, base de
datos y volúmenes. No se crea otro inventario ni otro backend para Virtuosa.

En una tarea de deployment revisar también el [runbook](deployment.md):
`AllowedHosts`, TLS, proxy confiable, recursos estáticos y `/_blazor`
(negociación/WebSocket/reconexión). La UI es InteractiveServer.

Inventario de recursos a conservar en el host público:
`branding/virtuosa/*`, `app.css`, `css/tailwind.css`, `app.js`,
`form-feedback.js`, `reconnect.js`, `catalogo-publico.js`, `_framework/*`
y el circuito `/_blazor`. Si una versión incorpora `_content/*` o CSS aislado,
publicar solo lo que consuma realmente.

Antes de cerrar nuevos cambios de routing, probar producción y preview con
enlaces directos/404, imágenes, filtros/reconexión y denegación de rutas privadas.
Registrar entorno, commit y evidencia. Una tarea de catálogo no autoriza por sí
sola cambios de Caddy, DNS, Docker o DeployManager.

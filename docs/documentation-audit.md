# Auditoría documental para agentes — 05/10/2026

## Base y alcance

Repositorio: `newton0608/ResellManager`. Se clonó y se hizo fetch de todas las
ramas. main y develop coincidían en `835a487` (tag v1.2.1); develop se eligió como
base de integración para `docs/agent-guidance`. Las ramas remotas de features,
hotfix y migración .NET 10 inspeccionadas ya están contenidas en esa base.
No había AGENTS.md ni una colección ADR separada.

Se inspeccionaron README, ROADMAP, CHANGELOG, documentación numerada/diagramas,
instrucciones Copilot, proyectos/capas, servicios/contratos del negocio, modelos
EF/migraciones, pruebas existentes, Dockerfile, Compose, Caddyfile y fuentes de
backup/systemd. No se accedió al VPS, DB real, DNS o DeployManager.

## Archivos nuevos

- `AGENTS.md`: contrato general, arquitectura resumida, Git, validación proporcional
  y mapa selectivo; 75 líneas en la entrega inicial.
- `docs/README.md`: índice, jerarquía de fuentes y flujo de futuras tareas.
- `docs/modules/{clientes,productos,compras,inventario,pedidos,ventas,catalogo,landed-cost}.md`:
  entradas por área con estado, contratos de referencia, código y pruebas existentes.
- `docs/deployment/domains.md`: hosts confirmados, evidencia versionada y rutas
  pendientes sin afirmar cambios del proxy.
- Este registro de auditoría.

## Reorganización de documentos existentes

| Ruta anterior | Ruta canónica actual |
| --- | --- |
| `docs/07_Arquitectura.md` | [architecture/overview.md](architecture/overview.md) |
| `docs/08_ReglasDelNegocio.md` | [architecture/domain-rules.md](architecture/domain-rules.md) |
| `docs/10_BaseDeDatos.md` | [architecture/persistence.md](architecture/persistence.md) |
| `docs/21_Despliegue_V1.md` | [deployment/deployment.md](deployment/deployment.md) |
| `docs/22_Seguridad_Produccion.md` | [deployment/security.md](deployment/security.md) |

Se trasladó el contenido útil y se actualizaron enlaces relativos entrantes y
salientes, incluidos anchors; no se mantienen copias paralelas. Los demás
documentos numerados, diagramas y SQL existente permanecen en su sitio.
Links externos que apunten a rutas antiguas necesitarán actualizarse cuando se
integre esta rama; no se añadieron stubs duplicados.

## Archivos existentes modificados

- README, ROADMAP y CHANGELOG: versión/capacidades actuales y navegación.
- `docs/04_ModeloDelNegocio.md`, `09_Backlog.md`, `18_Fase510_CierreV1.md`:
  actualización de enlaces a documentos trasladados, conservando contenido.
- `docs/11_DecisionesDeDiseño.md`: enlaces y decisiones 025–027 con estados.
- `docs/12_IdeasFuturas.md`, `14_Alcance_V1.md`, `19_V2_Pendientes.md`:
  distinguir contexto original, dirección de desactivación y capacidades actuales.
- `docs/23_MedidasYPresentacionProducto.md`, `25_CatalogoPublicoBackend.md`,
  `26_CatalogoPublicoUI.md`, `27_VirtuosaStore.md`: separar primeras iteraciones,
  comportamiento integrado, identidad y deployment.
- `.github/copilot-instructions.md`: reemplazar instrucciones Azure genéricas,
  ajenas a este stack, por referencia al contrato único AGENTS.md.
- Las cinco guías trasladadas: enlaces, estado actual y puntos de entrada reales.

## Estructura final

- `docs/README.md` y `documentation-audit.md`.
- `docs/architecture/`: `overview.md`, `domain-rules.md`, `persistence.md`.
- `docs/modules/`: `clientes.md`, `productos.md`, `compras.md`, `inventario.md`,
  `pedidos.md`, `ventas.md`, `catalogo.md`, `landed-cost.md`.
- `docs/deployment/`: `domains.md`, `deployment.md`, `security.md`.
- `docs/11_DecisionesDeDiseño.md`: registro numerado canónico, sin ADRs duplicados.
- Resto de documentación numerada: requisitos/contexto, guías especializadas,
  roadmaps y evidencia histórica, inventariados en el [índice](README.md).
- `docs/diagrams/`: los diez diagramas drawio existentes, conservados.
- `docs/20261003_AddPurchaseCurrencies.sql`: SQL de referencia existente, sin cambios.

## Duplicaciones y contradicciones resueltas

| Hallazgo | Tratamiento |
| --- | --- |
| README/runbook/checklist aún decían .NET 8, aunque Directory.Build.props y Dockerfile usan .NET 10 | Actualizar configuración descrita; conservar evidencia operativa fechada sin afirmar actualización del VPS. |
| ROADMAP/medidas decían V1.1 sin publicación; Git contiene v1.1.0, v1.2.0 y v1.2.1 | Registrar tags/fechas; distinguir existencia en Git de despliegue y GitHub Releases. |
| V2 presentaba foto principal/scanner/catálogo enteramente futuros | Marcar piezas implementadas y conservar ampliaciones pendientes (múltiples fotos, scanner operativo, tienda transaccional). |
| Virtuosa duplicaba instrucciones de proxy y proponía administración en resellmanager.tech | Concentrar hosts/routing en Deployment; administración confirmada en app.resellmanager.tech. |
| Dominio público sin nombre, frente a virtuosagt.com confirmado | Registrar dominio real; una verificación operativa posterior el 05/10/2026 confirmó HTTPS y routing efectivo. |
| URL pública /producto/{id} frente a @page /catalogo/{ProductoId:int} | Distinguir capas: /producto/{id} está operativo por rewrite externo de Caddy; Blazor conserva /catalogo/{id}. |
| «Catálogo» para abastecimiento bajo pedido y catálogo web | Separar explícitamente ambos contratos. |
| DELETE/desactivación como opciones abiertas; no hay estado activo | Registrar dirección 025 pendiente de implementación; preservar preguntas del análisis. |
| Guías de fases con cifras de pruebas, trabajo sin commit y UI antigua | Marcar evidencia histórica y enlazar estado integrado; no inventar pruebas actuales. |
| README describía estilos legacy/aislados retirados | Alinear descripción con app.css base, Tailwind compartido y storefront.css. |
| Requisitos/diagramas/alcance original no contienen todas las ampliaciones posteriores | Mantener material histórico y referir al índice/código/configuración EF; no afirmar sincronización completa de diagramas. |

## Decisiones y datos que siguen abiertos

- Landed cost: levantamiento pendiente, sin gastos/reglas de reparto, entidades,
  migraciones o versión definitiva.
- Desactivación: clientes y productos no se eliminan físicamente desde la
  aplicación, tengan o no historial. Sigue pendiente definir deuda/reservas,
  operaciones nuevas, búsquedas, efecto público/inventario y reactivación. No se
  generaliza a otros maestros.
- Ruta pública: `/producto/{id}` quedó verificada como URL operativa mediante
  rewrite externo; Blazor conserva `/catalogo/{id}`. Canonical SEO adicional
  sigue siendo una tarea separada.
- Dominios/preview: la verificación operativa posterior confirmó
  `virtuosagt.com`, redirección `www`, 404 de rutas administrativas y preview.
  Sigue pendiente alinear completamente la configuración efectiva de Caddy con
  la configuración versionada del repositorio y documentar/versionar esa deuda.
- Tienda V2.4: carrito/pedidos y operaciones transaccionales futuros, no existentes.
- Comisiones por proveedor/categoría, devoluciones, roles y concurrencia fuerte:
  no se resuelven por documentación. Se mantienen sus pendientes previos.
- Medidas públicas: ml/gramos/presentación ya existen; equivalencias y formatos
  adicionales siguen como propuesta futura.

## Validación y continuidad

Validación realizada: 50 archivos Markdown y 283 enlaces internos/anchors,
sin destinos ni anchors ausentes. AGENTS.md apunta a rutas reales y todos los
nombres de suites citados por los módulos existen. Se revisó el diff completo,
incluidas las diferencias de las guías trasladadas contra sus originales;
`git diff --cached --check` pasó. Todos los archivos cambiados son Markdown:
no se modificó código, proyectos, SQL/migraciones o configuración ejecutable.
Se conservaron contenido y evidencia útiles. No se ejecutaron build ni suite
pesada porque el alcance fue exclusivamente documental.

El SHA, publicación efectiva de la rama y estado Git final se reportan en la
entrega una vez ejecutados; no se anticipa aquí un resultado ni se introduce un
SHA autorreferencial. No se autoriza merge a develop/main.

Recomendación de flujo: acordar requisitos → actualizar guía canónica y criterios
de aceptación → prompt corto con referencias → implementación y validación
proporcional. [Índice](README.md#flujo-para-futuras-tareas-de-codex) contiene el ejemplo.

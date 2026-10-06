# Guía para agentes — ResellManager

ResellManager administra el negocio de reventa: clientes, compras, inventario,
pedidos, ventas, pagos y Dashboard. Virtuosa Store es su catálogo público de
lectura; todavía no es una tienda con carrito, checkout o pedidos web.

## Contrato de trabajo

- Limita los cambios al objetivo solicitado; no hagas refactors, cambios de UI,
  dependencias, modelos o migraciones ajenos a la tarea.
- Reutiliza servicios, contratos y componentes existentes; evita dependencias
  nuevas innecesarias. Revisa las pruebas del área antes de crear otro patrón.
- No modifiques infraestructura, Docker, Caddy, DNS o DeployManager salvo que
  la tarea lo requiera explícitamente. No uses datos reales para pruebas.
- Distingue comportamiento implementado, decisión pendiente y propuesta futura.
  Una contradicción documental no autoriza cambiar reglas de negocio.
- La documentación forma parte de la definición de terminado. **Cada cambio
  implementado debe cerrar también su estado documental en la misma rama**:
  revisa el documento canónico del módulo y cualquier registro de planificación
  afectado. Si una tarea completa algo que figuraba como pendiente, actualiza o
  marca ese ítem en `docs/09_Backlog.md`, `docs/19_V2_Pendientes.md`,
  `ROADMAP.md` u otro `.md` que lo siga; no dejes una funcionalidad
  implementada descrita como pendiente.
- Actualiza `CHANGELOG.md` cuando el cambio sea relevante para el historial de
  producto/release. Actualiza decisiones, arquitectura, deployment, seguridad,
  contratos, pruebas o runbooks cuando el cambio altere esas fuentes de verdad.
  No modifiques documentos no afectados solo por cumplir una lista.
- Antes de entregar, busca referencias obsoletas al comportamiento cambiado
  (estados «pendiente/planeado», nombres, rutas, versiones, límites, criterios de
  validación) y reconcílialas. Conserva la evidencia histórica como histórica:
  añade el estado posterior cuando corresponda en vez de reescribir lo que una
  validación pasada realmente demostró.
- Enlaza detalles en lugar de copiarlos en varias guías. Si el código y la
  documentación discrepan, reporta la discrepancia y deja ambas fuentes
  coherentes dentro del alcance autorizado; una contradicción documental no
  autoriza inventar ni cambiar reglas de negocio.

## Arquitectura

.NET 10; Blazor Web App InteractiveServer, EF Core/SQLite e Identity.
`Application → Domain`, `Infrastructure → Application`,
`Web → Application + Infrastructure` (referencias de proyectos).
Domain contiene entidades/enums; Application, contratos/DTOs/helpers compartidos;
Infrastructure, las implementaciones actuales de casos de uso, EF y almacenamiento;
Web, presentación, endpoints y composición. Respeta esas capas.
No introduzcas reglas de negocio, consultas DbContext ni cálculos financieros
en componentes Razor: consume los servicios existentes; la validación visual
no sustituye la del servidor.

## Git

- Antes de trabajar, revisa status y haz fetch; compara main/develop y confirma
  la base. develop es la rama de integración actual; main representa releases.
- Trabaja en una rama específica (`feature/…`, `hotfix/…`, `docs/…` o `chore/…`).
  Preserva cambios ajenos; no hagas reset destructivo ni force-push.
- No hagas merge a main/develop ni despliegues sin instrucción explícita.
- Revisa el diff completo y `git diff --check` antes de entregar. Usa commits
  pequeños y descriptivos, coherentes con el historial. Push cuando se solicite;
  reporta base, rama, SHA, validaciones y pendientes.

## Validación proporcional

- Cambios de código/proyecto: `dotnet build ResellManager.sln` y pruebas del área
  con `dotnet test ResellManager.sln --filter FullyQualifiedName~NombreDelArea`.
  Ejecuta la suite completa cuando sea razonable, especialmente ante contratos,
  persistencia, reglas compartidas, autenticación o cambios entre módulos.
- JS: `npm run test:js`. Estilos/utilities o assets generados: `npm ci` y
  `npm run css:build`; conserva el resultado, no lo edites manualmente.
- UI: comprueba móvil/escritorio y el flujo afectado; scanner requiere además
  el QA descrito en su guía. Declara límites del entorno y pruebas no ejecutadas.
- Solo documentación: revisa enlaces/rutas, estados y diff; no necesitas build
  ni suite pesada si no cambió ningún archivo ejecutable o de proyecto.

## Lectura según tarea

Lee **solo la entrada del área y sus referencias necesarias**, además de esta
guía. El [índice](docs/README.md) distingue contratos, planes y registros históricos.

| Tarea | Entrada |
| --- | --- |
| Capas / arquitectura | [Arquitectura](docs/architecture/overview.md) |
| Invariantes / modelo EF | [Reglas](docs/architecture/domain-rules.md) · [Persistencia](docs/architecture/persistence.md) |
| Clientes / saldos | [Clientes](docs/modules/clientes.md) |
| Productos / categorías / imágenes / scanner | [Productos](docs/modules/productos.md) |
| Compras / proveedores / monedas | [Compras](docs/modules/compras.md) |
| Inventario / recepción | [Inventario](docs/modules/inventario.md) |
| Pedidos / reservas | [Pedidos](docs/modules/pedidos.md) |
| Ventas / pagos / Dashboard | [Ventas](docs/modules/ventas.md) |
| Catálogo público / Virtuosa Store | [Catálogo](docs/modules/catalogo.md) |
| Landed cost / importaciones futuras | [Análisis pendiente](docs/modules/landed-cost.md) |
| Dominios / previews | [Dominios](docs/deployment/domains.md) |
| Despliegue / recuperación / seguridad | [Runbook](docs/deployment/deployment.md) · [Seguridad](docs/deployment/security.md) |
| Decisiones duraderas / planificación | [Decisiones](docs/11_DecisionesDeDiseño.md) · [Roadmap](ROADMAP.md) |

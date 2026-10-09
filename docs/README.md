# Documentación de ResellManager

Entrada general: [README del proyecto](../README.md).
Contrato para agentes: [AGENTS.md](../AGENTS.md).
Lee la guía del área afectada y sigue únicamente las referencias necesarias.

## Cómo interpretar las fuentes

- **Implementado:** describe código/contratos actuales; compruébalos con servicios
  y pruebas del área. Los proyectos/configuración son evidencia de lo versionado.
- **Decisión con implementación pendiente:** intención acordada que todavía no
  describe el comportamiento del sistema.
- **Planeado / análisis:** roadmap, opciones y preguntas; no autoriza implementación.
- **Histórico:** resultados de una fase/tag, no resultados de la última suite ni
  prueba de la imagen desplegada. Conservamos esa evidencia sin reescribirla.
- **Confirmación operativa:** identifica responsable/fecha; no sustituye la
  inspección del entorno. En esta auditoría no se accedió al VPS.

Si las fuentes discrepan, identifica la diferencia y actualiza el documento
pertinente en el alcance de la tarea. No cambies el código para satisfacer una
propuesta antigua. Las decisiones aceptadas todavía no implementadas se señalan
como tales en el [registro de decisiones](11_DecisionesDeDiseño.md).

## Mapa de contratos vigentes

| Área | Documento de entrada | Referencia detallada existente |
| --- | --- | --- |
| Capas y responsabilidades | [Arquitectura](architecture/overview.md) | Referencias reales entre proyectos y mapa de código/pruebas. |
| Invariantes | [Reglas del negocio](architecture/domain-rules.md) | Estados físicos, reserva, venta, saldo y costos. |
| EF / SQLite / archivos | [Persistencia](architecture/persistence.md) | Contratos del esquema y migraciones existentes. |
| Clientes | [Módulo](modules/clientes.md) | Actividad/saldo; decisión 025 pendiente. |
| Productos / categorías | [Módulo](modules/productos.md) | [Medidas](23_MedidasYPresentacionProducto.md), [imagen](24_ImagenPrincipalProducto.md), [scanner](29_BarcodeScanner.md), [lookup externo](modules/productos-lookup-codigo-barras.md), [selectores/progreso de fotos implementados](modules/catalogo-v1-4-ajustes-ux.md). |
| Compras / proveedores | [Módulo](modules/compras.md) | [Comprobantes](16_Fase58_ComprasYComprobantes.md), [GTQ/USD](28_MonedasDeCompra.md). |
| Inventario | [Módulo](modules/inventario.md) | Reglas físicas y recepción. |
| Pedidos / reservas | [Módulo](modules/pedidos.md) | [Códigos y canales](15_CodigosYCanalesVenta.md). |
| Ventas / pagos / Dashboard | [Módulo](modules/ventas.md) | [Dashboard](17_Fase59_Dashboard.md). |
| Catálogo público | [Módulo](modules/catalogo.md) | [Backend](25_CatalogoPublicoBackend.md), [primera UI](26_CatalogoPublicoUI.md), [marca vigente](27_VirtuosaStore.md), [V1.4 implementada; QA físico parcial](modules/catalogo-v1-4.md), [ajustes UX implementados; QA físico parcial](modules/catalogo-v1-4-ajustes-ux.md). |
| Deployment | [Dominios](deployment/domains.md) | [Runbook](deployment/deployment.md), [seguridad](deployment/security.md). |

Las entradas por módulo son mapas de trabajo: no reemplazan ni duplican las
especificaciones detalladas enlazadas. Se reutilizó el registro numerado de
decisiones en lugar de abrir una segunda colección ADR con decisiones repetidas.

## Planificación y contexto conservados

| Documento | Uso |
| --- | --- |
| [ROADMAP](../ROADMAP.md) | Versiones y evolución prevista, sin afirmar implementación. |
| [V2](19_V2_Pendientes.md) / [V3](20_V3_Pendientes.md) | Planes; distinguir ampliaciones de lo ya incorporado. |
| [Landed cost](modules/landed-cost.md) | Análisis pendiente; no hay modelo definitivo. |
| [Backlog](09_Backlog.md) | Entregas históricas por fase y pendientes. |
| [Ideas futuras](12_IdeasFuturas.md) | Exploración, no compromisos automáticos. |
| [Problema](00_Problema.md) / [objetivo](01_Objetivo.md) | Contexto del negocio. |
| [Requisitos](02_Requisitos.md) / [casos de uso](06_CasosDeUso.md) | Necesidades y alcance, con puntos futuros explícitos. |
| [Flujos](03_FlujoDelNegocio.md) / [modelo de negocio](04_ModeloDelNegocio.md) | Contexto operativo; publicación conceptual no sustituye disponibilidad de la API. |
| [Modelo de dominio](05_ModeloDelDominio.md) | Entidades del negocio. |
| [Entrevistas](13_Entrevistas.md) | Hallazgos originales; no tomar porcentajes/devoluciones como implementados. |
| [Nomenclatura](15_Nomenclatura.md) / [glosario](16_Glosario.md) | Vocabulario y separación de conceptos. |
| [Alcance V1](14_Alcance_V1.md) / [cierre V1](18_Fase510_CierreV1.md) | Alcance/evidencia de fases anteriores; no contienen todas las ampliaciones actuales. |
| [Changelog](../CHANGELOG.md) | Tags/versiones y estado Unreleased; no certifica despliegue. `.github/workflows/release.yml` publica GitHub Releases desde tags. |
| [Diagramas](diagrams/) | Material editable conservado; contrastar contratos ampliados con código/configuración EF. |
| [Auditoría documental 05/10/2026](documentation-audit.md) | Base, reorganización, contradicciones y límites de esta revisión. |

## Separación entre documentación y ejecución

Los documentos permanentes describen **decisiones, comportamiento implementado,
requisitos aprobados, reglas de negocio, arquitectura, contratos, límites y
criterios de aceptación**, distinguiendo siempre lo vigente de lo pendiente.
No contienen prompts para Codex ni instrucciones específicas de ejecución
(comandos a lanzar, orden de trabajo, creación de ramas, commits o push).

La comunicación de tareas a un agente se realiza **en el chat**, mediante un
prompt independiente que remite a los documentos pertinentes. `AGENTS.md`
conserva, por su propósito explícito, el contrato general del repositorio
para agentes; no se usa como contenedor de prompts por funcionalidad.

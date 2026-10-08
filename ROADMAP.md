# Roadmap

Resumen de planificación desde una V1 productiva. Las propuestas V2/V3 no se consideran implementadas.

## V1 — Sistema interno en producción

Sistema privado actual: clientes, productos/categorías, proveedores, compras/comprobantes, inventario/recepción, pedidos/reservas, ventas/pagos y Dashboard.

El cierre funcional y técnico se conserva como registro histórico en [Cierre V1](docs/18_Fase510_CierreV1.md). Existen los tags `v1.0.0` y `v1.0.1`, descritos en el [changelog](CHANGELOG.md); no prueban qué imagen exacta está desplegada.

Producción, dominio/HTTPS, cuentas Identity separadas y pruebas de cliente, compra y venta directa están confirmados operativamente. Backup manual y restore real fueron probados; el timer automático está activo y ya ejecutó correctamente, con retención. Las copias permanecen en el mismo VPS.

Pendientes operativos V1: copia automática externa a Raspberry/otro equipo, validación completa del rollback de versión de aplicación y verificaciones de seguridad/QA todavía sin evidencia detallada incorporada. El [runbook V1](docs/deployment/deployment.md) separa lo confirmado de lo pendiente; estas tareas no convierten el despliegue inicial en un evento futuro ni implementan V2/V3.

## V1.1 — Mejoras de Producto

**Implementada y etiquetada en Git.** Existe `v1.1.0` (`832c172`, 27/09/2026); también `v1.2.0` (`7cca3e5`, 03/10/2026) y `v1.2.1` (`835a487`, tag del 05/10/2026 UTC; commit del 04/10/2026). Los tags no prueban la versión ejecutada en producción. Medidas/presentación administrativa y captura de código de barras existen en el código.

Modelo propuesto, reglas y decisiones abiertas en [Medidas y presentación de producto](docs/23_MedidasYPresentacionProducto.md). V1.1 también incluye la captura de `Producto.CodigoBarras` con cámara durante alta y edición, conservando la entrada manual. La integración del lector en Venta Directa, Inventario y búsquedas permanece en V2.1. El catálogo público actual ya muestra medidas canónicas y presentación; las equivalencias visuales adicionales siguen pendientes. La evolución a tienda transaccional permanece en V2.4.

## Ampliaciones ya incorporadas al repositorio

La última base etiquetada, `v1.2.1`, incorpora imagen principal, catálogo público de Virtuosa Store, compras GTQ/USD y hotfix del scanner. `main` contiene además la búsqueda asistida externa por código de barras para Agregar producto y su persistencia segura de imagen; todavía no tiene un tag posterior en este documento. Consulta el [índice](docs/README.md) y el [changelog](CHANGELOG.md). No hay todavía carrito, checkout ni pedidos web; landed cost sigue en [análisis pendiente](docs/modules/landed-cost.md) y desactivación/reactivación en [decisión 025](docs/11_DecisionesDeDiseño.md#025-preservar-clientes-y-productos-con-historial).

## V1.4 — Experiencia de Virtuosa Store (aprobada; pendiente)

Rama `feature/catalogo-v1-4` **desde tag `v1.3.0`**. Alcance canónico:
[Galería de hasta 8 fotos por producto, zoom, subcategorías de dos niveles,
filtro público por marca, reconexión discreta exclusiva de catálogo, indicador
verde de disponibilidad y consulta por WhatsApp desde el detalle con número
configurable](docs/modules/catalogo-v1-4.md). La implementación
requiere cambios administrativos puntuales y migración EF, sin alterar la
operación comercial ni agregar carrito/checkout. Esta planificación no
significa que V1.4 esté implementada, etiquetada o desplegada. La rama de
V2.1 sigue independiente y su integración se decidirá después.

## V2 — Evolución planificada

- V2.1: productividad y experiencia de uso.
- V2.2: operación del negocio.
- V2.3: escalabilidad y multiusuario.
- V2.4: canal público / tienda en línea básica, con la misma fuente de inventario.

Alcance, prioridades y exclusiones en [Pendientes V2](docs/19_V2_Pendientes.md). La tienda básica corresponde a V2.4, no a una V4; ecommerce avanzado no es un requisito inicial.

## V3 — Candidatos posteriores

El candidato principal es conteo físico / toma de inventario, con revisión de diferencias y ajustes explícitos, nunca automáticos por una discrepancia de conteo.

Ver [Pendientes V3](docs/20_V3_Pendientes.md). Este resumen no sustituye ni amplía los roadmaps canónicos.

# Roadmap

Resumen de planificación desde una V1 productiva. Las propuestas V2/V3 no se consideran implementadas.

## V1 — Sistema interno en producción

Sistema privado actual: clientes, productos/categorías, proveedores, compras/comprobantes, inventario/recepción, pedidos/reservas, ventas/pagos y Dashboard.

El cierre funcional y técnico se conserva como registro histórico en [Cierre V1](docs/18_Fase510_CierreV1.md). Existen los tags `v1.0.0` y `v1.0.1`, descritos en el [changelog](CHANGELOG.md); no prueban qué imagen exacta está desplegada.

Producción, dominio/HTTPS, cuentas Identity separadas y pruebas de cliente, compra y venta directa están confirmados operativamente. Backup manual y restore real fueron probados; el timer automático está activo y ya ejecutó correctamente, con retención. Las copias permanecen en el mismo VPS.

Pendientes operativos V1: copia automática externa a Raspberry/otro equipo, validación completa del rollback de versión de aplicación y verificaciones de seguridad/QA todavía sin evidencia detallada incorporada. El [runbook V1](docs/21_Despliegue_V1.md) separa lo confirmado de lo pendiente; estas tareas no convierten el despliegue inicial en un evento futuro ni implementan V2/V3.

## V2 — Evolución planificada

- V2.1: productividad y experiencia de uso.
- V2.2: operación del negocio.
- V2.3: escalabilidad y multiusuario.
- V2.4: canal público / tienda en línea básica, con la misma fuente de inventario.

Alcance, prioridades y exclusiones en [Pendientes V2](docs/19_V2_Pendientes.md). La tienda básica corresponde a V2.4, no a una V4; ecommerce avanzado no es un requisito inicial.

## V3 — Candidatos posteriores

El candidato principal es conteo físico / toma de inventario, con revisión de diferencias y ajustes explícitos, nunca automáticos por una discrepancia de conteo.

Ver [Pendientes V3](docs/20_V3_Pendientes.md). Este resumen no sustituye ni amplía los roadmaps canónicos.

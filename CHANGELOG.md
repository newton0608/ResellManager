# Changelog

Las fechas siguientes corresponden a creación de tags; no son fechas exactas de despliegue ni acreditan una publicación en GitHub Releases.

## v1.0.1 — tag del 20/09/2026

Referencia: `97da64e`.

- Corrección de claves duplicadas de Blazor en venta directa.
- Hosting productivo con Docker/Compose y Caddy, configuración de proxy confiable, rutas persistentes, Data Protection e IP estable de la aplicación.
- Hardening del login y cabeceras de seguridad; checklist de producción con validaciones del VPS separadas de lo implementado en código.
- Script de backup consistente de SQLite, comprobantes y Data Protection, con SHA-256, reinicio, comprobación de liveness, bloqueo de ejecuciones concurrentes y retención automática.
- Servicio y timer systemd para la ejecución diaria del backup.

Estado operativo confirmado por el responsable del proyecto para la sincronización del 21/09/2026: producción con dominio/HTTPS, cuentas Identity separadas y pruebas de cliente, compra y venta directa; backup manual y restore real verificados; timer instalado, activo y con al menos una ejecución correcta. Las copias siguen en el mismo VPS. Copia automática externa y validación completa del rollback de aplicación continúan pendientes. Esta confirmación no identifica la imagen exacta desplegada ni certifica todos los controles de seguridad. Véase el [runbook operativo](docs/21_Despliegue_V1.md).

## v1.0.0 — tag del 15/09/2026

Referencia: `3f2d264`.

- Autenticación privada con Identity, sin autorregistro, y módulos de clientes, productos/categorías y proveedores.
- Compras con comprobantes privados; inventario físico, recepción parcial por compra y conservación de reservas vigentes. Pérdidas previas a recepción, irreversibles en V1 y con liberación de reserva.
- Pedidos y reservas opcionales al confirmar; ventas completas desde pedido y Venta Directa; pagos/abonos con saldo global y cancelaciones protegidas.
- Dashboard operativo; UX responsive, búsquedas, confirmaciones, filtros e historiales mensuales plegables. ClienteDetalle carga actividad mensual bajo demanda y separa pendientes actuales.
- Corrección final: completar una venta libera todas las reservas del pedido, incluidas las sustituidas, sin cambiar estados físicos de unidades no vendidas. Recepción rechaza mezclar compras.
- Cierre funcional y sus verificaciones históricas registrados en [Fase 5.10](docs/18_Fase510_CierreV1.md). Sus pendientes describen aquella fase y no el estado operativo posterior.

## 0.1.0

- Se crea el repositorio.
- Se agrega la documentación inicial.

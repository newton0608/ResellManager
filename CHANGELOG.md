# Changelog

Las fechas siguientes corresponden a creación de tags; no son fechas exactas de despliegue. Las GitHub Releases pueden publicarse después usando esos mismos tags, por lo que la fecha de una Release tampoco debe confundirse con la fecha de despliegue.

## Unreleased — main posterior a v1.2.1

- Búsqueda asistida al agregar Producto: coincidencia local primero y fallback externo Open Facts → UPCitemdb, con revisión explícita antes de copiar datos al formulario.
- Importación reversible de datos externos sin importar precios ni crear categorías; la imagen externa queda pendiente hasta Guardar producto.
- Descarga de imágenes con límites, validación de contenido, seguimiento manual de redirecciones y mitigaciones SSRF; persistencia final en el almacenamiento WebP administrado existente.
- Preview de imagen externa segura, prioridad de imagen manual y regresiones de extremo a extremo para endpoints administrativos y catálogo público.
- La cadena vulnerable de desarrollo `braces/micromatch` fue retirada mediante el override compatible de `@parcel/watcher`; `npm audit` quedó en 0 vulnerabilidades en la validación documentada.
- Validación física posterior con cámara real confirmó lectura en vivo. La combinación exacta dispositivo/navegador y la regresión específica del caso Safari de v1.2.0 no quedaron registradas, por lo que no se presentan como certificadas.

## v1.2.1 — tag del 05/10/2026 (fecha Git en UTC)

Referencia: `835a487`; commit de integración del 04/10/2026.

- Hotfix de scanner integrado a main/develop: Quagga2 local para códigos 1D,
  pruebas ópticas/JS y conservación de bytes del vendor entre plataformas.
- La validación física en iPhone/Safari sigue pendiente según
  [scanner](docs/29_BarcodeScanner.md); no confundir QA emulado con equipo real.

## v1.2.0 — tag del 03/10/2026

Referencia: `7cca3e5`.

- Base migrada a .NET 10 (commit `cb2c38d`).
- [Imagen principal](docs/24_ImagenPrincipalProducto.md) y catálogo público de
  [Virtuosa Store](docs/modules/catalogo.md), separado de administración y sin
  carrito, checkout o pedidos web.
- [Compras GTQ/USD](docs/28_MonedasDeCompra.md), con tipo aplicado congelado y
  sugerencia opcional de Banco de Guatemala. No incluye landed cost.
- Migración visual a Tailwind/componentes compartidos; scanner anterior
  sustituido posteriormente por el hotfix v1.2.1.

## v1.1.0 — tag del 27/09/2026

Referencia: `832c172`.

- [Medidas y presentación opcionales de Producto](docs/23_MedidasYPresentacionProducto.md)
  en modelo, contratos, validación, persistencia y captura administrativa.

Estos apartados se reconstruyeron desde tags, historial y código durante la
auditoría del 05/10/2026. No son builds/tests nuevos ni confirmación de despliegue.

## v1.0.1 — tag del 20/09/2026

Referencia: `97da64e`.

- Corrección de claves duplicadas de Blazor en venta directa.
- Hosting productivo con Docker/Compose y Caddy, configuración de proxy confiable, rutas persistentes, Data Protection e IP estable de la aplicación.
- Hardening del login y cabeceras de seguridad; checklist de producción con validaciones del VPS separadas de lo implementado en código.
- Script de backup consistente de SQLite, comprobantes y Data Protection, con SHA-256, reinicio, comprobación de liveness, bloqueo de ejecuciones concurrentes y retención automática.
- Servicio y timer systemd para la ejecución diaria del backup.

Estado operativo confirmado por el responsable del proyecto para la sincronización del 21/09/2026: producción con dominio/HTTPS, cuentas Identity separadas y pruebas de cliente, compra y venta directa; backup manual y restore real verificados; timer instalado, activo y con al menos una ejecución correcta. Las copias siguen en el mismo VPS. Copia automática externa y validación completa del rollback de aplicación continúan pendientes. Esta confirmación no identifica la imagen exacta desplegada ni certifica todos los controles de seguridad. Véase el [runbook operativo](docs/deployment/deployment.md).

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

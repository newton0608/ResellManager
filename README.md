# ResellManager

Aplicación web para administrar inventario, clientes, ventas y cuentas por cobrar de un negocio de reventa.

Para trabajar con agentes, comienza por [AGENTS.md](AGENTS.md) y consulta el
[índice de documentación](docs/README.md) según el área de la tarea.

## Estado del proyecto

La última versión etiquetada es `v1.2.1` (`835a487`, 05/10/2026), pero `main` ya contiene cambios posteriores: búsqueda asistida de productos por código de barras con Open Facts/UPCitemdb, revisión antes de importar y persistencia segura de imágenes externas. El código usa .NET 10 e incluye además medidas/presentación, imagen principal, catálogo público, compras GTQ/USD y scanner Quagga2. El tag identifica una versión del repositorio; no acredita por sí solo la imagen exacta desplegada.

ResellManager V1 implementa autenticación privada, clientes, productos/categorías, proveedores, compras y comprobantes privados, inventario/recepción, pedidos y reservas, ventas, pagos y Dashboard. La Fase 5.10 cerró consistencia y UX sin incorporar funcionalidades grandes ni concurrencia fuerte V2.

Consulta el [alcance V1](docs/14_Alcance_V1.md), las [decisiones](docs/11_DecisionesDeDiseño.md) y el [registro histórico de cierre y validación](docs/18_Fase510_CierreV1.md).

ResellManager está en producción, con dominio y HTTPS funcionando y cuentas Identity separadas para los usuarios actuales. Se probaron cliente, compra y venta directa. Estos hechos operativos fueron confirmados por el responsable del proyecto para la sincronización documental del 21/09/2026; no certifican roles/permisos, concurrencia avanzada ni todos los controles de seguridad.

Existen los tags `v1.0.0` (`3f2d264`) y `v1.0.1` (`97da64e`); este último incluye la corrección de claves duplicadas de Blazor en venta directa. La existencia de un tag no identifica por sí sola la imagen o commit que ejecuta el VPS. Véase el [changelog](CHANGELOG.md).

El [runbook operativo V1](docs/deployment/deployment.md) distingue configuración versionada y evidencia operativa. Se probaron backup manual y restore real; el timer systemd está instalado, activo y ya ejecutó correctamente, con retención automática. Las copias permanecen en el mismo VPS. Siguen pendientes la copia automática externa a Raspberry/otro equipo y la validación completa del rollback de versión de aplicación.

El [backend del catálogo público](docs/25_CatalogoPublicoBackend.md) ofrece lecturas comerciales de productos con inventario físico libre mediante `/api/catalogo/productos`, incluido detalle e imagen pública controlada. La [primera UI pública](docs/26_CatalogoPublicoUI.md) sigue declarada en Blazor como `/catalogo` y `/catalogo/{productoId}`, con búsqueda, filtro por categoría, precios en quetzales y diseño responsive integrado con Tailwind. La administración conserva su autenticación y los flujos del negocio permanecen intactos. La [identidad de Virtuosa Store](docs/27_VirtuosaStore.md) documenta logos y estilos `store-*`. Operacionalmente, `https://virtuosagt.com/` sirve el catálogo y `/producto/{id}` es la URL pública limpia mediante routing externo de Caddy; `https://app.resellmanager.tech` sigue siendo administración y `https://preview.newtonlab.dev/catalogo` conserva el preview. La diferencia entre rutas Blazor, routing externo y deuda de versionado de Caddy se documenta en [Dominios](docs/deployment/domains.md). No hay carrito, checkout ni pedidos web.

## Versiones

El historial curado se mantiene en [CHANGELOG.md](CHANGELOG.md). Los tags `v*` son la referencia de versión y pueden publicarse además como GitHub Releases. El workflow `.github/workflows/release.yml` publica automáticamente tags nuevos y permite publicar manualmente un tag histórico ya existente.

| Referencia | Estado | Resumen |
| --- | --- | --- |
| `main` posterior a `v1.2.1` | En desarrollo / aún sin tag | Búsqueda asistida por código, proveedores externos e imágenes importadas de forma segura. |
| `v1.2.1` | Tag | Hotfix del scanner Quagga2. |
| `v1.2.0` | Tag | .NET 10, imagen principal, catálogo público, GTQ/USD y migración visual. |
| `v1.1.0` | Tag | Medidas y presentación de Producto. |
| `v1.0.1` | Tag | Hotfix de venta directa y preparación operativa de producción. |
| `v1.0.0` | Tag | Primera versión funcional V1. |

## Estructura

```text
ResellManager.sln
├── src/ResellManager.Web             # Host Blazor Web App y endpoints de autenticación
├── src/ResellManager.Domain          # Entidades y reglas del dominio
├── src/ResellManager.Application     # Contratos, DTOs y validaciones compartidas
└── src/ResellManager.Infrastructure  # EF Core, SQLite e Identity
```

Las dependencias respetan la dirección de la arquitectura:

- `ResellManager.Application` depende de `ResellManager.Domain`.
- `ResellManager.Infrastructure` depende de `ResellManager.Application`.
- `ResellManager.Web` depende de `ResellManager.Application` e `ResellManager.Infrastructure`.

## Tecnologías

- .NET 10
- ASP.NET Core Blazor Web App (renderizado interactivo de servidor)
- Entity Framework Core 10
- SQLite
- ASP.NET Core Identity

## Requisitos

- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0)

## Compilar y ejecutar

Desde la raíz del repositorio:

```bash
dotnet restore
dotnet build ResellManager.sln
dotnet test ResellManager.sln
dotnet run --project src/ResellManager.Web/ResellManager.Web.csproj
```

La aplicación usa SQLite con la cadena `Data Source=resellmanager.db`, configurable en `src/ResellManager.Web/appsettings.json` o `ConnectionStrings__ResellManager`. Al iniciar aplica las migraciones existentes, haya o no configuración del usuario inicial. Usa una ruta absoluta si necesitas controlar exactamente qué base abrir, y respáldala antes de actualizar una instalación con datos.

Para crear la primera cuenta, configura `UsuarioInicial:Correo` y `UsuarioInicial:Contrasena` mediante User Secrets o variables de entorno, siguiendo la decisión 016. Retira esas credenciales de configuración después; la cuenta existente no se modifica. No hay autorregistro ni contraseñas predeterminadas de producción.

Los comprobantes se guardan en `App_Data` por defecto, fuera de `wwwroot`. `AlmacenamientoComprobantes__DirectorioBase` permite una carpeta privada distinta; respalda esa carpeta junto con SQLite y, en producción, el key ring persistente de Data Protection. La ruta `/comprobantes/{compraId}` exige sesión autenticada. El informe de cierre conserva la preparación pendiente en aquella fase; la operación posterior se describe en el runbook. Este repositorio no despliega a producción automáticamente.

Para construir y ejecutar la imagen .NET 10 con Caddy, consulta el
[runbook de despliegue V1](docs/deployment/deployment.md#construcción-y-arranque-en-ubuntu).
Incluye configuración externa, permisos, backups y restauración, hechos operativos confirmados y verificaciones pendientes.

## Tailwind CSS v4 (infraestructura)

`wwwroot/app.css` conserva las bases globales y estados de reconexión; los componentes administrativos usan el sistema compartido Tailwind. La fuente `src/ResellManager.Web/Styles/tailwind.css` importa únicamente `theme.css` y `utilities.css`, sin Preflight ni reset global, siguiendo la [documentación de Tailwind v4](https://tailwindcss.com/docs/preflight#disabling-preflight). En `Components/App.razor`, el CSS compilado se carga después de `app.css`. Las utilities se importan sin capa; `Styles/storefront.css` define los estilos públicos `store-*` y los componentes administrativos reutilizan `ui-*`. No se agrega un reset global.

Tailwind y su CLI están fijados en `4.3.3`. Los scripts precargan `scripts/tailwind-resolver.mjs`: en rutas que contienen `#` (como `D:\C#\...`), resuelve los dos imports CSS del paquete con Node mediante el hook `__tw_resolve` de Tailwind. Esto evita que `enhanced-resolve` entregue rutas con bytes NUL y mantiene la CLI oficial para build/watch; en Docker, cuya ruta no contiene `#`, se usa el resolutor normal. Este hook es interno: verifica esta compatibilidad al actualizar Tailwind.

Desde la raíz, con Node.js 24 y npm:

```bash
npm install
npm run css:build
```

Durante desarrollo, ejecuta el watcher en una terminal y Blazor en otra:

```bash
npm run css:watch
```

```bash
dotnet run --project src/ResellManager.Web/ResellManager.Web.csproj
```

`css:watch` regenera `src/ResellManager.Web/wwwroot/css/tailwind.css` al cambiar las fuentes. `css:build` produce el mismo archivo minificado para producción. El CSS compilado se versiona como asset normal: `dotnet run` y `dotnet build` no ejecutan npm ni necesitan Node o un watcher activo. No edites el archivo generado; ejecuta `npm run css:build` y conserva su actualización cuando agregues utilities. `node_modules/` ya está excluido por `.gitignore`; `package-lock.json` se versiona.

El [escaneo de fuentes](https://tailwindcss.com/docs/detecting-classes-in-source-files) usa `source(none)` y `@source` con rutas relativas al CSS fuente: todos los `.razor`, `.cshtml`, `.html`, `.cs` y `.js` de `src/ResellManager.Web`. Se excluyen `bin`, `obj` y `wwwroot/vendor`; no se escanean el CSS existente, los paquetes npm ni otras capas de la solución. Usa nombres completos y literales (por ejemplo, `text-red-600`), también en condiciones Razor/C#; las concatenaciones como `text-@color-600` no son detectables. No hay `tailwind.config.js` ni configuración `content` de v3.

Para publicar fuera de Docker, genera el CSS **antes** de publicar:

```bash
npm ci --include=dev
npm run css:build
dotnet publish src/ResellManager.Web/ResellManager.Web.csproj -c Release
```

El Dockerfile usa una etapa `node:24-bookworm-slim`, instala las versiones del lockfile con `npm ci --include=dev` y ejecuta `npm run css:build`. css:build también sincroniza el bundle local de Quagga2 1.11.0 y su licencia desde el paquete npm fijado. La etapa SDK .NET 10 copia ese CSS y el vendor generado antes de `dotnet publish`; la imagen final sigue basada en ASP.NET Core 10 y recibe solo la aplicación publicada, sin Node, npm ni `node_modules`. `.dockerignore` excluye el CSS compilado local para construirlo siempre desde las fuentes.

```bash
docker build -t resellmanager:local .
```

## Branding oficial de ResellManager

Los assets entregados se conservan como copias exactas en `src/ResellManager.Web/wwwroot/branding/`, sin redibujar, recolorear, recortar ni recomprimir los archivos. Se conservan las seis fotos JPG y las seis versiones PNG transparentes; la aplicación usa los PNG directamente.

| Nombre base (extensiones `.jpg` y `.png`) | Foto JPG | PNG recibido | Uso |
| --- | --- | --- | --- |
| `resellmanager-logo-dark` | 1 | 6 | Horizontal oscuro: sidebar, menú móvil, topbar móvil, login y página de error, sobre fondos claros. |
| `resellmanager-logo-light` | 6 | 2 | Horizontal claro: conservado para futuros fondos oscuros. |
| `resellmanager-icon` | 2 | 3 | Símbolo sin texto: favicon PNG, sin generar tamaños o variantes adicionales. |
| `resellmanager-logo-monochrome-stacked` | 3 | 4 | Variante vertical monocroma conservada. |
| `resellmanager-logo-dark-stacked` | 4 | 5 | Variante vertical oscura conservada. |
| `resellmanager-logo-light-stacked` | 5 | 1 | Variante vertical clara conservada. |

Los logos horizontales ya incluyen el nombre; se retira el texto provisional que aparecía junto al símbolo R. Los componentes controlan tamaño y adaptación de los logos; conservar los assets originales y comprobar el encuadre del componente afectado. El favicon se declara en `Components/App.razor` como `branding/resellmanager-icon.png`.

## Interfaz base

El layout, la navegación, la topbar y el dashboard usan utilities de Tailwind con superficies blancas, fondo `#F2F2F7`, esquinas redondeadas y sombras suaves. La fuente es `-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`, sin descargas. Los tokens `ui-*` se definen con `@theme` en `Styles/tailwind.css`; el azul `#0066D6` y el secundario `#63636B` mejoran el contraste de textos pequeños.

`ui-button`, `ui-card` y `ui-input` son utilities compartidas. Los módulos administrativos ya fueron migrados a controles compartidos y Tailwind; se conserva CSS base, incluido el tratamiento del selector nativo de fechas iOS. El catálogo utiliza la identidad `store-*` descrita en su guía.

En móvil se mantiene el menú nativo `popover`, con cierre al navegar, controles de al menos 44–48 px y márgenes reducidos. El dashboard presenta los canales como cards en móvil y los movimientos recientes como cards en todas las resoluciones. Solo la topbar y el fondo del menú utilizan blur; las transiciones respetan `prefers-reduced-motion`.

El [soporte GTQ/USD en compras](docs/28_MonedasDeCompra.md) conserva GTQ como moneda base de inventario, ventas, utilidad, pagos y Dashboard. Banguat ofrece una sugerencia opcional; el tipo aplicado queda congelado al registrar la compra.

## Scanner de códigos de producto

El scanner usa Quagga2 1.11.0 local para EAN-13/EAN-8/UPC-A/UPC-E/CODE-128, con foto local y doble confirmación en vivo. La validación física posterior confirmó lectura en vivo con **Brave en un iPhone 14 Plus**; la regresión histórica específica de Safari sigue documentada por separado en [su guía técnica](docs/29_BarcodeScanner.md).

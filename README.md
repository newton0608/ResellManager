# ResellManager

Aplicación web para administrar inventario, clientes, ventas y cuentas por cobrar de un negocio de reventa.

## Estado del proyecto

ResellManager V1 implementa autenticación privada, clientes, productos/categorías, proveedores, compras y comprobantes privados, inventario/recepción, pedidos y reservas, ventas, pagos y Dashboard. La Fase 5.10 cerró consistencia y UX sin incorporar funcionalidades grandes ni concurrencia fuerte V2.

Consulta el [alcance V1](docs/14_Alcance_V1.md), las [decisiones](docs/11_DecisionesDeDiseño.md) y el [registro histórico de cierre y validación](docs/18_Fase510_CierreV1.md).

ResellManager está en producción, con dominio y HTTPS funcionando y cuentas Identity separadas para los usuarios actuales. Se probaron cliente, compra y venta directa. Estos hechos operativos fueron confirmados por el responsable del proyecto para la sincronización documental del 21/09/2026; no certifican roles/permisos, concurrencia avanzada ni todos los controles de seguridad.

Existen los tags `v1.0.0` (`3f2d264`) y `v1.0.1` (`97da64e`); este último incluye la corrección de claves duplicadas de Blazor en venta directa. La existencia de un tag no identifica por sí sola la imagen o commit que ejecuta el VPS. Véase el [changelog](CHANGELOG.md).

El [runbook operativo V1](docs/21_Despliegue_V1.md) distingue configuración versionada y evidencia operativa. Se probaron backup manual y restore real; el timer systemd está instalado, activo y ya ejecutó correctamente, con retención automática. Las copias permanecen en el mismo VPS. Siguen pendientes la copia automática externa a Raspberry/otro equipo y la validación completa del rollback de versión de aplicación.

El [backend del catálogo público](docs/25_CatalogoPublicoBackend.md) ofrece lecturas comerciales de productos con inventario físico libre mediante `/api/catalogo/productos`, incluido detalle e imagen pública controlada. La [primera UI pública](docs/26_CatalogoPublicoUI.md) está disponible sin sesión en `/catalogo` y `/catalogo/{productoId}`, con búsqueda, filtro por categoría, precios en quetzales y diseño responsive integrado con Tailwind. La administración conserva su autenticación y los flujos del negocio permanecen intactos. La [identidad de Virtuosa Store y la preparación del dominio propio](docs/27_VirtuosaStore.md) documentan los logos originales, los estilos `store-*` y el routing futuro hacia la misma aplicación.

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

- .NET 8
- ASP.NET Core Blazor Web App (renderizado interactivo de servidor)
- Entity Framework Core 8
- SQLite
- ASP.NET Core Identity

## Requisitos

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)

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

Para construir y ejecutar la imagen .NET 8 con Caddy, consulta el
[runbook de despliegue V1](docs/21_Despliegue_V1.md#construcción-y-arranque-en-ubuntu).
Incluye configuración externa, permisos, backups y restauración, hechos operativos confirmados y verificaciones pendientes.

## Tailwind CSS v4 (infraestructura)

Se conservan `wwwroot/app.css` y los estilos aislados de Blazor para los módulos pendientes de migración. La fuente `src/ResellManager.Web/Styles/tailwind.css` importa únicamente `theme.css` y `utilities.css`, sin Preflight ni reset global, siguiendo la [documentación de Tailwind v4](https://tailwindcss.com/docs/preflight#disabling-preflight). En `Components/App.razor`, el CSS compilado se carga después de `app.css` y de los estilos aislados. Las utilities se importan sin capa para que puedan sobrescribir las reglas legacy al migrar un componente; no se usa `important`. Los controles compartidos se adaptan únicamente dentro de `.rm-ui`.

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

Los logos horizontales ya incluyen el nombre; se retira el texto provisional que aparecía junto al símbolo R. En los logos, Tailwind controla el tamaño, la adaptación al ancho disponible y `object-fit: cover`: el encuadre oculta los márgenes exteriores del lienzo y mantiene la proporción del dibujo. El favicon se declara en `Components/App.razor` como `branding/resellmanager-icon.png`.

## Interfaz base

El layout, la navegación, la topbar y el dashboard usan utilities de Tailwind con superficies blancas, fondo `#F2F2F7`, esquinas redondeadas y sombras suaves. La fuente es `-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`, sin descargas. Los tokens `ui-*` se definen con `@theme` en `Styles/tailwind.css`; el azul `#0066D6` y el secundario `#63636B` mejoran el contraste de textos pequeños.

`ui-button`, `ui-card` y `ui-input` son utilities compartidas. Los nombres legacy de botones, cards, inputs/selects, badges y mensajes reutilizan estas reglas con `@apply` dentro de `.rm-ui`, sin migrar el marcado de Productos, Clientes, Compras, Pedidos o Ventas. Se conserva el CSS legacy, incluidos los ajustes del selector de fechas nativo de iOS.

En móvil se mantiene el menú nativo `popover`, con cierre al navegar, controles de al menos 44–48 px y márgenes reducidos. El dashboard presenta los canales como cards en móvil y los movimientos recientes como cards en todas las resoluciones. Solo la topbar y el fondo del menú utilizan blur; las transiciones respetan `prefers-reduced-motion`.

El [soporte GTQ/USD en compras](docs/28_MonedasDeCompra.md) conserva GTQ como moneda base de inventario, ventas, utilidad, pagos y Dashboard. Banguat ofrece una sugerencia opcional; el tipo aplicado queda congelado al registrar la compra.

## Scanner de códigos de producto

El scanner usa Quagga2 1.11.0 local para EAN-13/EAN-8/UPC-A/UPC-E/CODE-128, con foto local y doble confirmación en vivo. Consulta [su documentación técnica y validación pendiente en iPhone/Safari](docs/29_BarcodeScanner.md).

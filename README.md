# ResellManager

Aplicación web para administrar inventario, clientes, ventas y cuentas por cobrar de un negocio de reventa.

## Estado del proyecto

ResellManager V1 implementa autenticación privada, clientes, productos/categorías, proveedores, compras y comprobantes privados, inventario/recepción, pedidos y reservas, ventas, pagos y Dashboard. La Fase 5.10 cerró consistencia y UX sin incorporar funcionalidades grandes ni concurrencia fuerte V2.

Consulta el [alcance V1](docs/14_Alcance_V1.md), las [decisiones](docs/11_DecisionesDeDiseño.md) y el [registro histórico de cierre y validación](docs/18_Fase510_CierreV1.md).

ResellManager está en producción, con dominio y HTTPS funcionando y cuentas Identity separadas para los usuarios actuales. Se probaron cliente, compra y venta directa. Estos hechos operativos fueron confirmados por el responsable del proyecto para la sincronización documental del 21/09/2026; no certifican roles/permisos, concurrencia avanzada ni todos los controles de seguridad.

Existen los tags `v1.0.0` (`3f2d264`) y `v1.0.1` (`97da64e`); este último incluye la corrección de claves duplicadas de Blazor en venta directa. La existencia de un tag no identifica por sí sola la imagen o commit que ejecuta el VPS. Véase el [changelog](CHANGELOG.md).

El [runbook operativo V1](docs/21_Despliegue_V1.md) distingue configuración versionada y evidencia operativa. Se probaron backup manual y restore real; el timer systemd está instalado, activo y ya ejecutó correctamente, con retención automática. Las copias permanecen en el mismo VPS. Siguen pendientes la copia automática externa a Raspberry/otro equipo y la validación completa del rollback de versión de aplicación.

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

Se conservan `wwwroot/app.css`, los estilos aislados de Blazor y todas las clases actuales. La fuente `src/ResellManager.Web/Styles/tailwind.css` importa únicamente `theme.css` y `utilities.css`, sin Preflight ni reset global, siguiendo la [documentación de Tailwind v4](https://tailwindcss.com/docs/preflight#disabling-preflight). El enlace al CSS compilado se carga antes de los estilos existentes en `Components/App.razor`. Las reglas existentes sin capa conservan prioridad sobre las utilities; no se usa `important`.

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

El Dockerfile usa una etapa `node:24-bookworm-slim`, instala las versiones del lockfile con `npm ci --include=dev` y ejecuta `npm run css:build`. La etapa SDK .NET 10 copia ese CSS antes de `dotnet publish`; la imagen final sigue basada en ASP.NET Core 10 y recibe solo la aplicación publicada, sin Node, npm ni `node_modules`. `.dockerignore` excluye el CSS compilado local para construirlo siempre desde las fuentes.

```bash
docker build -t resellmanager:local .
```

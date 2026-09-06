# ResellManager

Aplicación web para administrar inventario, clientes, ventas y cuentas por cobrar de un negocio de reventa.

## Estado del proyecto

ResellManager V1 implementa autenticación privada, clientes, productos/categorías, proveedores, compras y comprobantes privados, inventario/recepción, pedidos y reservas, ventas, pagos y Dashboard. La Fase 5.10 cierra consistencia y UX sin incorporar funcionalidades grandes ni concurrencia fuerte V2.

Consulta el [alcance V1](docs/14_Alcance_V1.md), las [decisiones](docs/11_DecisionesDeDiseño.md) y el [informe de cierre y validación](docs/18_Fase510_CierreV1.md).

## Estructura

```text
ResellManager.sln
├── src/ResellManager.Web             # Host Blazor Web App y endpoints de autenticación
├── src/ResellManager.Domain          # Entidades y reglas del dominio
├── src/ResellManager.Application     # Casos de uso y abstracciones
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

Los comprobantes se guardan en `App_Data` por defecto, fuera de `wwwroot`. `AlmacenamientoComprobantes__DirectorioBase` permite una carpeta privada distinta; respalda esa carpeta junto con SQLite. La ruta `/comprobantes/{compraId}` exige sesión autenticada. El informe de cierre incluye la preparación operativa pendiente; este repositorio no despliega a producción automáticamente.

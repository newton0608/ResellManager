# ResellManager

Aplicación web para administrar inventario, clientes, ventas y cuentas por cobrar de un negocio de reventa.

## Estado del proyecto

La **Fase 1** establece una solución limpia en .NET 8 con Blazor Web App, una arquitectura por capas y la configuración inicial de persistencia y autenticación. Todavía no incluye entidades del negocio, migraciones ni pantallas funcionales finales.

## Estructura

```text
ResellManager.sln
├── src/ResellManager.Web             # Host Blazor Web App y endpoints de autenticación
├── src/ResellManager.Domain          # Futuras entidades y reglas del dominio
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
dotnet run --project src/ResellManager.Web/ResellManager.Web.csproj
```

La aplicación usa SQLite con la cadena `Data Source=resellmanager.db`, configurable en `src/ResellManager.Web/appsettings.json`. La base de datos y sus migraciones se incorporarán cuando se modele el dominio en una fase posterior.

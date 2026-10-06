# Arquitectura

**Estado: arquitectura implementada, auditada el 05/10/2026.** La solución usa
.NET 10 (`Directory.Build.props`), Blazor Web App InteractiveServer, EF Core 10,
SQLite e Identity. Las implementaciones actuales de casos de uso están en
Infrastructure; no se presupone una capa Application con implementaciones que
el repositorio todavía no tiene.

Referencias actuales entre proyectos (`A → B` significa que A referencia B):

- `Application → Domain`
- `Infrastructure → Application`
- `Web → Application`
- `Web → Infrastructure`

`Domain` no depende de `Infrastructure`. Estas referencias describen dependencias de proyectos, no una secuencia de ejecución.

El [diagrama de arquitectura](../diagrams/08_Arquitectura.drawio) está sincronizado con las dependencias vigentes descritas aquí y en los archivos de proyecto.



- Blazor

- ASP.NET Core

- Entity Framework

- SQLite

- ASP.NET Identity


## Presentación:
Blazor Web App. Interfaz utilizada desde navegador.

## Aplicación:
Contratos de casos de uso y servicios, DTOs y validaciones compartidas.

## Dominio:
Entidades principales y reglas del negocio.

## Infraestructura:
Implementaciones principales de servicios, persistencia con Entity Framework Core y SQLite, almacenamiento de archivos e integración de persistencia de Identity.

## Domain
- Entidades
- Enums
- Reglas simples del dominio

## Application
- Interfaces de servicios y casos de uso
- DTOs
- Validaciones y utilidades compartidas

## Infrastructure
- Implementaciones principales de servicios y casos de uso
- DbContext y configuraciones de Entity Framework Core
- Persistencia SQLite
- Almacenamiento de comprobantes
- Persistencia de Identity

## Web
- Blazor
- Identity
- Páginas
- Componentes

## Puntos de entrada y límites

- [Program.cs](../../src/ResellManager.Web/Program.cs) compone hosting, Identity,
  endpoints y Blazor; [DependencyInjection](../../src/ResellManager.Infrastructure/DependencyInjection.cs)
  registra servicios y persistencia. No se cambia esa composición por una tarea
  de contenido o UI que no lo requiera.
- [Domain](../../src/ResellManager.Domain/) conserva entidades/enums sin depender
  de las otras capas; [Application](../../src/ResellManager.Application/) expone
  interfaces, DTOs, ServiceResult, validaciones/helpers compartidos.
- [Infrastructure](../../src/ResellManager.Infrastructure/) implementa casos de
  uso, consultas EF, almacenamiento administrado y referencia de Banguat.
  Reutiliza sus servicios y transacciones; no copies lógica al frontend.
- [Web](../../src/ResellManager.Web/) adapta contratos a endpoints y presentación.
  Razor puede manejar estado visual, formato y validación preventiva; las
  invariantes, acceso EF y cálculos financieros permanecen en servidor.
- La administración exige sesión; el [catálogo público](../modules/catalogo.md)
  usa contratos de lectura comerciales explícitos y componentes anónimos fuera
  de `Components/Pages`. Misma fuente de inventario no significa mismos DTOs.
- No ejecutes operaciones EF concurrentes sobre el mismo DbContext scoped.
  La coordinación multiusuario fuerte continúa planificada, no implementada.

## Pruebas y desarrollo

[ResellManager.Tests](../../tests/ResellManager.Tests/) usa xUnit, SQLite real
en memoria/directorios aislados, HtmlRenderer y WebApplicationFactory.
[TestDatabase](../../tests/ResellManager.Tests/TestDatabase.cs) y
[PruebaWebAislada](../../tests/ResellManager.Tests/PruebaWebAislada.cs) son patrones
existentes; revisa los tests del módulo antes de crear un arnés alternativo.
Las pruebas JS están en `tests/` y `tests/ResellManager.Tests/`, con Node test runner.
Comandos y requisitos de validación: [AGENTS.md](../../AGENTS.md) y
[README](../../README.md). No hay workflow CI versionado en `.github/workflows`.

Contratos relacionados: [reglas](domain-rules.md), [persistencia](persistence.md),
[decisiones](../11_DecisionesDeDiseño.md) y [mapa de módulos](../README.md).

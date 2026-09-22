# Arquitectura

Referencias actuales entre proyectos (`A → B` significa que A referencia B):

- `Application → Domain`
- `Infrastructure → Application`
- `Web → Application`
- `Web → Infrastructure`

`Domain` no depende de `Infrastructure`. Estas referencias describen dependencias de proyectos, no una secuencia de ejecución.

El [diagrama de arquitectura](diagrams/08_Arquitectura.drawio) está sincronizado con las dependencias vigentes descritas aquí y en los archivos de proyecto.



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
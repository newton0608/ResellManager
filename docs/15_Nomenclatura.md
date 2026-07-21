# Nomenclatura

Este documento define las convenciones utilizadas durante el desarrollo del proyecto ResellManager.

## Idioma

- El código fuente estará escrito en español.
- La documentación del proyecto estará escrita en español.
- Los nombres de clases, interfaces, métodos, propiedades, variables, enumeraciones y tablas seguirán nombres en español.
- Los comentarios en el código deberán escribirse en inglés únicamente cuando sean necesarios; se priorizará un código autoexplicativo.
- Los textos visibles para el usuario (botones, mensajes, etiquetas, etc.) estarán en español en la primera versión del sistema.

## General

- Todo el código estará escrito en inglés.
- La documentación estará escrita en español.
- Los nombres deben ser descriptivos.
- Evitar abreviaturas innecesarias.
- Mantener una nomenclatura consistente en todo el proyecto.

---

## Clases

- Utilizar PascalCase.
- Nombrar las clases en singular.

Ejemplos:

Cliente
Producto
UnidadInventario
Compra
Venta

---

## Interfaces

Prefijo I.

Ejemplos:

IProductoRepository
IClienteService

---

## Métodos

Utilizar PascalCase.

Ejemplos:

RegistrarCompra()
BuscarCliente()
CalcularSaldo()

Los métodos deben comenzar con un verbo.

---

## Propiedades

Utilizar PascalCase.

Ejemplos:

Nombre
PrecioVenta
FechaCompra
CodigoBarras

---

## Variables locales

Utilizar camelCase.

Ejemplos:

cliente
producto
totalVenta

---

## Constantes

Utilizar PascalCase.

Ejemplos:

MaxIntentos
DiasGarantia

---

## Enumeraciones

Utilizar PascalCase.

Ejemplo:

EstadoUnidadInventario

Valores:

Disponible
Apartado
EnTransito
Vendido
Entregado

---

## Base de datos

Las tablas estarán en singular.

Ejemplos:

Cliente
Producto
UnidadInventario
Compra

---

## Llaves primarias

Siempre utilizar:

Id

Ejemplo:

Producto

Id

Nombre

---

## Llaves foráneas

NombreEntidadId

Ejemplos:

ProductoId
CompraId
ProveedorId
ClienteId

---

## Fechas

Prefijo Fecha.

Ejemplos:

FechaCompra
FechaEntrega
FechaRegistro

---

## Valores monetarios

Prefijo:

Costo
Precio
Total
Saldo

Ejemplos:

CostoUnitario
PrecioVenta
TotalCompra
SaldoPendiente

---

## Archivos

Utilizar PascalCase.

Ejemplos:

Producto.cs
CompraService.cs
ClienteRepository.cs

---

## Carpetas

Utilizar PascalCase.

Ejemplos:

Models
Services
Repositories
Components
Pages

---

## Commits

Seguir el formato:

tipo: descripción

Ejemplos:

feat: agregar registro de compras
fix: corregir cálculo del saldo
docs: actualizar modelo del dominio
refactor: simplificar repositorio de productos


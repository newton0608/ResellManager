# Nomenclatura

Este documento define las convenciones utilizadas durante el desarrollo del proyecto ResellManager.

## Idioma

- Los conceptos y entidades del dominio se nombran principalmente en español.
- La documentación del proyecto estará escrita en español.
- Los nombres del dominio mantienen el vocabulario del negocio; nombres propios del framework y conceptos técnicos como `Service`, `Dto`, `DbContext` o `Async` pueden permanecer en inglés.
- Los comentarios en el código deberán escribirse en inglés únicamente cuando sean necesarios; se priorizará un código autoexplicativo.
- Los textos visibles para el usuario (botones, mensajes, etiquetas, etc.) estarán en español en la primera versión del sistema.

## General

- Aplicar la convención de idioma anterior de forma consistente; no traducir nombres del framework ni renombrar código existente por esta sincronización documental.
- La documentación estará escrita en español.
- Los nombres deben ser descriptivos.
- Evitar abreviaturas innecesarias.
- Mantener una nomenclatura consistente en todo el proyecto.

---

## Clases

- Utilizar PascalCase.
- Nombrar las clases de entidad en singular.

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

Comprada
EnTransito
Disponible
Vendida
Entregada
Perdida

`EnvioHermano` es el identificador persistido de `OrigenCompra`; «Envío del hijo» es su etiqueta comercial. No se cambia el enum para hacerlo coincidir con la etiqueta.

Las reservas se nombran mediante DetallePedidoReservaId; Apartada no pertenece a EstadoUnidadInventario.

---

## Base de datos

Las tablas comerciales actuales se mapean en plural mediante EF, mientras las entidades permanecen en singular.

Ejemplos:

Clientes
Productos
UnidadesInventario
Compras
Pedidos
Ventas

Esta descripción no propone renombres de tablas ni migraciones. Los nombres de tablas propios de Identity conservan las convenciones del framework.

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


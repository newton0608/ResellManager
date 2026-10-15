# V2.1 — Scanner operativo en Venta Directa e Inventario

**Estado: requisitos aprobados; implementación pendiente (06/10/2026).**

Este documento define el comportamiento que debe implementar V2.1 al reutilizar
el scanner de códigos de barras existente en dos flujos operativos:
**Venta Directa** e **Inventario**. Es una especificación de producto/UX para una
implementación posterior; no describe funcionalidad ya disponible.

El motor, formatos soportados, privacidad, modos de captura y contrato actual de
`BarcodeScanner` siguen definidos en [Scanner de códigos de producto](../29_BarcodeScanner.md).
No deben duplicarse ni reemplazarse para esta tarea.

## Objetivo

Reducir el tiempo necesario para localizar mercancía durante una venta presencial
o una consulta de inventario, usando `Producto.CodigoBarras` como acceso rápido
a datos que **ya existen localmente en ResellManager**.

El scanner operativo no registra productos, no consulta proveedores externos y
no sustituye la búsqueda manual.

## Principios comunes

- La cámara se activa únicamente por acción explícita de la usuaria.
- Reutilizar el componente `BarcodeScanner` vigente y su `OnDetected`; no crear
  otro decoder, otro flujo JS ni otra dependencia de lectura.
- El valor confirmado por el scanner se busca como `Producto.CodigoBarras`.
- La coincidencia operativa es local. **No consultar Open Facts, UPCitemdb ni
  ningún otro proveedor externo** desde Venta Directa o Inventario.
- Si el código no corresponde a un producto registrado, informar claramente y
  permitir continuar con la búsqueda manual.
- Cancelar/cerrar el scanner no modifica la venta, el inventario ni los filtros.
- La búsqueda manual actual permanece disponible en ambos flujos.
- No generar, normalizar silenciosamente ni reemplazar códigos de barras.
- No cambiar esquema, migraciones, estados de dominio ni reglas de disponibilidad
  para implementar esta mejora.
- Los componentes Razor coordinan UI; consultas de inventario y reglas de
  elegibilidad deben permanecer en servicios/contratos de aplicación e
  infraestructura, sin introducir `DbContext` en Razor.

---

# Venta Directa

## Experiencia aprobada

El scanner debe comportarse como un acceso rápido de caja: identifica el
producto y permite agregar varias unidades físicas de ese producto sin escanear
el mismo código repetidamente.

Flujo:

1. La usuaria abre **Venta Directa**.
2. Selecciona **Escanear código**.
3. Confirma un código desde el `BarcodeScanner`.
4. ResellManager busca una coincidencia local exacta por
   `Producto.CodigoBarras`.
5. Si el producto existe, calcula cuántas **unidades elegibles para Venta
   Directa** quedan disponibles para agregar.
6. Se presenta el producto y la cantidad disponible, por ejemplo:
   **“Producto X · 7 unidades disponibles”**.
7. Se pregunta **cuántas unidades desea agregar**. El valor inicial es `1` y el
   máximo es la disponibilidad calculada.
8. Al confirmar, ResellManager agrega automáticamente esa cantidad de
   `UnidadInventario` concretas a la venta.
9. Las unidades agregadas entran al flujo actual: precio final editable,
   posibilidad de quitar artículos, revisión y confirmación de la venta.
10. La validación existente vuelve a comprobar elegibilidad antes de registrar
    la operación.

El scanner es un atajo para seleccionar artículos; no registra la Venta por sí
mismo y no omite la revisión/confirmación existente.

## Qué significa “disponible para agregar”

Para este flujo deben aplicarse las mismas reglas actuales de Venta Directa:
una unidad física debe ser elegible según los contratos vigentes del módulo y no
puede estar ya agregada a la venta en curso.

En particular:

- sólo considerar unidades que el flujo actual admite como disponibles para
  Venta Directa;
- excluir unidades con reserva incompatible según la regla vigente;
- excluir los `UnidadInventario.Id` ya agregados al formulario;
- no contar dos veces una misma unidad;
- al volver a escanear el mismo producto, mostrar únicamente las unidades que
  todavía pueden agregarse.

Ejemplo: si existen 7 unidades elegibles y 2 ya están en la venta actual, el
scanner debe mostrar **5 disponibles para agregar**, no 7.

## Selección de las unidades físicas

La usuaria elige **cantidad**, no los IDs individuales, en este atajo. El
sistema selecciona N unidades concretas de las elegibles y el formulario/revisión
sigue mostrando esas unidades reales.

Esta mejora **no crea una política contable de selección de inventario**.
No inventar FIFO, LIFO, “menor costo”, “mayor costo”, “más antigua” ni otra
preferencia de negocio que no esté aprobada. La implementación debe reutilizar
la elegibilidad y un orden estable del servicio operativo existente, sin ordenar
por costo o introducir una regla nueva de valoración.

Las unidades concretas siguen visibles en el flujo y pueden quitarse antes de
confirmar; la búsqueda manual existente continúa disponible cuando se necesite
elegir una unidad específica.

## Estados y errores de Venta Directa

### Producto no registrado

Mostrar un mensaje equivalente a:

> No hay ningún producto registrado con este código de barras.

No iniciar lookup externo, no ofrecer crear Producto desde este flujo y no
modificar los artículos ya agregados.

### Producto registrado sin unidades elegibles

Mostrar el producto encontrado y **0 unidades disponibles para agregar**, con un
mensaje claro. No abrir un selector de cantidad válido y no agregar nada.

### Una o más unidades disponibles

Mostrar la disponibilidad actual y permitir una cantidad entera entre `1` y
el máximo disponible. No aceptar cero, negativos, decimales ni una cantidad
superior al máximo.

### Cambio de disponibilidad

La cantidad mostrada al escanear es informativa del momento de la consulta, no
una reserva. Agregar al formulario tampoco debe debilitar las comprobaciones
actuales.

Antes de registrar la Venta Directa se conserva la revalidación vigente. Si una
unidad dejó de ser elegible, la operación debe detenerse con un mensaje claro;
no sustituir silenciosamente una unidad, no reducir silenciosamente la cantidad
y no registrar una venta parcial.

## Precio

Cada unidad agregada por scanner usa el mismo comportamiento de precio que una
unidad agregada por el buscador actual: cargar el precio sugerido del producto
como valor inicial y permitir editar el precio final antes de confirmar.

No introducir precios por cantidad, promociones ni descuentos automáticos.

---

# Inventario

## Experiencia aprobada

En Inventario el scanner sirve para **abrir rápidamente el producto**, no para
filtrar una lista de unidades ni cambiar estados.

Flujo:

1. La usuaria abre **Inventario**.
2. Selecciona **Escanear código**.
3. Confirma un código desde el `BarcodeScanner`.
4. ResellManager busca una coincidencia local exacta por
   `Producto.CodigoBarras`.
5. Si existe, navega directamente al detalle administrativo actual del producto:
   `/productos/{id}`.
6. Si no existe, permanece en Inventario, muestra un mensaje claro y conserva la
   búsqueda manual como alternativa.

La navegación depende de que exista el Producto, no de que tenga unidades
disponibles en ese momento. El scanner de Inventario no cambia estados, reservas,
recepciones ni unidades.

## Filtros existentes

El scanner es una acción independiente del formulario de filtros de Inventario.
No debe interpretar el código como filtro de estado ni requerir limpiar los
filtros antes de usarlo. Si no encuentra producto, no debe destruir
innecesariamente el término/filtro que la usuaria ya tenía.

---

# Contratos técnicos a reutilizar

La implementación debe partir de los contratos existentes, verificando sus
firmas actuales antes de modificar:

- `BarcodeScanner.razor`: captura y confirmación del código.
- `IProductoService.ObtenerPorCodigoBarrasAsync(...)`: coincidencia local de
  Producto por código de barras.
- `ISeleccionOperativaService` / `SeleccionOperativaService`: reglas y
  consultas de unidades elegibles para flujos operativos.
- `UnidadBuscador` y el flujo actual de `VentaDirectaForm`: alternativa manual,
  exclusión de unidades ya seleccionadas y agregado de unidades concretas.
- `Inventario.razor`: búsqueda/filtros actuales, que deben seguir funcionando.

Si para Venta Directa hace falta una consulta por `ProductoId` que devuelva o
cuente unidades elegibles excluyendo IDs ya seleccionados, ampliar el contrato
operativo correspondiente en lugar de cargar todo el inventario y filtrar en
Razor.

No usar `IProductoLookupService` para estos flujos: ese servicio pertenece a la
búsqueda asistida al **registrar Producto**, que tiene otro propósito.

## Alcance de UI

La integración debe adaptarse al diseño existente de ResellManager:

- controles táctiles y sin desbordamiento horizontal;
- cantidad usable en móvil;
- estados ocupado/error accesibles;
- no abrir múltiples diálogos o consultas por doble toque;
- bloquear acciones incompatibles mientras el formulario esté registrando,
  revisando o procesando otra selección.

El comportamiento visual del scanner existente no se rediseña en esta tarea.
La imagen principal del producto en Venta Directa/Inventario tampoco forma parte
obligatoria de esta feature; está planificada por separado donde aporte valor.

---

# Criterios de aceptación

## Venta Directa

- [ ] Existe una acción visible para abrir el scanner sin eliminar
  `UnidadBuscador`.
- [ ] Cancelar el scanner deja intacta la venta en curso.
- [ ] Un código inexistente muestra error local y no realiza lookup externo.
- [ ] Un producto existente muestra la cantidad de unidades elegibles restantes.
- [ ] Las unidades ya agregadas se descuentan de esa disponibilidad.
- [ ] La cantidad inicia en 1 y no puede superar la disponibilidad.
- [ ] Confirmar N agrega exactamente N unidades físicas distintas.
- [ ] Escanear nuevamente el mismo producto permite agregar sólo las restantes.
- [ ] Cero disponibilidad no agrega artículos.
- [ ] Las unidades agregadas conservan precio sugerido inicial y precio final
  editable como el flujo manual.
- [ ] La revisión final muestra las unidades concretas y el total normal.
- [ ] La revalidación previa al registro sigue siendo obligatoria.
- [ ] Un cambio concurrente de disponibilidad detiene la venta; no sustituye ni
  reduce artículos silenciosamente.
- [ ] No se introduce FIFO/LIFO ni orden por costo como regla de negocio.

## Inventario

- [ ] Existe una acción visible para abrir el scanner sin eliminar búsqueda ni
  filtros actuales.
- [ ] Un código de producto existente navega a `/productos/{id}`.
- [ ] La navegación funciona aunque el producto tenga cero unidades disponibles.
- [ ] Un código inexistente muestra error y permanece en Inventario.
- [ ] El fallo/cancelación no borra innecesariamente filtros o búsqueda.
- [ ] Escanear no cambia estados, reservas ni recepción.
- [ ] No se consulta ningún proveedor externo.

## Validación técnica y regresión

- [ ] Pruebas cubren coincidencia, inexistente, cero/una/múltiples unidades,
  exclusión de seleccionadas, límites de cantidad y escaneo repetido.
- [ ] Pruebas cubren navegación de Inventario y preservación ante fallo/cancelación.
- [ ] Pruebas demuestran que el camino operativo no invoca lookup externo.
- [ ] Las pruebas existentes de Venta Directa, inventario y scanner continúan
  pasando.
- [ ] Ejecutar build y suite .NET; ejecutar pruebas JS/scanner cuando se toque
  integración o markup relacionado.
- [ ] Revisar UI al menos en los anchos móviles ya usados por el proyecto
  (320/390 px) y escritorio, sin overflow horizontal.
- [ ] Después de implementar, validar físicamente el flujo operativo con cámara
  en un dispositivo real; registrar dispositivo/navegador exactos sin extrapolar
  esa evidencia a navegadores no probados.

---

# Fuera de alcance

Esta feature no autoriza:

- registrar o editar Producto desde Venta Directa/Inventario;
- búsqueda asistida Open Facts/UPCitemdb;
- generar códigos de barras;
- modificar lectores/algoritmo óptico del scanner salvo que aparezca una
  regresión demostrable durante la integración;
- crear una política FIFO/LIFO/costeo;
- descuentos, promociones o precios por cantidad;
- cambios a reservas, estados físicos o reglas de venta;
- múltiples imágenes u object storage;
- carrito, pedidos web, ecommerce o WhatsApp;
- cambios de infraestructura/deployment no requeridos por la integración.

## Documentación al cerrar la implementación

Conforme a `AGENTS.md`, la rama que implemente esta feature debe actualizar la
documentación en el mismo cambio. Como mínimo:

- cambiar este documento de “implementación pendiente” al estado realmente
  validado;
- actualizar [V2 pendientes](../19_V2_Pendientes.md) para no dejar Venta Directa
  e Inventario descritos como pendientes si ambos quedaron terminados;
- actualizar [Ventas](ventas.md), [Inventario](inventario.md) y
  [Scanner](../29_BarcodeScanner.md) con consumidores/pruebas reales;
- actualizar [Backlog](../09_Backlog.md), [ROADMAP](../../ROADMAP.md) y
  [CHANGELOG](../../CHANGELOG.md) cuando corresponda al cierre/versionado;
- conservar como histórica la evidencia previa del scanner y añadir la nueva
  validación operativa, sin reescribir qué dispositivo/navegador probó cada fase.

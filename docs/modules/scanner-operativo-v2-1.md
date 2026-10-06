# V2.1 — Scanner operativo en Venta Directa, Inventario y Nueva compra

**Estado: implementación inicial y ajustes aprobados tras Preview implementados y validados automáticamente (06/10/2026); QA físico final pendiente.**

Este documento conserva el contrato aprobado y describe su implementación en
**Venta Directa**, **Inventario** y **Nueva compra**, reutilizando el scanner de códigos de barras
existente. La evidencia automatizada y los límites del cierre se registran más
abajo. No implica merge, release ni despliegue de esta rama.

El motor, formatos soportados, privacidad, modos de captura y contrato actual de
`BarcodeScanner` siguen definidos en [Scanner de códigos de producto](../29_BarcodeScanner.md).
No deben duplicarse ni reemplazarse para esta tarea.

## Objetivo

Reducir el tiempo necesario para localizar mercancía durante una venta presencial
o una consulta de inventario y seleccionar artículos al preparar una compra, usando `Producto.CodigoBarras` como acceso rápido
a datos que **ya existen localmente en ResellManager**.

El escaneo operativo selecciona datos locales y conserva la búsqueda manual.
Sólo Nueva compra permite abrir explícitamente el alta asistida de Producto
cuando un código no existe; ese subflujo reutiliza el lookup y el guardado vigentes.

## Principios comunes

- La cámara se activa únicamente por acción explícita de la usuaria.
- Reutilizar el componente `BarcodeScanner` vigente y su `OnDetected`; no crear
  otro decoder, otro flujo JS ni otra dependencia de lectura.
- El valor confirmado por el scanner se busca como `Producto.CodigoBarras`.
- La coincidencia operativa es local. **No consultar Open Facts, UPCitemdb ni
  ningún otro proveedor externo** desde el escaneo de Venta Directa, Inventario
  o Nueva compra. La excepción pertenece sólo al alta explícita de Producto en Compra.
- Si el código no corresponde a un producto registrado, informar claramente y
  permitir continuar con la búsqueda manual.
- Cancelar/cerrar el scanner no modifica la venta, la compra, el inventario ni los filtros.
- La búsqueda manual actual permanece disponible en los tres flujos.
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

## Ajuste aprobado tras probar Preview — agrupación por lote de agregado

La primera implementación agregó correctamente N unidades físicas, pero presentó
cada unidad como un detalle visual independiente con su propio campo de precio.
Tras probar el flujo se aprobó cambiar **la representación y edición del
formulario**, sin cambiar la identidad física ni el modelo histórico de la venta.

### Regla de agrupación

Las unidades agregadas en **una misma acción** forman un lote visual de Venta
Directa:

- un escaneo que agrega 5 unidades crea un lote visual con cantidad `×5`;
- el lote muestra el nombre del Producto una sola vez;
- debajo se muestran los códigos de las 5 `UnidadInventario` concretas;
- el lote tiene **un solo Precio final por unidad**, editable;
- ese precio se aplica a todas las unidades concretas del lote;
- el subtotal del lote es `cantidad × PrecioFinal`;
- la revisión previa a confirmar debe conservar la misma agrupación visual.

Ejemplo conceptual:

```text
Producto X                                      ×5
UNI-...-001
UNI-...-002
UNI-...-003
UNI-...-004
UNI-...-005

Precio final por unidad
Q 75.00

Subtotal
Q 375.00
```

La agrupación se determina por **acción de agregado**, no solamente por
`ProductoId` ni por igualdad de precio. Si el mismo Producto se agrega después
en otra acción, debe formar otro lote independiente aunque coincidan Producto y
precio. Esto permite, por ejemplo, vender 5 unidades juntas a Q75 y agregar luego
1 unidad del mismo Producto a Q65 sin que la UI las fusione.

La búsqueda manual de una unidad individual crea naturalmente un lote de una
unidad. Si se quita una unidad concreta de un lote, la cantidad visible se
actualiza; el lote desaparece al quedar vacío.

### Persistencia y reglas que no cambian

Esta agrupación es de **formulario/revisión**, no una fusión de las unidades
físicas ni una nueva entidad de dominio obligatoria:

- cada `UnidadInventario` conserva su identidad y código;
- el registro final continúa creando los detalles físicos requeridos por el
  contrato vigente, uno por unidad cuando corresponda;
- todas las unidades de un lote reciben el mismo `PrecioFinal` al construir la
  entrada de la Venta;
- no agrupar detalles históricos de forma que se pierda la identidad de unidad;
- la revalidación de elegibilidad continúa por IDs concretos;
- no introducir descuentos por cantidad, promociones ni una política de precios.

La implementación puede introducir un modelo de presentación/grupo en Web para
representar el lote, pero no requiere migración ni cambio de esquema.

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

# Compras — integración aprobada tras probar Preview

El scanner operativo ya se integra en **Nueva compra**. Este consumidor se aprobó
después de probar la primera implementación de Venta Directa e Inventario y se
implementó en la continuación de la misma rama, reutilizando el alta contextual.

Compras ya modela cada detalle como **Producto + Cantidad + Costo unitario** y
`CompraService` genera las unidades físicas según la cantidad. El scanner no
debe duplicar esa lógica ni crear una fila visual por cada unidad comprada.

## Producto ya registrado

Flujo aprobado:

1. La usuaria abre **Nueva compra** y activa **Escanear código** para el detalle
   de compra en el que está trabajando.
2. Confirma el código mediante el `BarcodeScanner` existente.
3. ResellManager realiza primero una coincidencia local exacta por
   `Producto.CodigoBarras`.
4. Si existe, selecciona ese Producto en el detalle usando el mismo comportamiento
   funcional que `ProductoBuscador`.
5. En una línea nueva la cantidad inicial continúa siendo `1`; la usuaria puede
   editar Cantidad y Costo unitario normalmente.
6. La revisión de Compra muestra Producto, Cantidad, Costo unitario y subtotal
   como hasta ahora.
7. Al confirmar, `CompraService` conserva la autoridad para crear
   `DetalleCompra` y las `UnidadInventario` físicas correspondientes.

El scanner es solamente otro mecanismo para seleccionar Producto. No cambia
moneda, proveedor, origen, costo, estado inicial, comprobante ni reglas de
recepción. Si se usa sobre un detalle que ya contiene datos, seleccionar por
scanner debe respetar las mismas reglas de conservación/reemplazo que seleccionar
otro Producto mediante el buscador manual; no crear semántica paralela.

## Código no registrado

Si la coincidencia local no encuentra Producto, **Nueva compra sí puede ofrecer
“Registrar producto”** porque este es un punto natural de entrada de mercancía
nueva.

Ese camino debe reutilizar el alta de Producto y la búsqueda asistida ya
existentes:

1. conservar el código escaneado como `CodigoBarras` del formulario de alta;
2. ejecutar la comprobación local obligatoria del flujo de alta;
3. permitir la ronda externa Open Facts → UPCitemdb únicamente dentro de ese
   flujo de registro de Producto;
4. presentar el candidato y exigir la misma aceptación explícita existente antes
   de copiar datos;
5. permitir edición manual y Guardar producto;
6. al crear correctamente el Producto, volver a la Compra conservando sus datos
   y autoseleccionar el Producto nuevo en el detalle original.

Cancelar/cerrar el alta debe regresar a la Compra sin registrar Producto y sin
perder proveedor, moneda, origen, detalles, cantidades, costos, observaciones ni
comprobante que ya estuvieran en preparación.

La excepción de lookup externo aplica **sólo al subflujo explícito Registrar
producto**. Escanear en Compra no debe consultar proveedores externos de forma
automática si existe un Producto local ni debe convertir datos externos
directamente en un detalle de Compra.

## Criterios de aceptación de Compras

- [x] Existe acción visible de scanner en Nueva compra sin eliminar
  `ProductoBuscador`.
- [x] Un código local existente selecciona el Producto en el detalle objetivo.
- [x] Cantidad y costo continúan siendo campos del detalle, no uno por unidad.
- [x] La revisión conserva el modelo Producto × Cantidad × Costo unitario.
- [x] El scanner no crea `UnidadInventario` directamente; la autoridad sigue en
  `CompraService`.
- [x] Un código inexistente ofrece Registrar producto sin modificar la Compra.
- [x] Registrar producto reutiliza el flujo asistido vigente y conserva el código
  escaneado.
- [x] El lookup externo sólo ocurre dentro del alta explícita de Producto.
- [x] Guardar el Producto nuevo vuelve a la Compra y lo autoselecciona en el
  detalle original.
- [x] Cancelar el alta conserva intacta la Compra en preparación.
- [x] Fallo/cancelación del scanner conserva la Compra y la búsqueda manual.
- [x] No se cambian reglas de moneda, costos, origen, comprobantes, recepción ni
  generación de unidades.
- [x] Pruebas cubren encontrado/no encontrado, alta asistida/cancelación,
  autoselección y preservación del formulario de Compra.

---

# Contratos técnicos a reutilizar

La implementación debe partir de los contratos existentes, verificando sus
firmas actuales antes de modificar:

- `BarcodeScanner.razor`: captura y confirmación del código.
- `IConsultaProductoCodigoBarras.ObtenerPorCodigoBarrasAsync(...)`: contrato local
  vigente, implementado por `ProductoService`, para coincidencia exacta por código.
  La especificación inicial lo atribuía a `IProductoService`; se verificó y
  reutilizó la interfaz real sin ampliar ese contrato ni activar lookup externo.
- `ISeleccionOperativaService` / `SeleccionOperativaService`: reglas y
  consultas de unidades elegibles para flujos operativos.
- `UnidadBuscador` y el flujo actual de `VentaDirectaForm`: alternativa manual,
  exclusión de unidades ya seleccionadas y agregado de unidades concretas.
- `Inventario.razor`: búsqueda/filtros actuales, que deben seguir funcionando.
- `CompraNueva.razor`, `ProductoAltaPanel` y `ProductoForm`: detalle objetivo y
  alta contextual asistida con revisión/importación reversible; guardado mediante
  `IAltaProductoAsistidaService`.

Si para Venta Directa hace falta una consulta por `ProductoId` que devuelva o
cuente unidades elegibles excluyendo IDs ya seleccionados, ampliar el contrato
operativo correspondiente en lugar de cargar todo el inventario y filtrar en
Razor.

No usar `IProductoLookupService` en el escaneo operativo de Venta Directa,
Inventario o Compra. Sólo el subflujo explícito **Registrar producto** de Compra
reutiliza esa búsqueda asistida y `IAltaProductoAsistidaService` para el guardado.

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

- [x] Existe una acción visible para abrir el scanner sin eliminar
  `UnidadBuscador`.
- [x] Cancelar el scanner deja intacta la venta en curso.
- [x] Un código inexistente muestra error local y no realiza lookup externo.
- [x] Un producto existente muestra la cantidad de unidades elegibles restantes.
- [x] Las unidades ya agregadas se descuentan de esa disponibilidad.
- [x] La cantidad inicia en 1 y no puede superar la disponibilidad.
- [x] Confirmar N agrega exactamente N unidades físicas distintas.
- [x] Escanear nuevamente el mismo producto permite agregar sólo las restantes.
- [x] Cero disponibilidad no agrega artículos.
- [x] Las unidades agregadas conservan precio sugerido inicial y precio final
  editable como el flujo manual.
- [x] La revisión final muestra las unidades concretas y el total normal.
- [x] La revalidación previa al registro sigue siendo obligatoria.
- [x] Un cambio concurrente de disponibilidad detiene la venta; no sustituye ni
  reduce artículos silenciosamente.
- [x] No se introduce FIFO/LIFO ni orden por costo como regla de negocio.

## Inventario

- [x] Existe una acción visible para abrir el scanner sin eliminar búsqueda ni
  filtros actuales.
- [x] Un código de producto existente navega a `/productos/{id}`.
- [x] La navegación funciona aunque el producto tenga cero unidades disponibles.
- [x] Un código inexistente muestra error y permanece en Inventario.
- [x] El fallo/cancelación no borra innecesariamente filtros o búsqueda.
- [x] Escanear no cambia estados, reservas ni recepción.
- [x] No se consulta ningún proveedor externo.

## Ajustes posteriores a la primera implementación

- [x] Venta Directa agrupa visualmente por **lote de agregado**, no por Producto
  global ni por precio.
- [x] Un lote de N unidades muestra Producto una vez, `×N`, los N códigos
  físicos, un solo Precio final por unidad y su subtotal.
- [x] Cambiar el Precio final del lote aplica el mismo valor a sus N unidades
  concretas.
- [x] Agregar el mismo Producto en otra acción crea otro lote independiente y
  permite otro Precio final.
- [x] La revisión previa a confirmar conserva los mismos lotes visuales.
- [x] Quitar una unidad concreta actualiza la cantidad del lote sin perder la
  identidad de las restantes.
- [x] La persistencia y revalidación siguen operando sobre unidades físicas
  concretas; no hay migración ni fusión histórica.
- [x] Implementar y validar los criterios de Compras definidos en la sección
  correspondiente.

## Validación técnica y regresión

- [x] Pruebas cubren coincidencia, inexistente, cero/una/múltiples unidades,
  exclusión de seleccionadas, límites de cantidad y escaneo repetido.
- [x] Pruebas cubren navegación de Inventario y preservación ante fallo/cancelación.
- [x] Pruebas demuestran que el camino operativo no invoca lookup externo.
- [x] Las pruebas existentes de Venta Directa, inventario y scanner continúan
  pasando.
- [x] Ejecutar build y suite .NET; ejecutar pruebas JS/scanner cuando se toque
  integración o markup relacionado.
- [x] Revisar UI al menos en los anchos móviles ya usados por el proyecto
  (320/390 px) y escritorio, sin overflow horizontal.
- [ ] Después de implementar, validar físicamente el flujo operativo con cámara
  en un dispositivo real; registrar dispositivo/navegador exactos sin extrapolar
  esa evidencia a navegadores no probados.

---

## Implementación y evidencia — 06/10/2026

**Registro histórico de la implementación inicial**, anterior a los ajustes de
lotes y Compra. Conserva los resultados de esa ejecución; el estado actual y
sus validaciones se registran en la sección posterior.

- [VentaDirectaForm](../../src/ResellManager.Web/Components/Ventas/VentaDirectaForm.razor)
  y [Inventario](../../src/ResellManager.Web/Components/Pages/Inventario.razor)
  consumen el mismo `BarcodeScanner.OnDetected` y consultan exclusivamente
  `IConsultaProductoCodigoBarras`. Se mantienen los strings exactos, la entrada
  manual y el contrato óptico existente.
- `ISeleccionOperativaService.ListarUnidadesDirectasAsync(productoId, excluir, ct)`
  devuelve todas las unidades elegibles del producto, sin el límite de 12 del
  buscador manual. Reutiliza `Elegibles(productoId, null)`: estado Disponible,
  sin reserva y excluyendo los IDs del formulario. Ordena por `CodigoInterno`
  como el servicio operativo existente; `Id` desempata. No utiliza costo ni fecha.
- Venta Directa muestra una selección de cantidad en la página, inicialmente 1,
  con límites enteros verificados en el manejador del servidor. Ambos caminos
  agregan los mismos modelos de unidad con precio sugerido editable. Mientras
  hay una selección pendiente se bloquean selección manual, revisión y cambios
  incompatibles. Cancelarla conserva los artículos existentes.
- Antes de agregar N se comprueban los N IDs elegidos. Si alguno perdió
  elegibilidad se conserva la selección para cancelarla/repetir el escaneo,
  sin agregar parte de ella ni elegir reemplazos. La revalidación vigente antes
  de crear el pedido y las comprobaciones de VentaService permanecen intactas.
- Los errores del scanner de Inventario son independientes del listado y de sus
  filtros. Un producto sin inventario también abre su detalle. La consulta no
  modifica unidades, estados, reservas ni recepciones.

Validaciones ejecutadas:

| Comprobación | Resultado |
| --- | --- |
| `dotnet build ResellManager.sln` | 0 errores y 0 advertencias. |
| `dotnet test ResellManager.sln --filter FullyQualifiedName~ScannerOperativo` | 34 casos correctos, sin omisiones. |
| `dotnet test ResellManager.sln --no-build` | 862 pruebas correctas, sin omisiones; incluye Venta Directa, inventario y reservas existentes. |
| `npm ci` y `npm run css:build` | Correctos; el CSS y el vendor generados conservan su contenido versionado. |
| `npm run test:js` | 115 pruebas correctas, incluidas las 98 del scanner. |
| `npm run qa:scanner` | 80 comprobaciones y 28 decodificaciones correctas; cámara de canvas, sin hardware. |
| `npm run qa:scanner-operativo` | 40 vistas correctas: 10 estados a 320/390/768/1440 px, sin overflow horizontal; controles visibles de al menos 44 × 44 px y límites nativos de cantidad correctos. |
| `git diff --check` | Sin errores. |

[ScannerOperativoTests](../../tests/ResellManager.Tests/ScannerOperativoTests.cs)
usa SQLite sintético, los servicios reales y el callback de BarcodeScanner:
coincidencia exacta, inexistente, cero/una/múltiples unidades, reservas/estados,
exclusiones, cantidad inválida, escaneo repetido, precios/revisión/registro,
cancelación, errores técnicos, doble toque y pérdida de elegibilidad tanto antes
de agregar como antes de registrar. Un servicio externo instrumentado verifica
cero invocaciones en ambos caminos operativos.

El [QA operativo reproducible](../../tests/scanner-operativo.browser.mjs) exporta
markup de los componentes reales y usa sus CSS en **Edge 154.0.4258.62 headless
sobre Windows**. Evidencia local ignorada por Git:
`.artifacts/scanner-operativo-ui/report.json` y capturas PNG de los 40 estados.
Para ejecutarlo se reutiliza Playwright disponible, con
`SCANNER_PLAYWRIGHT_MODULE` y `SCANNER_BROWSER_CHANNEL` según la
[guía del scanner](../29_BarcodeScanner.md#validación-y-límites), sin agregarlo al
runtime ni al lockfile. El QA visual no levanta un circuito Blazor interactivo;
los callbacks, selección y navegación se ejercitan en .NET. Se inspeccionaron
además las capturas de cantidad a 320 px, error/filtros a 390 px y escritorio.

La prueba física anterior de **iPhone 14 Plus + Brave** corresponde al flujo de
Producto y se conserva en la guía del scanner. No valida los consumidores
operativos nuevos ni certifica Safari. El QA físico de la implementación final
se detalla a continuación.

---

## Ajustes implementados y validados — 06/10/2026

Se continuó desde `f7d4879` en `feature/scanner-operativo-v2-1`, conservando la
implementación inicial y su base `develop` (`de55b57`). Este cierre no implica
merge, release ni despliegue.

- **Venta Directa:** cada acción asigna un `LoteId` de presentación a sus unidades.
  `LoteVentaDirectaFormModel` agrupa sólo por esa identidad; dos acciones del mismo
  Producto y precio permanecen independientes. Formulario y revisión muestran
  Producto ×N, todos los códigos físicos, un precio por unidad y subtotal. Editar
  ese precio actualiza los modelos de las N unidades; quitar una conserva el lote
  y las identidades restantes, y el lote desaparece al quedar vacío. Los inputs
  persistidos y la revalidación continúan por unidad física, sin nuevo esquema.
- **Nueva compra:** cada detalle tiene `BarcodeScanner` junto a `ProductoBuscador`.
  El callback consulta `IConsultaProductoCodigoBarras` y utiliza la misma aplicación
  de Producto que el buscador, conservando cantidad, costo y el resto del modelo.
  Las consultas no registran Compra ni generan inventario.
- Un código inexistente muestra **Registrar producto** sin alterar el detalle.
  Sólo al elegir esa acción se abre `ProductoAltaPanel` con el código exacto y
  lookup habilitado. `ProductoForm.BuscarAlIniciar` inicia una sola ronda por modelo,
  reutilizando la comprobación local y el fallback Open Facts → UPCitemdb.
  El candidato requiere aceptación explícita; se conservan edición manual,
  deshacer, cancelación y guardado. El alta manual previa de Compra conserva su
  comportamiento.
- El panel utiliza `IAltaProductoAsistidaService` para guardar el alta asistida,
  incluida la imagen externa opcional y la prioridad de imagen manual. Un fallo
  recuperable de imagen se comunica al volver a la Compra. Si el producto apareció
  localmente antes de abrir el alta, no se consulta un proveedor ni se crea un
  duplicado; su consulta se abre en otra pestaña para conservar la Compra.
- Guardar autoselecciona el producto en el detalle original. Cerrar/cancelar el alta
  conserva el modelo, moneda/tipo aplicado, origen, proveedor, detalles, cantidades,
  costos, observaciones y el mismo `IBrowserFile` del comprobante. Cancelar durante
  lookup cancela la ronda y descarta respuestas tardías. Los botones y manejadores
  bloquean selecciones incompatibles, doble consulta y revisión durante el proceso.

| Validación final | Resultado |
| --- | --- |
| `dotnet build ResellManager.sln` | 0 errores y 0 advertencias. |
| Pruebas del área: ScannerOperativo + CompraProductoFlujo + CompraRevisionProveedor | 65 correctas, sin omisiones; incluyen 50 del scanner operativo. |
| `dotnet test ResellManager.sln --no-build` | 878 correctas, sin omisiones. |
| `npm ci` y `npm run css:build` | Correctos; CSS regenerado y conservado, vendor óptico sin cambios. |
| `npm run test:js` | 115 correctas. |
| `npm run qa:scanner` | 80 comprobaciones y 28 decodificaciones correctas con fixtures/canvas. |
| `npm run qa:scanner-operativo` | 84 vistas: 21 estados a 320/390/768/1440 px, sin overflow, errores JS ni controles táctiles menores de 44 × 44 px. |
| Enlaces/rutas nuevas y `git diff --check` | Correctos. |

Las pruebas del área se ejecutaron con el filtro combinado
`FullyQualifiedName~ScannerOperativo|FullyQualifiedName~CompraProductoFlujo|FullyQualifiedName~CompraRevisionProveedor`.
[Pruebas de lotes](../../tests/ResellManager.Tests/ScannerOperativoLotesTests.cs) y
[pruebas de Compra](../../tests/ResellManager.Tests/ScannerOperativoComprasTests.cs)
amplían `ScannerOperativoTests`: precios iguales/distintos entre acciones,
eliminación parcial/vacía, revisión/persistencia por IDs, códigos exactos,
found/not-found, alta asistida/fallback y aceptación, autoselección, imagen opcional
fallida, cancelación normal y durante lookup, comprobación local concurrente,
comprobante intacto y generación de unidades sólo al confirmar la Compra.
Todos los proveedores externos y datos son sintéticos en estas pruebas.

El QA visual usa markup/CSS reales en **Edge 154.0.4258.62 headless sobre Windows**;
no levanta un circuito Blazor interactivo ni usa cámara física. Comprueba también
que las acciones iguales conservan dos lotes y que la Compra mantiene cantidad,
costo, tipo aplicado y código durante el alta. Se inspeccionaron capturas de
lotes a 320 px, alta/candidato a 390 px y Compra en escritorio. La evidencia local
actual está en `.artifacts/scanner-operativo-ui/report.json` y sus PNG; el comando,
Playwright disponible y las variables de configuración siguen la guía del scanner.

**Pendiente: QA físico final** con cámara en dispositivo real de Venta Directa,
Inventario y Nueva compra. Registrar modelo, sistema y navegador/versiones exactos;
comprobar lotes/precios independientes, escaneo repetido, código inexistente,
cancelación, navegación de Inventario sin unidades y alta/autoselección en Compra
sin perder el comprobante. La prueba anterior de Producto no se extrapola a estos
flujos. No se dispuso de ese dispositivo en esta validación.

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
- actualizar [Ventas](ventas.md), [Inventario](inventario.md),
  [Compras](compras.md) y [Scanner](../29_BarcodeScanner.md) con consumidores/pruebas reales;
- actualizar [Backlog](../09_Backlog.md), [ROADMAP](../../ROADMAP.md) y
  [CHANGELOG](../../CHANGELOG.md) cuando corresponda al cierre/versionado;
- conservar como histórica la evidencia previa del scanner y añadir la nueva
  validación operativa, sin reescribir qué dispositivo/navegador probó cada fase.

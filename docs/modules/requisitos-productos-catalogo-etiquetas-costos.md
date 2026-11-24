# ResellManager y Virtuosa Store — Requisitos de productos, catálogo, precios, etiquetas y costos desconocidos

> **Estado:** Especificación funcional aprobada; implementación en rama de trabajo, sin despliegue. [Contratos y límites de aceptación](operaciones-financieras-y-productos.md), incluida la validación física pendiente de etiquetas/NIIMBOT.
> **Fecha inicial:** 2026-11-11
> **Actualización funcional:** 2026-11-22 (costo desconocido, migración por Codex, notas de corrección, versionado lógico de reportes, enlaces a productos agotados y roles futuros solo como idea, sin diseño)
> **Áreas:** Productos, Compras, Inventario, Ventas/Reportes, Scanner, Catálogo público de Virtuosa Store y Etiquetas.
> **Alcance:** comportamiento, criterios de aceptación y límites; los contratos técnicos se definirán tras inspeccionar el código.
> **Control de versiones:** documentación publicada en rama de trabajo; sin merge ni despliegue.

## 1. Objetivo

Permitir (a) vincular productos que son distintas versiones del mismo modelo —color, talla, aroma, capacidad o presentación— sin unificar sus registros ni existencias; (b) mostrar «Consultar precio» y abrir WhatsApp cuando no haya precio de venta comercial válido; (c) facilitar que el personal asigne precios desde un teléfono y que otra persona imprima etiquetas con precio y **código interno escaneable**, incluso cuando el artículo carezca de código de barras de fabricante; y (d) **registrar mercancía cuyo costo de adquisición se desconoce** sin inventar un costo de Q0.00 ni reportar ganancias ficticias.

El catálogo sigue siendo **de lectura**, sin carrito, checkout ni venta en línea. La fuente de verdad continúa siendo ResellManager. En administración, además, se facilitará localizar rápidamente los productos **pendientes de precio** mediante un filtro directo y un panel de filtros avanzados, y asignarles precio desde el teléfono. **Precio de venta desconocido** y **costo de adquisición desconocido** son necesidades distintas; ningún filtro o cálculo debe confundirlas.

## 2. Contexto actual que debe respetarse

Documentos revisados en `develop` de `newton0608/ResellManager`:

- [`docs/modules/productos.md`](productos.md): `Producto` describe el modelo comercial, `UnidadInventario` representa cada unidad física. Hay **código interno automático `PRO-…`**, código de barras externo opcional y gestión de fotografías; no suponer que el código interno ya es aceptado por el scanner.
- [`docs/architecture/domain-rules.md`](../architecture/domain-rules.md) (§ Producto, precios y utilidad): `Producto.PrecioSugerido` es una referencia comercial; **el costo no pertenece a Producto**, sino a `UnidadInventario.Costo` en inventario y al snapshot `DetalleVenta.CostoUnitario` en ventas, y la utilidad se calcula desde el precio final de venta menos ese costo. El contrato vigente requiere una revisión explícita para admitir costo desconocido.
- [`docs/modules/compras.md`](compras.md) y [`docs/modules/inventario.md`](inventario.md): las compras y unidades de inventario conservan datos de costo y origen; no asumir que ya aceptan importes nulos o desconocidos.
- [`docs/modules/scanner-operativo-v2-1.md`](scanner-operativo-v2-1.md): el scanner operativo de Venta Directa, Inventario y Nueva compra actualmente **busca el dato escaneado contra `Producto.CodigoBarras`**. Por tanto, imprimir un `PRO-…` en Code 128 **no bastaría**: debe adaptarse la resolución local del escaneo en los flujos que correspondan, con pruebas y control de colisiones.
- [`docs/modules/catalogo.md`](catalogo.md): catálogo público, búsqueda/filtros/paginación, disponibilidad desde backend y rutas públicas de detalle. No modificar stock ni consultar datos privados desde el público.
- [`docs/27_VirtuosaStore.md`](../27_VirtuosaStore.md): dominio público `virtuosagt.com`, página limpia `/producto/{id}` en producción con rutas internas `/catalogo/{id}`; contacto por WhatsApp **ya configurable en servidor**.
- [`AGENTS.md`](../../AGENTS.md): separación de capas y reglas Git, sin dependencias ni cambios ajenos a la tarea.
- Biblioteca de decisiones del negocio, `ResellManager - Ideas y decisiones pendientes - octubre 2026.md`, §8: **«Sin precio asignado»** y panel desplegable contraído con **marca, modelo, presentación en ml, peso y atributos existentes pertinentes**. Esta necesidad antecede a la presente especificación; no implica que ya esté implementada.

**Precaución:** confirmar versiones/contratos actuales al implementar. Esta especificación no afirma que las nuevas funciones ya existan.

## 3. Decisiones funcionales aprobadas

1. Cada variante continúa siendo un **Producto independiente**, con código, costo/valor transaccional, precio comercial, imágenes, inventario físico e historial propios.
2. Se vinculan por **grupos de variantes** (mismo modelo/base comercial) y pueden diferir por **uno o más atributos simultáneamente**: color + talla, aroma, capacidad, presentación, etc.
3. **Todas las variantes comercialmente disponibles aparecen individualmente** en portada, navegación, búsquedas, filtros y carruseles del catálogo, sin agrupar tarjetas. **Ningún producto sin existencias disponibles** (variante o producto independiente) aparece en resultados públicos; esto no afecta los listados administrativos.
4. En el **detalle de un producto disponible** se muestran **todas sus variantes vinculadas**, incluidas las agotadas: las disponibles son seleccionables y navegan a su detalle propio; las agotadas aparecen identificadas como **«Agotado»**, sin presentarse como comprables ni como resultados individuales del catálogo. Un grupo totalmente agotado no tiene tarjeta pública visible. **Un enlace directo existente a un producto agotado sí debe abrir su detalle**, con estado «Agotado» muy visible, sin fingir disponibilidad y mostrando, si las hay, otras variantes vinculadas disponibles.
5. Si no hay precio de venta definido o es **Q0.00**, el catálogo **no muestra «Q0.00» ni apariencia de artículo gratis**: muestra **«Consultar precio»** y facilita contacto vía el WhatsApp Business existente.
6. El enlace de WhatsApp para consultas incluirá **nombre del producto, variante si existe, código de identificación útil y URL pública**. Reutilizar configuración de contacto ya existente; no guardar número, token o credenciales en código o Markdown.
7. Personal autorizado podrá **poner o actualizar precios desde el celular**. La otra persona podrá imprimir etiquetas en lote posteriormente, desde una computadora; **no se necesita Bluetooth para el registro remoto del precio**.
8. Las etiquetas serán **por Producto, no por unidad física**: las copias de un producto llevan **el mismo código interno `PRO-…`**, nombre abreviado y precio, sin identificar cada unidad de inventario. Deben funcionar aunque no exista `CodigoBarras` externo; la cantidad de copias será configurable.
9. **NIIMBOT B1 es la opción preferida provisionalmente**, no una compra confirmada. Diseñar una **exportación a Excel (.xlsx) de datos para etiquetas por lote** y un PDF a tamaño real para impresión desde computadora; la importación a la aplicación NIIMBOT, plantillas, costes y compatibilidad reales deberán comprobarse con la versión elegida. No depender exclusivamente de esa marca.
10. En **Productos (administración)** debe haber un acceso rápido visible a **«Sin precio asignado»**; adicionalmente, un panel **«Filtros avanzados»** desplegable y **contraído por defecto** que reúna marca, modelo, presentación en ml, peso y atributos existentes aplicables. No crear campos arbitrarios solo para filtrar.
11. La edición rápida de precios desde teléfono se hará mediante **un diálogo/modal ligero** que muestre una **fotografía principal grande** y datos del producto **solo de lectura**; el **precio de venta será el único campo editable**. Tras guardar correctamente, se cierra el diálogo y **se actualizan los resultados del filtro**: el producto recién valorado deja de aparecer bajo «Sin precio asignado», sin recargar la aplicación completa ni perder los filtros activos.
12. **Costo desconocido**, sin modalidad de costo estimado: se podrán registrar nuevas unidades y operar el inventario sin conocer el costo. Los artículos ya cargados en el sistema tienen actualmente costo **Q0.00 por desconocimiento**, según la persona responsable: deben tratarse como **«Costo no registrado»**, mediante migración segura y verificable de esos registros existentes, sin fingir que costaron cero y sin afectar datos ajenos.
13. **No se permitirán costos estimados** en esta funcionalidad: solo **conocido** o **desconocido**. Se podrá registrar el costo real posteriormente mediante **nota de corrección/auditoría**, incluso si la unidad ya fue vendida. El original, la fecha de conocimiento, autor, motivo y valor anterior/nuevo permanecerán consultables; si cambia la utilidad histórica, deberá ser una **corrección explícita, identificable y trazable**, jamás un recálculo silencioso.
14. Distinguir en Dashboard y reportes la **utilidad calculable de unidades con costo conocido** de operaciones con **costo pendiente**; una utilidad parcial nunca deberá etiquetarse como «ganancia total» del negocio. Las correcciones posteriores de costo se reflejarán con indicación de **valor originalmente registrado y corrección auditada** en consultas/historial financiero pertinentes.
15. **Roles futuros, solo idea documentada:** contemplar a futuro perfiles como **Administrador, Supervisor y Vendedor/personal operativo**, con permisos configurables. Se considera aprovechar el ecosistema **ASP.NET / ASP.NET Core Identity** cuando llegue esa fase, pero **no diseñar ahora tablas, relaciones, permisos, migraciones ni pantallas de roles**, y no comprometer un esquema de usuarios concreto. Hasta entonces, las nuevas operaciones sensibles requieren autorización segura existente en backend, sin otorgar acceso implícito a todos los usuarios.
16. **Informes con versionado lógico:** conservar ventas, costos originales y correcciones como hechos auditables; el reporte permitirá reconocer el **estado original** y la **versión revisada** según las correcciones aplicadas hasta una fecha de corte. Cada revisión mostrará quién corrigió, cuándo, qué importe cambió y el motivo. No duplicar automáticamente todos los reportes en BD ni alterar una exportación histórica ya emitida como si siempre hubiese tenido el dato corregido; el mecanismo exacto se definirá en diseño técnico.

## 4. A — Grupos de variantes (administración)

**VAR-01.** Agregar la acción **«Gestionar variantes»** en administración de producto para crear o seleccionar un grupo comercial, vincular productos existentes y retirar vínculos sin borrar ninguno.

**VAR-02.** Permitir atributos flexibles con nombres y valores legibles (por ejemplo: `Color=Negro`, `Talla=M`, `Aroma=Lavanda`). Un producto podrá tener **múltiples atributos** dentro del mismo grupo.

**VAR-03.** Cada producto pertenecerá **como máximo a un grupo de variantes** a la vez. Evitar vínculos duplicados o pertenencias simultáneas; si se desea mover a otro grupo, requerir acción explícita.

**VAR-04.** Impedir la creación inadvertida de selecciones indistinguibles con exactamente los mismos valores de atributos en el grupo; si existen productos realmente distintos con iguales atributos, exigir una característica diferenciadora o resolución explícita.

**VAR-05.** No fusionar registros de Producto, `UnidadInventario`, códigos, fotos, costos, precios, compras, pedidos ni ventas. **Vincular/desvincular solo afecta la relación de variantes**.

**VAR-06.** La creación de variantes deberá soportar productos ya existentes; no será necesario recrearlos ni tocar sus códigos, imágenes o referencias históricas.

**VAR-07.** El selector público de una **variante disponible** muestra sus variantes vinculadas, incluso si algunas están agotadas, sin presentar las agotadas como comprables. No mostrar un selector si el grupo solo contiene un producto. La elegibilidad comercial la define el backend.

**VAR-08.** El formulario administrativo mostrará resumen de variantes vinculadas, atributos, identificación y estado, con acciones de revisión y confirmación. Debe funcionar en pantalla móvil.

**VAR-09.** Los atributos del grupo no sustituyen campos maestros existentes. Reutilizar los campos/contratos disponibles, evitar duplicar `Color` o `Presentación` si ya están modelados; el diseño técnico determinará normalización y persistencia.

## 5. B — Variantes en el catálogo público

**CAT-01.** El catálogo devolverá **solo productos comercialmente disponibles**, **cada uno por separado**, en portada, navegación, búsquedas, filtros de marca/categoría y carruseles, con ID, imagen y precio propios. Esto aplica tanto a variantes como a productos independientes. Las variantes agotadas **no generan tarjetas públicas**; la agrupación no altera la paginación ni sus cursores.

**CAT-02.** En el detalle incluir un selector de variantes vinculadas, semejante al ejemplo de productos por color proporcionado por el negocio. Etiquetas posibles: color, talla, aroma, capacidad o combinación de atributos.

**CAT-03.** El selector indicará visualmente la variante activa. **Desde el selector, solo las variantes disponibles serán navegables** hacia su propia ruta pública canónica; el detalle de cada una mostrará imágenes, título, precio, disponibilidad y características propios, nunca una mezcla del grupo. Las opciones agotadas se mostrarán identificadas como **«Agotado»** y no ofrecerán selección engañosa. Esto **no impide abrir directamente una URL existente de un producto agotado**.

**CAT-04.** Si un **producto disponible** tiene variantes agotadas vinculadas, esas variantes **solo se mostrarán como opciones «Agotado» dentro del detalle del disponible**, sin aparecer en portada, búsqueda o listados públicos ni permitir venta/reserva. Si ninguna variante está disponible, ninguna tendrá tarjeta pública. Las fichas agotadas existentes siguen accesibles **por su URL directa**, no por descubrimiento en listados. Conservar los registros y enlaces administrativos. Se exige que la API pública entregue solo los metadatos indispensables para mostrar variantes agotadas, sin exponer información privada ni inventar disponibilidad.

**CAT-05.** En móvil, el selector debe poder desplazarse o adaptarse sin romper la galería, el visor, los enlaces, navegación atrás/adelante, scroll incremental ni accesibilidad (teclado, foco, etiquetas descriptivas).

**CAT-06.** Los enlaces individuales continuarán funcionando al compartir productos por WhatsApp o abrir `/producto/{id}`, y el modo `preview` conservará sus rutas internas. No alterar Caddy/DNS por esta función.

**CAT-07.** Si la URL directa `/producto/{id}` corresponde a un producto **que aún existe pero ya está agotado**, abrir normalmente su **ficha de detalle**, incluyendo imágenes, nombre, características y un aviso **«Agotado» claramente visible**. No ofrecer acciones que aparenten compra o disponibilidad. Si tiene variantes vinculadas disponibles, mostrar el selector y permitir acceder a esas otras variantes; si todas están agotadas, informar de ello. **Que un producto no aparezca en búsquedas/listados no significa que su enlace directo deba dejar de funcionar.** Los productos inexistentes, eliminados o no publicables por otras causas siguen sujetos a reglas de acceso y error independientes; esta decisión no hace públicos datos privados ni obliga a implementar eliminación física.

## 6. C — Precio no definido y consulta por WhatsApp

**PRE-01.** Si el **precio comercial de venta mostrado** es nulo/no definido o **igual a Q0.00**, usar exactamente la etiqueta **«Consultar precio»** en tarjetas, resultados, carruseles, detalle y variantes; **nunca** mostrar `Q0.00` como si fuera precio comercial.

**PRE-02.** No presentar ese caso como descuento del 100 %, regalo ni oferta. Omitir tachados, porcentajes y comparaciones que requieren precio válido. No modificar costo de compra ni precio transaccional por la mera presentación pública.

**PRE-03.** Mostrar botón **«Consultar por WhatsApp»** que utilice el **número Business configurado actualmente** en el catálogo, sin duplicarlo en componentes. El mensaje preformateado incluirá nombre, selección de variante/atributos si existe, **código interno** e **hipervínculo público canónico**.

**PRE-04.** Codificar correctamente texto y URL para WhatsApp; validar el número de destino en configuración. No insertar teléfonos de clientes, datos internos sensibles ni campos administrativos en URLs públicas.

**PRE-05.** Si ya existe botón de WhatsApp en el detalle, **reutilizarlo/adaptarlo** en lugar de añadir otro sistema de contacto. La consulta no crea pedido, cobro o reserva.

**PRE-06.** La disponibilidad del producto es **independiente** del precio. Un artículo sin precio puede aparecer en consulta si el backend lo considera publicable; uno sin disponibilidad no debe aparentar estar disponible por mostrar «Consultar precio».

**PRE-07.** La edición móvil de precio se reflejará en consultas públicas posteriores según la política de actualización/caché existente; garantizar que el valor Q0 no se muestre como precio por errores de caché o estado anterior.

## 7. D — Asignación de precios desde el teléfono

**ETI-01.** Permitir una experiencia rápida y **adaptada a móvil** para personal autorizado: buscar por nombre, código interno, código del fabricante o escaneo; ver producto/foto y **establecer o actualizar el precio de venta**.

**ETI-02.** Priorizar filtro **«Sin precio / pendientes de precio»**, búsqueda rápida y paso al siguiente producto sin perder el contexto. Confirmar cambios y mostrar errores de validación sin editar costos/ventas históricas.

**ETI-03.** La modificación del precio debe registrarse por el servicio de Producto con controles de concurrencia y validación en backend. Mantener las reglas actuales del precio sugerido, precio por unidad y precio final de venta; la implementación identificará qué campo es el precio **que debe imprimirse**. La asignación rápida del **precio de venta** nunca debe sobrescribir el **costo de adquisición** ni cambiar su estado de conocido/desconocido.

**ETI-04.** Guardar el precio en ResellManager para que cualquier usuario autorizado vea el nuevo valor sin depender de comunicación con impresora, dispositivos emparejados o archivo local.

**ETI-05.** Las etiquetas representan el **precio vigente cuando se imprimieron**. Si cambia, permitir detectar necesidad de **reimpresión** o advertir que la etiqueta anterior quedó desactualizada; no asumir que los adhesivos físicos cambian automáticamente.

### 7.1 Filtro rápido «Sin precio asignado» — requisito prioritario

**FIL-01.** En el **listado administrativo existente de Productos** (no en el catálogo público), añadir un filtro rápido y claramente visible **«Sin precio asignado»**, accesible incluso cuando el panel avanzado esté cerrado.

**FIL-02.** La condición «Sin precio asignado» identifica productos cuyo **precio comercial de venta relevante es nulo/no definido o Q0.00**. No confundir precio de venta con costo de compra, utilidad, valor de inventario ni precio histórico de una venta. Revisar el campo real del dominio antes de implementarlo.

**FIL-03.** Mostrar **«Pendiente de precio»** en administración en lugar de `Q0.00`, de modo coherente con «Consultar precio» en el catálogo público; ningún producto se interpreta como gratuito por falta de precio.

**FIL-04.** Permitir combinar «Sin precio asignado» con búsqueda por texto/código y categoría, limpiar el filtro independientemente y restablecer todos los filtros con la acción general correspondiente. Mostrar el filtro activo y un estado vacío comprensible cuando no haya coincidencias.

**FIL-05.** Ejecutar el filtrado en **backend/SQLite**, antes de la paginación incremental; no descargar todos los productos para ocultar los que tengan precio. Preservar lotes/cursor, orden estable, reintentos y accesibilidad del listado actual.

### 7.2 Panel de filtros avanzados

**FIL-06.** Incorporar un panel **«Filtros avanzados»** que se pueda **abrir/cerrar** y permanezca **contraído por defecto**. Mantener accesibles fuera del panel la búsqueda principal, categoría y «Sin precio asignado».

**FIL-07.** Filtros mínimos según los **atributos ya presentes y válidos** del producto: **marca, modelo, presentación en ml, peso** y, cuando tengan sentido, otras propiedades existentes como color, talla o presentación comercial. Los filtros inaplicables a la categoría actual deben ocultarse, deshabilitarse o explicarse claramente.

**FIL-08.** Los filtros deben poder **combinarse** entre sí y con los filtros básicos. Validar rangos/unidades de medidas en el servidor; no fabricar propiedades nuevas ni permitir consultas excesivas. Establecer opciones y correspondencias usando el modelo real.

**FIL-09.** Al aplicar o limpiar filtros se reinicia la paginación desde el comienzo, se descartan solicitudes obsoletas y no se muestran resultados del filtro anterior. Mantener la búsqueda incremental y los controles «Cargar más»/reintento existentes.

**FIL-10.** Diseñar una interfaz cómoda en el teléfono: controles táctiles, panel desplegable sin ocupar la pantalla cuando está cerrado, indicación de filtros aplicados y retorno al listado sin perder las selecciones activas.

### 7.3 Edición rápida de precios desde la lista, mediante diálogo

**FIL-11.** En cada resultado del listado administrativo de Productos, especialmente al aplicar **«Sin precio asignado»**, mostrar un botón visible **«Asignar precio»** (o «Editar precio» cuando ya tenga un valor válido). Pulsarlo abrirá **un diálogo/modal** sin navegar al formulario completo de edición.

**FIL-12.** El diálogo mostrará **la imagen principal en tamaño grande y con buena definición** para reconocer el producto desde el teléfono, sin recortar o distorsionar su contenido. Debe ofrecer un estado alternativo claro si el producto no dispone de fotografía. Se pueden aprovechar imágenes y rutas ya existentes, sin duplicar archivos.

**FIL-13.** El diálogo presentará **solo como información de lectura** el nombre comercial, código interno, código de barras externo (si existe), marca, modelo, color, talla, aroma, presentación u otros atributos disponibles que ayuden a distinguir productos similares. No incorporar controles de edición para estos campos.

**FIL-14.** **El único dato editable del diálogo será el precio comercial de venta en quetzales.** Mostrar una entrada numérica grande, legible, con teclado decimal adecuado en móvil, formato monetario y precisión que corresponda al contrato actual. Para el flujo «Sin precio asignado», exigir un **importe válido mayor que Q0.00** al guardar. No permitir editar costos de compra, inventario, imágenes ni otros datos desde aquí.

**FIL-15.** Colocar acciones claras **«Guardar precio»** y **«Cancelar»**, visibles en móvil incluso con teclado virtual. Cancelar o cerrar el diálogo **no modifica nada**. Mientras se procesa el guardado, evitar envíos duplicados y comunicar el resultado. Validar en backend y aplicar controles de autorización, concurrencia e integridad del servicio actual de Producto.

**FIL-16.** **Tras un guardado exitoso**, cerrar el diálogo y **actualizar automáticamente los resultados del listado en la página actual**, conservando la búsqueda, la categoría, el panel avanzado y los filtros seleccionados. Cuando está activo **«Sin precio asignado»**, el producto cuyo precio ya es mayor que cero **deja de aparecer sin que la usuaria tenga que recargar manualmente**. Si procede, cargar el siguiente resultado coincidente desde el backend, sin omitir ni duplicar otros productos por la paginación incremental.

**FIL-17.** Si la edición se hizo **sin** el filtro «Sin precio asignado», conservar el producto en el listado si sigue cumpliendo los filtros y mostrar el **nuevo precio actualizado**. Si falla el guardado, mantener abierto el diálogo, el importe introducido y el contexto del listado; **no retirar** el producto ni mostrar éxito engañoso.

**FIL-18.** Al cerrar el diálogo, restituir el foco al listado y permitir que la usuaria continúe con otro producto sin navegar innecesariamente. Respetar scroll móvil, accesibilidad, bloqueo del fondo durante el diálogo y márgenes seguros del iPhone. **No recargar toda la aplicación ni perder el estado de los filtros** para actualizar los resultados.

## 8. E — Generación y administración de etiquetas

### 8.1 Contenido y formato

**ETI-06.** Incorporar una pantalla **«Etiquetas de productos»** accesible desde administración, con filtros (sin etiqueta, precio actualizado, producto, categoría, stock disponible y/o selección manual).

**ETI-07.** Cada etiqueta deberá contener, como mínimo:

- Nombre comercial legible (abreviación controlada si no cabe).
- Precio en quetzales con dos decimales, **si existe precio mayor que Q0**; si no existe, indicar **«Consultar precio»** o permitir generar una etiqueta sin importe, sin imprimir «Q0.00».
- **Código interno actual del producto (por ejemplo `PRO-…`)** tanto en texto como codificado en un código de barras **Code 128** u otro estándar compatible y comprobado.
- Opcionalmente marca, talla/color/aroma y distintivo de Virtuosa Store según espacio.

**ETI-08.** La impresión utilizará **el identificador interno existente como valor del código de barras**, sin crear otro ID arbitrario. Si el identificador no cumple requisitos de codificación o colisiona con otro código, advertirlo y resolverlo antes de imprimir.

**ETI-09.** Tamaño inicial sugerido: **50 × 30 mm** para etiquetas adhesivas de producto. Permitir perfil configurable con ancho, alto, márgenes, resolución y orientación, así como una hoja de prueba de alineación; no fijar un tamaño único sin validar consumibles.

**ETI-10.** La plantilla debe mantener **barras nítidas, zona silenciosa suficiente y altura legible**, sin reducir a una imagen borrosa o escalar de modo que el lector falle. Confirmar lectura real con el scanner existente en iPhone y, si se usa, lector físico.

### 8.2 Lotes, cantidades y resultados

**ETI-11.** Poder seleccionar **muchos productos** y establecer cantidad de etiquetas por cada uno; por defecto sugerir las unidades físicas disponibles cuando tenga sentido, con posibilidad de ajustar para no desperdiciar papel.

**ETI-12.** **Decisión aprobada: etiquetas por Producto, no por unidad física.** Todas las copias de un mismo producto codifican **el mismo `PRO-…`**; el scanner identifica el producto y sigue seleccionando unidades con sus reglas existentes. La cantidad de copias se elige por producto y no se crea trazabilidad unitaria ni un ID distinto por etiqueta.

**ETI-13.** Proporcionar **vista previa** con tamaño real, número de etiquetas, saltos, cortes y cantidad de copias antes de enviar a impresión.

**ETI-14.** Producir una salida **PDF descargable** y correctamente dimensionada para impresora térmica o para impresión en hojas cuando se configure ese perfil. Esto permite imprimir desde una computadora aunque el dispositivo carezca de integración directa con ResellManager.

**ETI-15.** La impresión directa vía Wi-Fi/USB/Bluetooth es **dependiente del hardware elegido**; no prometer impresión desde navegador móvil mediante Bluetooth genérico. Si se implementa un conector/controlador de impresión, debe ser opcional y no bloquear la exportación PDF.

**ETI-16.** Poder **reimprimir** una etiqueta sin generar nuevos productos ni modificar inventario. El sistema mostrará el precio actual y advertirá si una etiqueta anteriormente impresa contenía precio anterior. Si se registra estado «impresa», no marcarlo automáticamente por la sola generación de un PDF que podría no haberse impreso.

**ETI-17.** No imprimir información financiera sensible (costos de compra, utilidades, saldos, proveedor ni datos privados) en etiquetas públicas.

### 8.3 Escaneo del código interno impreso — requisito crítico

**ETI-18.** Hoy los flujos de scanner operativo priorizan/consultan `Producto.CodigoBarras`. Adaptar el **resolver local** para aceptar el valor del **código interno `PRO-…` impreso**, además de códigos externos existentes, sin consultar servicios externos por un escaneo operativo normal.

**ETI-19.** Mantener compatibilidad con códigos externos y búsquedas manuales. Resolver colisiones y ambigüedades de manera determinista y segura; no asignar una venta al producto incorrecto si un valor coincide con códigos de distinta clase.

**ETI-20.** Al leer una etiqueta interna en Venta Directa, seguir los controles existentes de disponibilidad y selección de unidades físicas; el código identifica el Producto, **no reserva ni vende automáticamente**. En Inventario/Nueva compra, respetar las reglas específicas del flujo.

**ETI-21.** Probar Code 128 con identificadores reales sintéticos (`PRO-000…`, letras y guiones), resoluciones 203/300 dpi, distancias de cámara y dispositivos móviles reales. Reutilizar el componente scanner; no crear un decodificador paralelo.

### 8.4 Exportación Excel para lotes NIIMBOT (compatibilidad a validar)

**ETI-22.** Ofrecer además una **exportación a Excel (.xlsx) para impresión por lotes con la aplicación NIIMBOT**, que contenga al menos nombre abreviado, precio de venta, código interno, atributos distintivos y cantidad de copias. No confundir esta exportación de etiquetas con el Excel de reportes financieros.

**ETI-23.** La exportación deberá permitir una **fila por etiqueta** cuando sea necesario para el flujo de importación de la app, duplicando las filas por número de copias solicitado, o una plantilla equivalente validada con la versión real de NIIMBOT. Verificar que el tipo de código de barras, plantilla, tamaño y acceso a importación masiva estén disponibles antes de prometer impresión automática.

**ETI-24.** Mantener el **PDF a tamaño real** como alternativa independiente de la impresora; ni descargar Excel ni PDF equivale a haber impreso una etiqueta ni debe modificar existencias.

## 9. F — Impresora: requisitos de compra e integración (decisión pendiente)

No seleccionar impresora por tener Bluetooth/Wi-Fi solamente. Evaluar:

1. **Ancho útil:** soporte de etiquetas adhesivas alrededor de **50 × 30 mm**; comprobar rollos disponibles localmente.
2. **Resolución:** **203 dpi** como punto de partida para Code 128 bien dimensionado; preferir **300 dpi** si la etiqueta es pequeña o tiene mucho texto, y validar físicamente.
3. **Conectividad real:** USB para laptop; Wi-Fi para red y posible AirPrint si el modelo **lo anuncia expresamente**; Bluetooth de modelos portátiles normalmente exige app del fabricante.
4. **Compatibilidad con Linux/Ubuntu**, si la impresión final se realizará desde la laptop habitual; verificar controlador y tamaño personalizado. No asumir que disponer de aplicación iOS asegura impresión PDF directa desde el navegador.
5. **Consumibles:** precio por rollo, disponibilidad, adhesivo removible o apto para empaque, calidad de papel y comportamiento ante humedad/calor. Las etiquetas térmicas directas pueden perder legibilidad con calor y sol; elegir material según tiempo de almacenamiento.
6. **Integración:** impresión fiable de PDF a escala 100 % desde ResellManager/computadora, antes de desarrollar comandos específicos de marca.
7. **Seguridad:** no conectar una impresora doméstica a Internet ni enviar trabajos de impresión desatendidos desde el backend. La acción de imprimir parte de una sesión autorizada.

**Alternativas evaluadas como orientación (no compra aprobada):**

- **NIIMBOT B1 (preferida provisionalmente):** 203 dpi, Bluetooth/USB, acepta etiquetas entre 20 y 50 mm y existe consumible 50 × 30 mm; económica para probar, pero la aplicación y la integración/driver en Ubuntu requieren verificación. Fuente fabricante: <https://www.niimbot.com/us/products/niimbot-b1-food-label-printer>. Disponibilidad local: <https://www.kemik.gt/niimbot>.
- **Brother QL-820NWB:** Wi-Fi, Ethernet, USB, Bluetooth, **AirPrint** y soporte de descargas Linux; apta para imprimir desde dispositivos Apple y sistemas de escritorio, con costo considerablemente superior y consumibles DK. Confirmar formato de etiqueta real y costos de rollos. Fabricante: <https://www.brother-usa.com/p/thermal-printers-labelers/QL820NWB>; soporte: <https://support.brother.com/g/b/downloadtop.aspx?c=mx&lang=es&prod=lpql820nwbeus>.

## 10. G — Costos de adquisición desconocidos y utilidad parcial

### 10.1 Principio y alcance

**COS-01.** Permitir que mercancía nueva o ya registrada se conserve en Compras/Inventario con **costo de adquisición no conocido** cuando no se dispone de la factura o del importe atribuible a la unidad. Esto facilita registrar las dos cajas recientes sin exigir investigar cada comprobante antes de poner precios.

**COS-02.** Distinguir de forma inequívoca:

- **Costo conocido:** importe verificado de una unidad o transacción, que puede utilizarse para calcular utilidad.
- **Costo desconocido / pendiente:** importe sin determinar; se muestra **«Costo no registrado»** y **no se interpreta como Q0.00**, ni como mercancía gratuita.
- **Costo estimado:** **no admitido** en el alcance aprobado, porque presentaría utilidades sustentadas en importes no comprobados. No añadir un tercer estado ni incluir estimaciones en reportes.

**COS-03.** Como el costo vigente en el dominio pertenece a **`UnidadInventario`**, al detalle de compra y/o al **snapshot de venta**, y no a `Producto`, revisar todas las validaciones, DTO, columnas, consultas, anulaciones y pruebas antes de decidir cómo representar la ausencia (`null`, estado separado u otra alternativa). No imponer una migración concreta sin analizar datos existentes.

**COS-04.** **Decisión de datos existentes y responsabilidad:** el responsable confirma que **todos los productos/unidades que ha cargado hasta ahora tienen Q0.00 porque no se conoce su costo**. Esos importes deben pasar a **«Costo desconocido»**. **La migración de esquema/datos será diseñada, implementada, probada y ejecutada por Codex en la fase autorizada de implementación**, no mediante edición manual del usuario. El contrato de Codex deberá delimitar qué registros históricos de compra, unidad y snapshot de venta (si existen) corresponden a esa población, incluir respaldo/validación previa, ejecución idempotente, comprobaciones posteriores y trazabilidad del cambio. No transformar indiscriminadamente Q0.00 genuinos, datos futuros o registros ajenos; si no se puede determinar el alcance sin ambigüedad, bloquear esos registros y reportarlos, no adivinar.

### 10.2 Captura y actualización posterior

**COS-05.** El costo desconocido **no impedirá capturar o recibir unidades, gestionar inventario, tomar fotografías ni asignar el precio de venta** desde el diálogo rápido. Mantener las demás reglas de Compra, recepción, stock, pedido y venta.

**COS-06.** En las pantallas internas correspondientes se mostrará **«Costo no registrado»** en lugar de Q0.00 cuando realmente se desconozca. En el **modal rápido «Asignar precio»**, los datos de costo seguirán siendo **solo lectura si se muestran**; su único campo editable continuará siendo **precio de venta**.

**COS-07.** Permitir completar o corregir un **costo real comprobado** posteriormente, incluida una unidad ya vendida, mediante una **nota de corrección** vinculada a la unidad y, cuando aplique, a la venta/transacción. Conservar **importe anterior, nuevo, fecha/hora, responsable, motivo y referencia disponible**; no borrar el registro original. No exigir costo durante carga inicial; una variante no hereda el de otra.

**COS-08.** Registrar procedencia cuando sea posible (compra, caja/envío, comprobante), **sin exigir el prorrateo del flete por caja ni inventar impuestos**. El cálculo de landed cost sigue siendo un análisis independiente.

### 10.3 Ventas, Dashboard y reportes

**COS-09.** Si una unidad se vende sin conocer el costo, conservar íntegros los **ingresos por ventas y cobros reales**, pero marcar la **utilidad de esa línea como no calculable**; no guardar/mostrar una utilidad equivalente al precio de venta ni sumar un costo implícito cero.

**COS-10.** Reportar por separado **utilidad calculable de líneas con costo conocido**, **cantidad de líneas/unidades con costo pendiente** y **importe de ventas afectado**. No presentar la utilidad parcial como ganancia total ni calcular márgenes globales engañosos; las exportaciones deberán conservar estas distinciones.

**COS-11.** Cuando se conoce o corrige un costo **después de vender**, guardar una **nota de corrección auditable** con vínculo a venta/unidad, fecha de corrección, autor, motivo y datos anterior/nuevo. El historial conservará el hecho original: **no sobrescribir silenciosamente el costo original de la venta**. La utilidad podrá pasar de «no calculable» a «corregida», indicando la corrección aplicada. Debe ser posible consultar el resultado original y el revisado, incluso ante varias correcciones sucesivas. El diseño técnico establecerá almacenamiento y comportamiento en periodos cerrados, sin reescrituras invisibles.

**COS-12.** Los costos internos, su condición de «pendiente» y las utilidades **no aparecen en el catálogo público, mensajes de WhatsApp ni etiquetas de precios**. El cliente verá su **precio de venta** o «Consultar precio», independientemente de que el negocio conozca o no el costo.

### 10.4 Límites de la primera entrega

**COS-13.** La capacidad aprobada es **costo conocido/desconocido + migración controlada de los Q0 iniciales + captura/corrección posterior con nota auditada + utilidad parcial claramente identificada**. **No incluye costos estimados**, contabilidad general, prorrateo entre cajas, reconocimiento fiscal, reconstrucción de facturas, OCR ni sincronización de proveedores.

**COS-14.** El diseño técnico debe revisar si los contratos actuales de Compras, `UnidadInventario.Costo`, `DetalleVenta.CostoUnitario`, Dashboard y reportes **requieren migración y adaptación de reglas de validación**. No es suficiente cambiar solo un texto en la interfaz.

### 10.5 Versionado lógico de utilidades e informes (aprobado funcionalmente)

**HIS-01.** Cada venta conserva su importe, costo conocido/desconocido y estado **tal como quedaron registrados en el momento de la operación**; cualquier corrección posterior se representa mediante un **evento/nota inmutable de corrección**, relacionado con la transacción, y no por cambios sin evidencia al registro original.

**HIS-02.** Permitir consultar la **versión original** de la utilidad y el **estado revisado** a una fecha/hora de corte, tomando en cuenta las correcciones efectivamente registradas hasta entonces. Se identifican la fecha del hecho original, la fecha de registro de cada corrección y su efecto sobre utilidad. La numeración formal de revisiones puede ser por venta o por reporte, según el diseño técnico, sin exigir duplicar informes completos en cada consulta.

**HIS-03.** Si un informe se **emite o exporta** (PDF/Excel), debe identificar **periodo, fecha de generación y fecha de corte/as-of**; cuando se emita de nuevo después de una corrección, aparecerá como **revisión** con la causa y referencia de corrección disponibles. Las exportaciones anteriores no se reescriben; solo conservar copias inmutables en servidor si se aprueba expresamente el archivado oficial de reportes (no está incluido por defecto).

**HIS-04.** En el Dashboard se puede mostrar el **estado revisado más reciente**, pero con indicación clara de que hubo correcciones y de cuántas ventas siguen sin costo conocido. Nunca rotular utilidad parcial como utilidad total. Para comparar periodos, diferenciar **fecha de venta** de **fecha de corrección**, sin asumir todavía cómo afectan a cierres contables/fiscales.

**HIS-05.** No mezclar este versionado de correcciones financieras con los **tags/releases de Git** ni con la creación de versiones completas de la base de datos: son historiales de naturaleza distinta.

### 10.6 Roles y permisos futuros (idea reservada; SIN diseño NI implementación)

**ROL-01.** Se conserva **únicamente la intención de negocio** de incorporar en otra fase roles como **Administrador, Supervisor y Vendedor/personal operativo**, con permisos según sus responsabilidades. **No se aprueba ni se diseña ahora un modelo de datos** (`Rol`, `Permiso`, relaciones con `Usuario`, permisos específicos, asignación simple o múltiple), ni se decide el mecanismo de almacenamiento.

**ROL-02.** Cuando se aborde ese proyecto independiente, **evaluar primero las capacidades existentes de ASP.NET / ASP.NET Core Identity y la autenticación real del repositorio**, evitando duplicar innecesariamente infraestructura de seguridad. Las tablas, permisos y reglas concretas quedan abiertos para esa futura discusión.

**ROL-03.** **Fuera del alcance actual:** no implementar, diseñar ni migrar roles, usuarios, tablas, políticas generales ni pantallas de administración de permisos. Las operaciones sensibles nuevas, como corregir costos, **sí deberán protegerse mediante una medida mínima de autorización segura compatible con el sistema vigente**, definida específicamente al implementar esa operación, sin anticipar el futuro sistema general de roles.


## 11. Criterios de aceptación mínimos

**VAR-AC-01.** Crear grupo «Bolso Elegance» con Negro **disponible** y Beige/Vino **agotados**: **solo Negro** aparece como tarjeta individual en portada/búsqueda/filtros. En su detalle se muestran los tres colores, con Beige/Vino rotulados «Agotado».

**VAR-AC-02.** Cuando Negro y Beige están **disponibles**, ambos aparecen separadamente en búsquedas; seleccionar Beige desde el detalle de Negro abre su página, imagen, precio y URL. Si Beige se agota después, desaparece de búsquedas, pero se mantiene como opción «Agotado» **no seleccionable** desde un detalle disponible.

**VAR-AC-03.** Una camiseta con combinaciones «Negro/M», «Negro/L», «Blanco/M» permite elegir combinaciones sin fusionar existencias, costos ni códigos.

**VAR-AC-04.** Intentar vincular un producto a dos grupos se rechaza o exige desvinculación confirmada; no se pierde historial.

**VAR-AC-05.** Producto independiente sin existencias **no aparece** en resultados públicos; producto independiente disponible **sí aparece**. Un grupo con todas sus variantes agotadas no genera tarjetas; administración conserva todos los registros.

**CAT-AC-01.** Se comparte el enlace de un bolso disponible; posteriormente se agota. Abrir el **mismo enlace** muestra imágenes, nombre y características, además de un aviso **«Agotado» destacado**, sin acciones de compra. El bolso **no aparece en búsqueda, portada ni carruseles**.

**CAT-AC-02.** Abrir por enlace directo una variante agotada con otra variante disponible muestra la ficha agotada y permite pasar a la **variante disponible**. Si todas están agotadas, las fichas todavía existentes siguen accesibles por sus URLs directas, aunque ninguna aparezca en resultados. Si el ID no existe, no se inventa una ficha ni se exponen datos administrativos.

**PRE-AC-01.** Producto con precio `NULL` o `Q0.00` muestra **«Consultar precio»** tanto en tarjeta como en detalle, sin descuento falso.

**PRE-AC-02.** WhatsApp abre al destino configurado con mensaje que contiene nombre, código interno, variante y URL pública codificados correctamente, sin exponer datos privados.

**FIL-AC-01.** En Productos, activar «Sin precio asignado» muestra productos con precio `NULL`/`Q0.00` incluso si hay cientos de páginas; no muestra productos con precio mayor que cero y no depende de datos ya cargados en el navegador.

**FIL-AC-02.** Combinar filtro sin precio, categoría, marca y modelo, con presentación/ml o peso cuando aplique, funciona con backend y paginación sin duplicaciones; «Filtros avanzados» está contraído inicialmente y puede abrirse desde móvil.

**FIL-AC-03.** Desde iPhone, la usuaria activa «Sin precio asignado», toca «Asignar precio» en un resultado y se abre un **diálogo con fotografía grande**, nombre, código y detalles **de solo lectura**. **Solo puede editar el precio**, sin entrar al formulario completo.

**FIL-AC-04.** Al guardar un precio válido mayor que Q0.00, el diálogo se cierra, el listado **se actualiza automáticamente** y el producto **desaparece del filtro «Sin precio asignado»**, preservando búsqueda, filtros avanzados, scroll y los demás resultados. No hace falta recargar manualmente la página.

**FIL-AC-05.** Si el diálogo se cancela o falla el guardado, el producto continúa en los resultados, el precio no cambia y la usuaria recibe aviso. Doble clic o dos envíos no producen escrituras duplicadas.

**FIL-AC-06.** Sin el filtro «Sin precio asignado», guardar un nuevo precio actualiza el valor mostrado sin eliminar injustificadamente el producto de su listado; el modal no permite editar costo, inventario, fotos ni nombre.

**ETI-AC-01.** Desde móvil se asigna precio a producto sin precio; la pantalla administrativa y el catálogo reflejan el valor actualizado en la siguiente consulta válida.

**ETI-AC-02.** Desde escritorio se seleccionan 3 productos, 2/1/4 copias; la vista previa y el PDF contienen **7 etiquetas por Producto**, no por unidades físicas, del tamaño configurado. Todas las copias del mismo producto llevan el **mismo `PRO-…`** y precio.

**ETI-AC-03.** Producto sin `CodigoBarras` de fabricante imprime `PRO-…` como Code 128; al escanearlo con el flujo existente de Venta Directa e Inventario se identifica **el producto correcto** sin consulta a terceros y sin venta automática.

**ETI-AC-04.** Producto cuyo precio cambió puede reimprimirse; no se muestran etiquetas antiguas como si tuvieran el precio nuevo.

**ETI-AC-05.** Exportar/abrir PDF no modifica stock ni marca automáticamente las etiquetas como impresas. Fallos de impresora no cambian inventario ni precio.

**ETI-AC-06.** La impresión es legible con papel real, dimensiones exactas, escaneo en iPhone (incluido el navegador usado para el scanner) y, si procede, lector físico.

**ETI-AC-07.** Un lote de 3 productos con 2/1/4 copias genera Excel de etiquetas apto para revisión con **7 filas imprimibles**, sin datos de costos, y el PDF alternativo conserva siete etiquetas. Su compatibilidad real con la app NIIMBOT se valida antes de dar la integración por terminada.

**COS-AC-01.** Se registra una unidad de las cajas recientes sin documento que confirme su costo, queda «Costo no registrado» y se puede fotografiar, poner precio de venta y gestionar inventario sin inventar Q0.00.

**COS-AC-02.** Una unidad de costo desconocido vendida por Q395 registra Q395 como venta, pero la utilidad de esa línea queda **no calculable**; Dashboard no suma Q395 como ganancia ni presenta una utilidad parcial como ganancia total.

**COS-AC-03.** Dos ventas de Q250 y Q395 con costo conocido Q100 en la primera y desconocido en la segunda muestran **Q150 de utilidad calculable** y **Q395 de ventas sin costo disponible**, no Q545 de ganancia total.

**COS-AC-04.** Un usuario autorizado completa el costo real de una unidad, incluso **después de venderla**, mediante **nota de corrección** con responsable, motivo, fecha, importe anterior y nuevo y referencia a unidad/venta. La utilidad corregida se muestra con explicación y el dato histórico original sigue consultable.

**COS-AC-05.** En la fase de implementación, **Codex** prepara y ejecuta el proceso aprobado para convertir **solo los Q0.00 históricos identificados por el responsable** a «Costo no registrado», con validación de alcance, respaldo, idempotencia, registro de migración, comprobación posterior y pruebas. No exige editar manualmente filas al usuario; si existen registros ambiguos, la migración los informa sin atribuirles costos inventados.

**COS-AC-06.** Reportes y exportaciones que muestran utilidad señalan correctamente resultados parciales, número de líneas sin costo y ventas afectadas; cuando existe una corrección se muestra su origen y fecha, sin sustituir silenciosamente resultados históricos. Ni etiquetas ni catálogo exponen costos internos.

**COS-AC-07.** Un usuario sin autorización de costos no puede modificarlos mediante la UI ni invocando directamente el backend. La implementación del **sistema general de roles queda excluida** de esta entrega; se documentará un control mínimo compatible con la autenticación vigente para las operaciones privilegiadas y se verificará con pruebas de acceso denegado.

**HIS-AC-01.** Venta de Q300 inicialmente sin costo conocido: el reporte original indica utilidad no calculable. Posteriormente se registra mediante nota el costo real Q125; una revisión del mismo periodo muestra utilidad corregida Q175 e identifica **la nota, responsable, motivo y fecha**. Se puede consultar cómo se veía el reporte antes de la nota sin modificar la venta ni la exportación antigua.

**HIS-AC-02.** Una segunda corrección genera una nueva entrada vinculada; se conservan el registro inicial y la primera corrección y las consultas a fechas de corte anteriores siguen dando el mismo resultado.

**ROL-AC-01.** La idea de roles configurables y posible reutilización de **ASP.NET Core Identity** queda registrada **sin modelo técnico, decisiones de esquema ni implementación**. Los controles específicos de acceso a correcciones de costo deben funcionar en backend con las capacidades realmente disponibles, sin adelantar el sistema general de roles.

**REG-AC-01.** Compras, scanner de cámara, Venta Directa, filtros/carruseles de catálogo, paginación, imagen principal, galería, disponibilidad y URLs anteriores conservan comportamiento.

**REG-AC-02.** **Recordatorio al cerrar la implementación completa:** revisar junto con el responsable la API pública del catálogo, en particular la consulta segura por URL directa de fichas agotadas, sus variantes y la coherencia de disponibilidad entre listado y detalle. Este vistazo es un control final posterior a la implementación, **no un requisito de intervención manual previa**. No dar el cierre por revisado sin señalar expresamente esta comprobación.

## 12. Aspectos aún pendientes de confirmar antes del diseño técnico

1. **Modelo de impresora y tamaño real de rollo:** condicionan perfiles, drivers, márgenes y si existe impresión nativa por AirPrint. **No comprar antes de una prueba de compatibilidad/consumibles**.
2. **Etiqueta por Producto — resuelto:** cada copia usa `PRO-…`, no se rastrea cada unidad por etiqueta. Solo queda comprobar dimensiones, consumibles y cantidad óptima de copias en impresión física.
3. **Relación precio comercial/venta:** identificar el campo fuente del precio visible e impreso, y si hay casos de precios distintos por lote/unidad; preservar precio final transaccional editable según reglas existentes.
4. **Ubicación y comportamiento de la edición rápida:** queda aprobado **«Asignar precio» directamente en cada resultado**, con **diálogo de imagen grande, detalles de lectura y precio como único campo editable**; al guardar refrescar automáticamente el listado filtrado y retirar el producto de «Sin precio asignado». Solo quedan por ajustar detalles visuales según la interfaz real y la ubicación de «Etiquetas».
5. **Integración de perfiles de impresión:** salida PDF y exportación XLSX de lotes desde el comienzo; el uso de importación masiva/plantilla en NIIMBOT y la impresión directa desde iPhone requieren validar app, hardware y flujo.
6. **Visibilidad y enlaces de agotados — resuelto funcionalmente:** productos sin disponibilidad no aparecen en portada, resultados, filtros ni búsquedas; desde un detalle **disponible** se muestran sus variantes agotadas como «Agotado». **Una URL directa a un producto que todavía existe abre su ficha aunque esté agotado**, con aviso visible y sin acción de compra, pudiendo acceder a otras variantes disponibles. Queda solo el detalle técnico de lectura pública segura de fichas agotadas y metadatos de variantes.
7. **Política de material físico:** etiqueta adhesiva sobre empaque vs precio en prenda (cartón colgante, adhesivo removible), según productos reales.
8. **Costos existentes — decidido y delegado:** el responsable declara desconocidos los Q0.00 de los registros que ha cargado hasta ahora. **Codex realizará la migración al implementar** conforme al alcance, respaldo, idempotencia, auditoría y verificaciones exigidos. No habrá edición manual de BD por el usuario. Solo requiere investigación técnica del conjunto afectado y bloqueo de casos ambiguos.
9. **Corrección posterior a venta y versionado — aprobados:** notas inmutables vinculadas, vista original vs revisada, reportes con fecha de corte e identificación de correcciones. Falta definir en el diseño técnico cómo materializar las consultas, si existe cierre de periodos y qué reportes se archivarán oficialmente; prohibido recalcular sin evidencia.
10. **Costos estimados — descartados:** no son parte del módulo y no podrán usarse para presentar ganancias.
11. **Roles y permisos — futuro, sin diseño ni implementación:** solo se reserva la idea de Administrador/Supervisor/Vendedor y aprovechar el ecosistema de ASP.NET si conviene; no diseñar tablas, relaciones ni matriz de permisos en este proyecto. **La autorización mínima de correcciones de costo**, sin acceso indiscriminado, sí exige una solución técnica puntual al implementar esas operaciones.
12. **Origen/caja y costo puesto en Guatemala:** si solo se guarda referencia a las dos cajas y facturas o si se añadirá algún reparto futuro de envíos; **landed cost sigue fuera de este alcance**.
13. **Informes históricos — versionado aprobado:** vista original y revisada basada en historial de correcciones y fecha de corte; falta definir política de cierres contables, materialización eficiente e informes que se archivan como emisión oficial. Los indicadores de utilidad parcial siguen siendo obligatorios.

Las **decisiones funcionales principales están cerradas**, incluida la navegación mediante enlaces directos a fichas agotadas existentes. Quedan pendientes técnicos y de operación real: **Codex** tendrá que identificar y migrar de forma segura los registros históricos; resolver la autorización mínima de costos **sin diseñar ni implementar roles**; concretar consultas versionadas y política de cierres de periodos; y validar la **compatibilidad física de NIIMBOT**, los formatos de etiqueta y el flujo de impresión. No inventar reglas comerciales nuevas.

## 13. Orden sugerido de implementación

1. Auditar contratos de Producto, `UnidadInventario`, `CodigoInterno`, `CodigoBarras`, precios y scanner; definir relaciones y atributos de variante sin duplicar entidades.
2. Implementar grupos de variantes y su administración; exponer variantes elegibles en API pública, sin agrupar listados; UI de detalle por variante.
3. Implementar presentación «Consultar precio» y adaptación del contacto WhatsApp existente.
4. Incorporar filtro rápido **«Sin precio asignado»** y panel de **filtros avanzados contraído por defecto**, preservando búsqueda backend, categoría, cursor, scroll incremental y edición rápida de precios desde teléfono.
5. Incorporar etiquetas: perfil configurable, vista previa, PDF a tamaño real y Excel de datos por lote para NIIMBOT, con selección y cantidades; verificar impresión real antes de dar compatibilidad por probada.
6. Adaptar scanner operativo para **código interno** y realizar QA óptico con una impresora/rollo elegidos.
7. Codex, **cuando se autorice la implementación**, diseñará y ejecutará **costo conocido/desconocido**, **migración auditada y acotada de los Q0 históricos**, **notas de corrección** y **consultas/reportes con versionado lógico**. Adaptar Compras, Inventario, snapshots, Dashboard, reportes y exportaciones sin generar utilidad ficticia ni recalcular resultados silenciosamente. **No diseñar ni implementar roles ni migración de usuarios** en esta etapa; conservar solo la intención futura y resolver autorización mínima compatible con el sistema vigente.
8. Probar regresiones, seguridad, móviles/escritorio, escenarios con costo desconocido y documentar decisiones técnicas. Actualizar documentación canónica y estado de pendientes al implementarse. **Al terminar toda la implementación, recordar expresamente al responsable que eche un vistazo a la API pública y al acceso directo a productos agotados**, conforme a REG-AC-02.

---

**Revisión y publicación:** documento revisado por el responsable y autorizado expresamente para publicarse en Git. La publicación no autoriza implementar, fusionar ramas ni desplegar.

**Nota para futuros agentes:** documento funcional, **no un prompt de ejecución**. Respetar `AGENTS.md`, conservar productos y datos históricos, **no interpretar costo pendiente como Q0.00 ni como utilidad**. Al autorizarse, **Codex** se encargará de migraciones y pruebas, nunca se requerirá editar datos manualmente al usuario. Mantener historial de correcciones y versiones lógicas de reportes; **roles/permisos generales quedan solo como idea futura, sin diseño**. No improvisar impresión inalámbrica iOS/Ubuntu ni modificar Git, servidor o producción sin autorización específica.

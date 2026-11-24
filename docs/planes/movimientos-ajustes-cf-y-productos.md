# Plan de implementación de movimientos ajustes CF y productos

**Estado:** plan aprobado y desarrollado en rama de trabajo. [Contrato de implementación y límites](../modules/operaciones-financieras-y-productos.md). Las pruebas físicas y la aplicación operativa sobre datos reales se distinguen del código implementado.
**Fecha:** 22 de noviembre de 2026.

El texto de paquetes conserva el plan aprobado del 22/11/2026; el estado posterior y las decisiones técnicas concretadas se describen en el contrato enlazado.

El objetivo principal es incorporar la apertura de cartera, Movimientos y Ajustes y Venta Directa a Consumidor Final con cobros y anulaciones trazables. La misma especificación documental incorpora Productos y Catálogo; ese bloque se planifica por separado, coordinando costos, reportes, exportaciones y scanner para evitar cambios incompatibles.

Las reglas funcionales aprobadas están en [Movimientos, Ajustes, Saldos Iniciales y CF](../modules/requisitos-movimientos-ajustes-y-consumidor-final.md) y [Productos, Catálogo, Etiquetas y Costos](../modules/requisitos-productos-catalogo-etiquetas-costos.md). Los paquetes siguientes desarrollan esas reglas; las elecciones de persistencia indicadas como propuestas se concretarán en el diseño técnico.

## Punto de partida

| Comportamiento actual | Consecuencia para la implementación |
| --- | --- |
| El saldo deriva de ventas registradas menos pagos; hay cálculos adicionales en el listado de clientes y la cancelación de ventas. | Extender una consulta financiera compartida y adaptar todas sus lecturas y validaciones. |
| El cliente está en `Pedido.ClienteId`, obligatorio; toda venta tiene un pedido único. | CF requiere adaptar pedido, venta, contratos y consultas manteniendo el pedido técnico. |
| `Pago` pertenece globalmente a un cliente, sin vínculo con una venta. | Los cobros mixtos CF y sus devoluciones necesitan referencia a la venta concreta. |
| Venta Directa crea pedido y venta en llamadas separadas desde Razor. | Incorporar una operación transaccional de backend que incluya cobros e inventario y resista reintentos. |
| Identity autentica cuentas; no está registrado un sistema general de roles. | Conceder explícitamente autorización mínima a cuentas concretas y validarla en servidor. |
| La cancelación actual rechaza unidades entregadas y no implementa devoluciones CF. | Diseñar un proceso específico de solicitud, decisión y efectos efectivos. |
| `UnidadInventario.Costo` es obligatorio; ventas conservan snapshots de costo y la utilidad falla si falta alguno. | Adaptar contratos y cálculos para costo desconocido y resultados parciales. |
| Listados, detalle e imágenes públicas usan la elegibilidad de inventario disponible. | Separar descubrimiento público de lectura segura por enlace directo de un producto agotado. |
| El listado de Productos ya pagina en SQLite; el scanner operativo resuelve códigos externos. | Extender filtros y resolución local conservando cursor, lotes y validación de unidades. |

Mapa técnico: [Clientes](../modules/clientes.md), [Ventas](../modules/ventas.md), [Productos](../modules/productos.md), [Catálogo](../modules/catalogo.md), [Inventario](../modules/inventario.md) y [Scanner operativo](../modules/scanner-operativo-v2-1.md). Las implementaciones principales están en `Services/SaldoConsultas.cs`, `MasterDataServices.cs`, `OperacionServices.cs`, `CatalogoPublicoService.cs` y `SeleccionOperativaService.cs`, dentro de Infrastructure.

## Diseño compartido

El diseño debe conservar las capas actuales: entidades y estados en Domain; contratos y DTOs en Application; consultas, transacciones y persistencia en Infrastructure; presentación y composición en Web. Los componentes Razor recibirán saldos, totales y resultados validados por servicios.

La propuesta financiera es mantener el saldo derivado e incorporar movimientos inmutables para apertura, ajustes y compensaciones, relacionados con sus documentos de origen. Ventas y pagos existentes conservarán su semántica y se integrarán en una lectura común sin duplicar cargos ni cobros. Debe definirse una única regla de contribución al saldo para cada hecho. Una corrección de costo afecta la utilidad; por sí sola no aumenta ni reduce la deuda del cliente.

Para CF se propone admitir ausencia de cliente únicamente en el pedido técnico de una Venta Directa CF, conservar el nombre y apellido opcionales como datos de la venta y vincular cobros directamente a ella. Esta propuesta requiere revisar FK, restricciones, DTOs y todos los consumidores de `Pedido.Cliente`; no se utilizará un cliente genérico compartido.

Las operaciones sensibles usarán una autorización puntual compatible con Identity, concedida explícitamente y denegada cuando falte. El usuario responsable procederá del contexto autenticado. Este trabajo no incluye diseñar tablas, relaciones ni pantallas de un sistema general de roles.

Los comandos financieros tendrán una clave de idempotencia persistente vinculada a la operación y su contenido. Repetir el mismo comando devolverá su resultado anterior; reutilizar la clave con contenido distinto se rechazará. Las restricciones de persistencia y la revalidación transaccional deberán cubrir ejecuciones desde conexiones independientes, además de los controles visuales.

Se distinguirán fecha efectiva y momento de registro. Los importes y redondeos se centralizarán con aritmética decimal y precisión monetaria definida; se usará la fecha de negocio de Guatemala para el corte de apertura. Los hechos históricos sin autor o momento de registro conocido no recibirán datos inventados.

## Paquetes financieros

### F0 Diseño de contratos estados y restricciones

Definir el estado global de apertura y sus filas preparadas, movimientos financieros, vínculos de reversión, comprador CF, cobros por método, solicitud de anulación, devolución efectiva y condición de cada unidad devuelta. Revisar dónde reutilizar transacciones y servicios existentes y cómo evitar transacciones anidadas.

Concretar autorización administrativa, permisos de consulta, restricciones de unicidad, recuperación y precisión monetaria. Determinar las reversiones compatibles: la propuesta inicial limita este mecanismo a ajustes y compensaciones cuya operación inversa preserve saldo no negativo. La apertura confirmada permanece cerrada; sus errores se corrigen con ajustes. Ventas, pagos y devoluciones seguirán su proceso de negocio propio.

**Resultado verificable:** contratos y transiciones definidos, mapa de consumidores afectados y pruebas de aceptación asociadas antes de crear migraciones.

### F1 Base financiera y trazabilidad

Implementar movimientos, auditoría, autorización e idempotencia. Extender la consulta compartida de saldo y usarla en ficha, búsqueda/listado de clientes, pagos, historial, Dashboard y validación de cancelaciones. Al proyectar una cancelación se calculará el saldo resultante con apertura y ajustes incluidos.

Proteger los cambios incompatibles sobre la misma cuenta, incluidas carreras entre pago, ajuste negativo, reversión y cancelación. Los efectos de cada comando se confirmarán juntos. El cierre global de apertura también deberá coordinarse con ventas y pagos concurrentes para impedir actividad que invalide la verificación del lote.

**Resultado verificable:** las distintas vistas y validaciones coinciden; una operación rechazada no deja movimientos parciales ni saldo negativo.

### F2 Migración global única de cartera

Desde Clientes, preparar un único lote mediante captura manual y CSV. La plantilla propuesta es UTF-8, con columnas de ID opcional, teléfono, nombre, apellido, deuda y observación; separador y formato decimal deberán quedar explícitos y cubiertos por pruebas. No interpretar formatos monetarios ambiguos.

La vista previa permitirá resolver coincidencias, crear clientes mediante el servicio existente y corregir errores. El teléfono será obligatorio para clientes registrados. Los saldos Q0 se omitirán y reportarán; negativos, duplicados, ambigüedades y filas pendientes impedirán confirmar. Preparar el lote no aplicará deudas.

Al confirmar, revalidar permisos, clientes, filas, total, ausencia de actividad financiera preexistente y estado global de apertura dentro de la protección transaccional. Aplicar todas las deudas o ninguna, con corte automático del día, hora, autor y referencia de origen. Cerrar definitivamente la apertura y permitir recuperar el resultado de una confirmación cuyo envío se haya repetido.

La ausencia de ventas y abonos es un supuesto informado por el negocio y deberá comprobarse antes de aplicar la migración. Esta comprobación no se sustituye por la existencia de productos cargados.

**Resultado verificable:** Q1,250 de apertura y Q300 de abono posterior producen Q950; una segunda confirmación o actividad previa bloquean una nueva apertura. El resumen tendrá exportaciones XLSX/PDF en la primera entrega.

### F3 Movimientos y Ajustes

Implementar historial global y por cliente con filtros de fecha, tipo, referencia y cliente, paginación y enlaces al origen. Cada entrada mostrará naturaleza, importe, fechas, autor, motivo y vínculos relacionados.

Permitir ajustes positivos y negativos con revisión de saldo previo, cambio y resultado. Revalidar el saldo al confirmar y rechazar resultados inferiores a Q0. Las correcciones y reversiones conservarán el original y tendrán motivo, autor y vínculo; un segundo intento no aplicará otra compensación.

**Resultado verificable:** ajustes de +Q150 y -Q100 cambian la deuda sin registrar ventas, cobros o inventario. Las vistas global y del cliente muestran el mismo historial.

### F4 Venta Directa CF y cobros mixtos

Mostrar únicamente CF, seleccionado inicialmente, y cliente existente. Conservar nombre y apellido opcionales solo en la venta. Cambiar de opción antes de confirmar mantendrá artículos, lotes, cantidades y precios.

Implementar un caso de uso de servidor para crear pedido técnico, venta, detalles, cobros e inventario en una única transacción. Reutilizar las validaciones y la selección de unidades actuales. Adaptar el flujo existente con cliente sin alterar sus abonos globales ni introducir nuevas reglas de crédito.

En CF, los importes aplicados por método sumarán exactamente el total. Registrar efectivo entregado y cambio por separado del importe aplicado; bloquear insuficiencia, cantidades negativas y combinaciones inconsistentes. Los accesos alternativos de backend también impedirán CF con deuda, reservas personalizadas o cobros omitidos.

**Resultado verificable:** Q300 en efectivo y Q200 por transferencia cubren una venta de Q500. Q600 entregados en efectivo implican Q100 de cambio. Un fallo o reintento no duplica pedido, venta, cobros ni consumo de unidades.

### F5 Solicitud y anulación completa CF

Permitir al vendedor solicitar anulación con motivo. Mientras esté pendiente, venta, cobros e inventario continúan vigentes. Solo un administrador podrá aprobar o rechazar, conservando la decisión y su responsable.

Modelar por separado aprobación, dinero realmente devuelto, recepción de productos y finalización. Registrar el reembolso efectivo con importe, método, fecha y autor, aunque el método difiera del cobro original. Una aprobación no dará por ejecutada una transferencia externa. Las operaciones pendientes tendrán recuperación segura e impedirán dobles efectos.

Registrar condición por unidad. Las aptas podrán volver al stock disponible; las no aptas permanecerán fuera del stock vendible mediante una representación explícita por definir. No reutilizar silenciosamente `Perdida`, cuyo significado actual corresponde a pérdidas previas a recepción. Revisar unidades entregadas mediante este proceso específico sin relajar arbitrariamente la cancelación ordinaria.

**Resultado verificable:** rechazo sin efectos; aprobación con trazabilidad de dinero y mercancía; finalización únicamente al completar los efectos exigidos; sin anulaciones parciales ni devolución o reposición duplicadas.

### F6 Reportes y exportaciones de la primera entrega

Entregar XLSX y PDF reales para historial global, estado de cuenta por cliente, resumen de apertura y ventas CF. Compartir consultas filtradas y totales entre pantalla y exportación; exportar todas las filas del conjunto filtrado, con límites explícitos si fuera necesario, sin truncamiento por la página visible.

Identificar periodo, fecha de generación, fecha de corte y referencias. Aplicar permisos, descargas privadas y protección de texto de usuario frente a fórmulas de hoja de cálculo. Seleccionar bibliotecas por licencia, compatibilidad Linux/.NET y consumo de recursos; el proyecto no tiene hoy un generador de reportes reutilizable.

Dashboard y reportes distinguirán ventas, utilidad, cobros, apertura, ajustes, anulaciones y reembolsos. Si se integra el bloque de costos, las exportaciones mostrarán utilidad parcial y revisión por correcciones con las mismas reglas que la pantalla.

**Resultado verificable:** las cuatro vistas producen ambos formatos con filtros y totales coherentes. Las exportaciones forman parte del cierre financiero inicial.

## Paquetes de Productos y Catálogo

### P1 Precios móviles y filtros prioritarios

Ampliar el filtrado en SQLite antes de paginar: acceso visible a «Sin precio asignado» y panel avanzado inicialmente contraído con atributos existentes. Conservar orden, cursores, cancelación de consultas antiguas, carga incremental y reintentos.

Agregar el diálogo «Asignar precio» con fotografía principal grande, detalles de lectura y precio como único dato editable. Guardar con autorización y control de concurrencia. Al completar, actualizar resultados, retirar el producto si deja de cumplir el filtro, cargar continuidad cuando corresponda y conservar filtros, foco y scroll.

En el catálogo y etiquetas, representar la ausencia de precio mediante «Consultar precio» y reutilizar WhatsApp con nombre, código, variante y URL pública. El precio comercial actual procede de `Producto.PrecioSugerido`; la edición rápida no cambiará precios históricos ni costos.

**Resultado verificable:** filtrar cientos de páginas no depende de productos descargados; guardar o cancelar conserva correctamente el contexto móvil.

### P2 Variantes y fichas públicas agotadas

Crear grupos con productos independientes, un grupo máximo por producto y atributos diferenciadores sin duplicar campos maestros existentes. Vincular y desvincular no modificará inventario ni historial. Las variantes disponibles seguirán apareciendo individualmente en listados y búsquedas.

Separar elegibilidad de listados de lectura segura de detalle e imágenes. Una URL directa de un producto agotado existente conservará su ficha y fotos con aviso destacado. Su selector permitirá navegar únicamente a variantes disponibles; agotados no aparecerán en descubrimiento público. Revisar también endpoints de imágenes y caché, que hoy dependen de disponibilidad.

**Resultado verificable:** una URL compartida sigue abriendo al agotarse el producto; el producto desaparece de búsquedas y no expone costos, reservas, proveedores ni rutas privadas.

### P3 Etiquetas y código interno escaneable

Extender el resolver local para código interno y externo con detección de colisiones; conservar el componente scanner y sus controles de disponibilidad. La búsqueda manual y los flujos específicos de Venta Directa, Inventario y Compra seguirán disponibles.

Implementar selección por Producto y cantidades de copias, vista previa, PDF a tamaño real y XLSX por lote. Cada copia usará el mismo `PRO-…` del producto en texto y Code 128; no generará identificadores de unidades. Registrar o advertir desactualización del precio impreso, sin marcar como impresión efectiva una descarga.

El perfil físico será configurable. La impresora NIIMBOT B1 continúa como opción provisional; su app, consumibles, importación y lectura real requieren pruebas. El PDF seguirá siendo una salida independiente del fabricante.

**Resultado verificable:** tres productos con 2/1/4 copias producen siete etiquetas y siete filas imprimibles; las etiquetas se leen con el scanner real y no alteran existencias.

### P4 Costos desconocidos y correcciones históricas

Adaptar Compras, Inventario, snapshots de venta, validaciones y DTOs para distinguir costo conocido de desconocido, sin costos estimados. La ausencia de costo permitirá operar, pero su utilidad quedará no calculable. Sustituir las consultas que hoy rellenan un costo faltante desde la unidad si ese comportamiento pudiera modificar la visión histórica.

La migración de Q0 históricos es independiente de la apertura de cartera. Delimitar los registros declarados por el responsable, comprobar relaciones y ambigüedades y preparar respaldo, registro de alcance y ejecución idempotente. Convertir únicamente la población identificada y verificar después compras, unidades y ventas afectadas; conservar Q0 genuinos y datos ajenos. La ejecución corresponderá a la fase autorizada de implementación.

Permitir notas inmutables de costo real con valor anterior/nuevo, fecha de conocimiento y de registro, autor, motivo y referencias, incluso después de vender. Ofrecer consultas originales y revisadas a una fecha de corte, incluidas varias correcciones sucesivas. El Dashboard distinguirá utilidad calculable, líneas pendientes e importe de ventas afectadas.

**Resultado verificable:** ventas de Q250 con costo Q100 y Q395 sin costo muestran Q150 calculables y Q395 vendidos con costo pendiente. Una nota posterior modifica la revisión correspondiente, conservando la lectura original y las exportaciones anteriores. No se archivarán automáticamente copias oficiales de todos los reportes.

## Dependencias y entregas

| Entrega | Dependencias | Condición de cierre |
| --- | --- | --- |
| Diseño compartido | Requisitos aprobados y revisión de contratos. | Estados, autorización puntual, idempotencia, precisión y límites definidos. |
| Apertura y ajustes | F0 y F1. | F2 y F3 completos con sus XLSX/PDF de F6. |
| CF completo | F0 y F1; coordinación con P4 si ambos se entregan juntos. | F4 y F5, reportes CF, cobros y reembolsos probados. |
| Precios y catálogo | Diseño compartido para acceso sensible. | P1 y P2 con paginación, fotos y enlaces verificados. |
| Etiquetas | Precio comercial definido y resolver local adaptado. | P3 con archivos y prueba física identificada. |
| Costos e informes revisados | Modelo de costo y notas definido antes de cerrar consultas de utilidad. | P4 y adaptación transversal de reportes y F6. |

El orden recomendado para el objetivo financiero es F0, F1, F2, F3, F4, F5 y cierre de F6; sus exportaciones se incorporarán a medida que existan las vistas. P1 puede adelantarse como mejora acotada. P4 debe coordinarse desde el diseño con CF y reportes para evitar una segunda definición de costo y utilidad. El cierre conjunto requiere todos los criterios aprobados de ambos documentos, incluida la prueba física de etiquetas que corresponda.

## Validación y documentación

Las pruebas se apoyarán en los patrones existentes `TestDatabase`, SQLite y `PruebaWebAislada`. Se ampliarán las áreas de Cliente, Venta/Pago, Dashboard, autenticación, scanner, paginación administrativa y catálogo. Las pruebas de concurrencia usarán contextos y conexiones independientes; las de migraciones deberán recorrer migraciones EF reales, además del esquema creado para pruebas unitarias.

La aceptación incluirá fallos antes y después de confirmar, pérdida de respuesta tras una escritura exitosa, doble envío con igual o distinto contenido, dos administradores confirmando apertura, actividad concurrente con apertura, ajustes/pagos que compiten por el saldo, doble reversión, venta de la misma unidad, doble aprobación y recuperación de reembolsos pendientes. Las denegaciones se comprobarán invocando backend directamente.

Al implementar se requerirán compilación, pruebas del área y suite .NET completa por los contratos compartidos. Los cambios JS y de estilos exigirán sus pruebas y regeneración de assets. El QA cubrirá móvil/escritorio, filtros, diálogos, accesibilidad, descargas, scanner óptico y circuito Blazor real, además de impresión física y códigos internos cuando se complete P3. Los datos serán sintéticos en desarrollo y pruebas.

Cada paquete cerrará su estado en documentación canónica, decisiones y planificación afectada, conservando como histórica la evidencia anterior. Los cambios de producto se reflejarán en el changelog al implementarse. Deben reconciliarse especialmente las reglas actuales de saldo, cancelación, costo obligatorio, detalle público disponible, resolución de scanner y robustez de Venta Directa.

Antes del cierre conjunto se recordará expresamente al responsable la revisión de la API pública y del acceso directo a fichas agotadas y variantes, conforme a REG-AC-02. La compatibilidad física de una impresora o navegador solo se dará por validada con evidencia de esa combinación.

## Pendientes técnicos y operativos

CF sin cliente en el pedido técnico, estados NoApta/DevueltaEnRevision, CSV UTF-8, OpenXML/SkiaSharp, autorización por IDs Identity, cortes UTC e idempotencia persistente ya están concretados en código. El sistema no implementa cierres contables de periodos. Quedan la designación de administradores del entorno, perfiles y prueba física de impresión y el análisis del alcance real de Q0 antes de aplicar la migración de costos. La apertura vuelve a verificar actividad financiera al confirmar.

Las reglas comerciales principales ya están aprobadas: apertura única global y atómica, corte automático, sin saldos a favor, CF con pagos mixtos y cambio, aprobación administrativa de anulación, devolución completa real, costos conocidos/desconocidos y exportaciones iniciales. Los casos comerciales adicionales que aparezcan requerirán una decisión específica.

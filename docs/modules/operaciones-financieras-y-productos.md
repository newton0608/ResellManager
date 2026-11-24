# Movimientos financieros CF y operaciones de productos

**Estado:** implementación en rama de trabajo, sin release ni despliegue. La validación y los límites de aceptación se registran al final de esta guía.

Esta ampliación incorpora apertura de cartera, ajustes auditables y venta a Consumidor Final, junto con operaciones de precios, variantes, etiquetas y costos desconocidos. Los contratos funcionales se conservan en [Movimientos y CF](requisitos-movimientos-ajustes-y-consumidor-final.md) y [Productos y Catálogo](requisitos-productos-catalogo-etiquetas-costos.md). El catálogo sigue siendo de lectura y este desarrollo no añade FEL, carrito ni pagos online.

## Autorización y acceso

Las cuentas Identity autenticadas conservan las operaciones comerciales existentes y pueden consultar movimientos, vender, solicitar una anulación y operar productos y etiquetas. Confirmar apertura, ajustar, revertir, decidir una anulación, registrar sus efectos y corregir o migrar costos exige autorización administrativa explícita en backend.

`SeguridadOperaciones:AdministradoresIds` es una lista de identificadores exactos de cuentas Identity existentes, obtenidos por el responsable de administración mediante las herramientas de Identity. En variables de entorno se representa como `SeguridadOperaciones__AdministradoresIds__0`, `__1`, etc. No se concede permiso a la primera cuenta, por correo coincidente ni a todos los usuarios por defecto. La comprobación consulta el usuario existente y la configuración vigente; un ámbito sin petición o circuito autenticado devuelve acceso denegado.

La configuración se proporciona fuera del repositorio. No se incluye un sistema general de roles, permisos configurables o administración de usuarios. Las exportaciones requieren sesión, verifican acceso en servidor y responden sin caché; los identificadores de responsables proceden de la cuenta autenticada, nunca del formulario.

## Apertura de cartera

`/clientes/apertura`, accesible desde Clientes y Movimientos, administra un único borrador global. La captura manual y la importación CSV preparan deudas sin alterar el saldo. Los clientes nuevos se crean al confirmar, dentro de la misma transacción que los movimientos de apertura.

La plantilla es UTF-8 con encabezado `clienteId,telefono,nombre,apellido,deuda,observacion`, separador coma y punto decimal sin separadores de miles. Acepta campos entre comillas y comillas escapadas. El archivo se limita a 1 MB; el lote admite hasta 10,000 filas. La vista previa muestra identidad, deuda, error u omisión por fila, total y páginas de 50 filas.

Se coteja ID cuando se proporciona; en otro caso se busca teléfono normalizado a dígitos. Las coincidencias ambiguas requieren un ID explícito. Nombres coincidentes no fusionan clientes. Se validan teléfono, nombre, importes y longitudes, y se rechazan clientes/teléfonos duplicados en el lote. Q0 se omite; los importes negativos o con más de dos decimales no pueden confirmarse.

La confirmación exige la versión exacta del borrador, filas resueltas, permiso y ausencia de actividad financiera previa. La existencia de ventas, pagos o movimientos bloquea la operación. El corte es el día de confirmación en Guatemala y se conserva la hora UTC, autor, versión del lote y referencia por fila. El saldo inicial se identifica como «Saldo inicial / Deuda anterior a ResellManager».

Apertura, movimientos, clientes nuevos y resultado idempotente se confirman juntos. Una apertura confirmada no puede editarse ni repetirse; una corrección posterior utiliza ajustes. La migración no crea compras, pedidos, ventas, cobros ni inventario. Que el negocio haya informado ausencia de ventas y abonos no sustituye la comprobación del servidor.

## Movimientos ajustes y saldo

`/movimientos` ofrece filtros por cliente, fechas, tipo y referencia y páginas acotadas. El buscador compartido selecciona clientes por nombre o teléfono, tanto en filtros como al crear ajustes. La ficha del cliente incluye movimientos de cuenta y acceso al historial completo. El saldo actual es global y se mantiene independiente del rango mostrado.

La regla compartida es ventas registradas de clientes identificados menos pagos globales, más apertura, ajustes y compensaciones. CF queda fuera de la cartera. Ficha, búsqueda/listado, pagos, Dashboard y cancelación ordinaria utilizan esa fuente. El listado calcula saldos por conjunto de clientes, sin una consulta por cada fila.

Los cargos originales de ventas y sus cancelaciones se muestran como hechos vinculados. Las cancelaciones históricas sin fecha conocida se identifican expresamente; no se inventa su hora o autor. Los movimientos nuevos de apertura, ajuste y reversión conservan importe firmado, fecha efectiva, registro UTC, responsable, motivo y origen.

Un ajuste positivo aumenta deuda; uno negativo la reduce. El backend presenta saldo anterior y resultado antes de confirmar y vuelve a calcularlos al aplicar. No se aceptan saldo a favor ni sobreabonos. El importe tiene precisión de dos decimales y los ajustes no generan ventas, cobros o cambios de stock.

Solo los ajustes pueden revertirse desde este módulo. Se conserva el original, la compensación, motivo y vínculo único; una segunda reversión no produce efecto. La apertura se corrige mediante ajuste; ventas, pagos y devoluciones conservan sus procesos propios. El contexto EF rechaza modificaciones o eliminación de movimientos aplicados y comandos, incluidos los distintos overloads de guardado.

## Venta Directa y Consumidor Final

El comprador se elige entre CF, inicial, y cliente existente. Los datos opcionales de nombre y apellido CF pertenecen a esa venta y no crean cuenta corriente ni clientes. Cambiar comprador conserva artículos, lotes y precios; una selección de cliente recibida por URL se mantiene.

La creación de pedido técnico, venta, snapshots por unidad, cobros e inventario se realiza en un caso de uso transaccional de backend. `Pedido.ClienteId` es opcional exclusivamente para el pedido técnico de Venta Directa CF; los pedidos manuales y otras operaciones requieren cliente. Toda venta mantiene `PedidoId` obligatorio y único.

Los cobros CF pertenecen a la venta concreta. Admiten varios métodos y la suma aplicada debe coincidir con el total. Efectivo entregado y cambio se distinguen del importe aplicado; solo efectivo permite excedente. El cobro insuficiente, los importes negativos y las combinaciones inconsistentes se rechazan en servidor. Las ventas con cliente mantienen sus abonos globales separados.

Se conservan scanner, búsqueda manual, lotes por acción y selección de unidades físicas. El registro revalida unidades disponibles, ausencia de reserva incompatible y ausencia de venta activa. CF no puede cerrarse mediante el servicio ordinario de ventas sin sus cobros, ni cancelarse mediante la cancelación ordinaria.

## Anulación completa CF

La venta permite solicitar una anulación; `/ventas/anulaciones-cf` reúne las últimas solicitudes y enlaza cada venta. La solicitud guarda motivo, fecha y responsable. Pendiente o rechazada no cambia venta, cobros ni inventario. Una solicitud aprobada registra administrador y motivo, sin presumir que el dinero ya se devolvió.

La devolución efectiva guarda importe, método, fecha, hora de registro, referencia y responsable. Puede usar un medio distinto al cobro original; los registros no pueden superar lo pendiente. La anulación completa conserva los cobros originales y la trazabilidad de cada reembolso.

Cada recepción registra unidad, condición, observación, fecha y autor. La unidad recibida queda `DevueltaEnRevision`, fuera de venta. Para finalizar deben estar devueltos todos los importes y recibidos todos los artículos. Entonces la venta queda cancelada, su pedido cancelado y las unidades aptas vuelven a `Disponible`; las no aptas quedan `NoApta`. Ambos estados nuevos son distintos de `Perdida`, cuyo contrato anterior se conserva.

La operación no completa efectos externos: una transferencia bancaria debe haberse efectuado antes de registrarla. Una devolución pendiente puede continuarse sin borrar sus hechos ni repetir dinero o unidades. Esta entrega no habilita anulaciones parciales.

## Concurrencia e idempotencia

Los nuevos comandos usan claves persistentes y huella de operación, contenido y autor. Repetir el mismo comando recupera su resultado; reutilizar la clave con otro contenido se rechaza. Los abonos enviados desde su formulario también conservan su clave durante reintentos.

Las escrituras financieras se revalidan dentro de transacciones SQLite. El cierre de apertura comprueba actividad bajo esa misma protección. No se añade un bloqueo global en memoria. La serialización de escrituras propia de SQLite no equivale a una solución de escalado distribuido.

Estado de unidad, FK de reserva y estados de pedido/venta actúan como tokens EF de concurrencia. Un servicio con una lectura obsoleta no puede reservar o reescribir el estado de una unidad que otra venta consumió. Los conflictos se rechazan y los flujos requieren recargar datos; no se sustituyen unidades ni se reduce silenciosamente la cantidad.

## Precios y variantes

Productos conserva paginación en SQLite. «Sin precio asignado» y los filtros de marca, modelo, color, talla, presentación y rangos ml/g se aplican antes de limitar; el cursor incorpora contexto de filtros. El panel avanzado inicia contraído. Los rangos solo coinciden con artículos que tienen la medida correspondiente.

«Asignar precio» abre el diálogo compartido, con imagen principal grande y datos de lectura. El precio sugerido comercial es su único campo editable. El servidor exige precio positivo, valida el precio previo y registra el cambio con autor y hora. Al guardar se mantiene el contexto; si el producto deja de cumplir «Sin precio asignado», se retira y puede continuar la carga incremental. Cancelar o fallar no modifica el listado como si hubiera éxito.

Los grupos vinculan productos existentes, con un grupo máximo por producto y atributos diferenciadores. Color, talla, presentación y medidas se leen de los maestros, sin duplicarlos en el vínculo; los atributos adicionales se guardan separadamente. La firma del grupo evita combinaciones indistinguibles y se revalida al editar los maestros. Mover a otro grupo exige desvincular explícitamente. Ningún vínculo fusiona fotos, stock, códigos, costos o ventas.

## Lecturas públicas

Los listados y carruseles siguen publicando solo unidades físicamente disponibles, libres de reserva y de venta activa. Cada variante disponible mantiene su tarjeta individual.

El detalle y sus imágenes pueden consultarse por URL directa cuando el producto conserva evidencia de inventario recibido, aunque esté agotado. Los maestros sin unidades recibidas no se hacen públicos por conocer su ID. Las fichas agotadas muestran «Agotado» y no ofrecen contacto que aparente disponibilidad. Se mantienen inaccesibles los datos administrativos y las referencias de almacenamiento.

El selector público muestra las variantes vinculadas, con navegación solo a las disponibles. Las agotadas no se descubren como tarjetas. El detalle incorpora código interno comercial y metadatos mínimos de variantes; no incorpora unidades, reservas, costos, proveedores o teléfonos de clientes.

Q0 o precio no definido se presenta como «Consultar precio». El contacto reutiliza configuración WhatsApp y origen canónico existentes; incluye identificación de producto, variante y enlace. No modifica stock ni crea pedidos.

## Etiquetas

`/productos/etiquetas` prepara lotes por Producto con cantidades de copias. Las copias usan el mismo código interno y no identifican unidades. Se generan XLSX con una fila imprimible por copia y PDF vectorial con una etiqueta por página a tamaño físico. Se conservan nombre, precio y código del lote; un cambio posterior exige generar un lote actualizado.

La generación/descarga no marca impresión. El operador confirma explícitamente la impresión realizada. Descargar o reimprimir no cambia productos ni stock. Los archivos excluyen costos y demás información privada.

El encoder Code 128 B reutiliza las anchuras de símbolos verificadas contra Quagga2, con checksum y zonas silenciosas; el código fuente conserva su licencia MIT. El resolver local acepta código interno y externo, y rechaza coincidencias con varios productos. No se añade decodificador ni consulta externa operativa.

El perfil define ancho, alto, margen y resolución. El PDF rechaza códigos que no quepan con barras de al menos dos puntos de impresora y perfiles sin altura suficiente para nombre, precio, barras y texto. Un código histórico que no admita Code 128 B produce validación, sin una descarga inválida ni un cambio del identificador. Un `PRO-<GUID>` completo puede requerir aproximadamente 117 mm a 203 dpi; el perfil inicial 50 × 30 mm no certifica compatibilidad con esos códigos. La salida no acorta el identificador ni comprime barras para aparentar lectura fiable. NIIMBOT, rollos, importación de XLSX y lectura física permanecen pendientes de comprobación con el dispositivo real.

## Costos originales y revisiones

Compra, unidad y detalle de venta distinguen costo conocido/desconocido mediante `CostoConocido`. El importe original se conserva; Q0 declarado desconocido no participa en el cálculo como costo cero. El costo estimado no es una modalidad admitida. Los costos desconocidos permiten compra, recepción, precio y venta.

Una nota registra costo real, valor previo, fecha de conocimiento, registro UTC, responsable, motivo y referencia. Cuando existen ventas de la unidad, la nota se vincula a sus líneas. Las correcciones no sobrescriben su snapshot original. Una venta posterior toma el costo conocido vigente como su propio snapshot. La nota de costo no modifica la deuda del cliente.

`/costos` consulta notas y utilidad original o revisada a una fecha/hora de corte UTC. Se separan utilidad calculable, líneas sin costo e importe de ventas afectadas; la revisión identifica notas aplicadas. El Dashboard ofrece el estado revisado y distingue inventario de valor parcial. El contrato escalar anterior rechaza presentar una utilidad completa cuando faltan costos.

Los informes XLSX/PDF conservan periodo, generación, corte y versión original/revisada. Una consulta posterior no reescribe archivos ya emitidos ni archiva automáticamente copias oficiales en servidor. No se introduce cierre contable/fiscal de periodos.

La conversión de Q0 históricos utiliza un alcance explícito de unidades, administrador, motivo y clave idempotente, mediante `ICostosService.MigrarHistoricosAsync`. Comprueba costos y referencias, exige las unidades completas de cada detalle de compra y bloquea ambigüedades o correcciones previas. Crea respaldo SQLite privado, verifica `quick_check`, conserva SHA-256 y registra alcance anterior/nuevo; verifica las unidades modificadas antes de confirmar. Los datos ajenos y Q0 genuinos no se convierten por una regla global. Esta migración es independiente de la apertura de cartera; su aplicación sobre datos reales requiere preparar y validar el alcance en el entorno correspondiente.

## Exportaciones y validación

Historial global, estado de cuenta por cliente, apertura y ventas CF incluyen XLSX y PDF en esta entrega. La consulta exportada usa los filtros aplicados y no la página visible. El historial y el reporte CF limitan el conjunto a 100,000 registros; si se excede, solicitan acotar filtros en vez de truncar silenciosamente. Los textos de usuario se escriben como texto de Excel, nunca como fórmulas.

Las pruebas del área cubren apertura, abonos, ajustes, reversión, permisos, doble comando desde conexiones independientes, atomicidad CF, solicitudes/reembolsos/recepciones, costos y cortes históricos, filtros, variantes y formatos de exportación. La regresión recorre migraciones EF reales y conserva los casos históricos como evidencia de sus contratos anteriores, adaptando los que verificaban reglas sustituidas explícitamente.

El [registro de validación](../auditorias/movimientos-ajustes-cf-productos-20261123.md) recoge las pruebas técnicas y comprobaciones responsive de esta implementación. Permanecen como límites operativos la prueba física del scanner con etiquetas nuevas, impresora/NIIMBOT y la ejecución revisada de la migración de costos sobre la población real. Al cerrar se debe revisar expresamente la API pública y los enlaces directos a fichas agotadas, según REG-AC-02.

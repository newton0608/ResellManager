# ResellManager — Requerimientos: Movimientos y Ajustes, Saldos Iniciales y Consumidor Final (CF)

> **Estado:** Requisitos funcionales aprobados, **pendientes de implementación**; diseño técnico por definir.  
> **Proyecto:** ResellManager  
> **Fecha del documento:** 2026-11-07 · decisiones actualizadas 2026-11-07  
> **Alcance de este archivo:** requisitos de negocio y criterios de aceptación; no establece una implementación técnica definitiva.  
> **Control de versiones:** especificación funcional versionada en Git; su inclusión no implica implementación.

## 1. Contexto y objetivo

El negocio ya tiene clientes con deudas y abonos históricos anteriores al uso completo de ResellManager. Reconstruir cada producto vendido y cada abono sería costoso y propenso a errores. Además, algunas ventas directas son de compradores ocasionales a quienes no conviene dar de alta como clientes registrados.

Se requiere resolver **dos capacidades principales**, relacionadas pero independientes:

1. **Gestión de movimientos y ajustes financieros**, incluyendo la **carga de saldos iniciales** por cliente.
2. **Venta Directa a Consumidor Final (CF)**, sin crear un cliente convencional por cada compra ocasional.

ResellManager debe seguir siendo la **fuente oficial de los saldos y movimientos**, conservando trazabilidad. Los mensajes por WhatsApp, si se utilizan, son notificaciones y no sustituyen los registros del sistema.

## 2. Principios y decisiones funcionales aprobadas

- **Una sola migración inicial global**, preparada por carga manual y CSV y **confirmada una única vez** por un administrador. La **fecha de corte es la fecha de esa confirmación**, común para todos los clientes. La aplicación de los saldos es **atómica: todos o ninguno**.
- Según lo informado por el negocio, actualmente en ResellManager hay productos pero no ventas ni abonos registrados; se deberá **verificar este supuesto antes de migrar**. La presencia de movimientos anteriores bloquea la confirmación hasta resolver discrepancias, evitando duplicar la deuda.
- Cada saldo inicial representa la deuda real pendiente, no las compras y abonos históricos. **No genera ventas, pedidos, cobros ni cambios de inventario**.
- **Omitir saldos Q0 y rechazar saldos negativos/a favor** en la primera entrega. El sistema no debe permitir sobreabonos que creen saldo a favor; si una venta se paga con más efectivo del necesario, se calcula el cambio.
- Una vez confirmada y cerrada la migración, no se admite otra carga de apertura. Las correcciones posteriores se harán mediante **Movimientos y Ajustes**, con historial y motivo, sin borrar los hechos originales.
- **Solo cuentas administradoras** podrán confirmar la migración, crear ajustes o revertirlos. Como **todavía no hay un sistema general de roles**, debe implementarse una autorización administrativa **mínima y segura** con Identity, validada en backend y concedida explícitamente a cuentas concretas.
- En **Venta Directa** habrá dos opciones de comprador: **Consumidor Final** (predeterminada) o **seleccionar cliente existente**. Bajo CF se podrán introducir **nombre y apellido opcionales**. Si se dejan vacíos, aparecerá «Consumidor Final (CF)»; si se introducen, quedarán guardados **solo en la venta**, sin crear cliente ni exigir teléfono.
- CF será **exclusivamente de contado**, con **uno o varios métodos de pago** por venta y pago completo obligatorio. Cada medio tendrá un importe propio asociado a la venta concreta; nunca se usará un cliente genérico con pagos globales mezclados.
- Los usuarios autorizados a vender podrán **solicitar una anulación**, pero solamente un administrador podrá **aprobarla o rechazarla**. Mientras esté pendiente, no se modifica la venta, los cobros ni el inventario.
- Una anulación CF aprobada será **completa** (no parcial), con **devolución del dinero realmente registrada** y tratamiento auditado de inventario. Se permitirá devolver por un método **distinto al original**. Las unidades aptas podrán regresar al inventario disponible; las dañadas/no aptas se mantendrán trazables, **sin volver a stock vendible**. La aceptación de una devolución es decisión del negocio.
- La primera implementación incluirá **exportaciones reales a Excel (.xlsx) y PDF** del historial de movimientos, estados de cuenta por cliente, resumen de migración y ventas CF. **CSV** será el formato de **importación inicial**; XLSX importable no es requisito de esta entrega.
- ResellManager seguirá siendo la **fuente oficial** de datos financieros; WhatsApp permanece como canal de notificación.

## 3. Alcance A — Migración inicial única de saldos

### 3.1 Preparación de la migración

**SI-01.** Habilitar una **única migración inicial para todo el negocio**, preparada desde Clientes. El administrador podrá introducir deudas manualmente y/o subir un archivo CSV a una **vista previa** sin aplicar todavía movimientos financieros.

**SI-02.** El CSV utilizará una plantilla accesible con datos suficientes para identificar al cliente (teléfono, nombre y apellido; ID existente si procede), deuda positiva y observaciones opcionales. La plantilla, separador, formato de moneda y codificación se definirán en diseño técnico.

**SI-03.** Clientes registrados continúan requiriendo **teléfono**. Se espera contar con los teléfonos de los deudores; los faltantes deberán resolverse antes de confirmar. No crear clientes incompletos silenciosamente.

**SI-04.** En la preparación se podrá encontrar o crear clientes, revisar importes, identificar coincidencias ambiguas y duplicados y corregirlos. **No fusionar clientes automáticamente** por coincidencia de nombres.

**SI-05.** La vista previa mostrará filas válidas, errores por fila, clientes, importes, omitidos y **total de la cartera a migrar**, sin cambiar saldos reales.

**SI-06.** Solo aceptar deudas **mayores que Q0**. Omitir y reportar saldos Q0; rechazar negativos, importes inválidos o ambiguos y filas no resueltas.

### 3.2 Confirmación y cierre

**SI-07.** Solo un **administrador** puede confirmar la operación. El sistema verificará nuevamente permisos, unicidad de los registros, estado global de migración y ausencia de movimientos que provocarían duplicados.

**SI-08.** La **fecha de corte** será el **día de la confirmación**. Guardar también hora exacta, usuario, identificador del lote y vínculos de cada saldo a su origen. No permitir elegir una fecha retroactiva para esta migración.

**SI-09.** La confirmación será **atómica (todo o nada)**. Ante errores, discrepancias o filas pendientes, no aplicar una parte del lote. El resultado debe ser recuperable sin saldos parcialmente aplicados.

**SI-10.** Proteger la operación con idempotencia persistente contra doble clic, reconexión y reintentos; el mismo lote y la misma migración global no pueden confirmarse dos veces.

**SI-11.** Tras una confirmación exitosa, cerrar **definitivamente** la función de saldos iniciales: no se permiten altas posteriores como «segunda migración». Las correcciones se registran únicamente como **ajustes auditables** autorizados.

**SI-12.** Si se detecta actividad financiera preexistente inesperada (el negocio indica que solo hay productos cargados), **bloquear** el cierre hasta revisar y resolver la discrepancia; no sumar deudas dos veces.

### 3.3 Después de migrar

**SI-13.** Cada deuda se mostrará como **«Saldo inicial / Deuda anterior a ResellManager»**, sin crear pedidos, productos, ventas ni cobros ficticios.

**SI-14.** Los abonos posteriores y las nuevas ventas al crédito modificarán normalmente el saldo; no es necesario reconstruir compras o pagos anteriores a la fecha de corte.

**SI-15.** El lote, resultado, usuario y total podrán **exportarse a Excel (.xlsx) y PDF** desde la primera entrega; las exportaciones no crean ni cambian movimientos.

## 4. Alcance B — Módulo «Movimientos y Ajustes»

### 4.1 Historial financiero

**MA-01.** Pantalla administrativa para consultar movimientos por cliente y de forma global: saldos iniciales, cargos derivados de ventas al crédito, abonos, ajustes, anulaciones/reversiones y otros movimientos que actualmente afecten la cuenta del cliente.

**MA-02.** Filtros mínimos: cliente, rango de fechas, tipo de movimiento y referencia; paginación o carga incremental para evitar consultas masivas innecesarias.

**MA-03.** Cada entrada debe mostrar: identificador/referencia, tipo, fecha efectiva, fecha de registro, importe con signo o naturaleza cargo/abono, cliente cuando aplique, usuario responsable, descripción y relación con el documento/movimiento de origen.

**MA-04.** El historial del cliente y la vista administrativa deberán mostrar resultados coherentes, sin mantener saldos financieros divergentes.

### 4.2 Ajustes controlados

**MA-05.** Permitir crear ajustes **positivos** (aumentan la deuda) y **negativos** (disminuyen la deuda), asociados a un cliente identificable.

**MA-06.** Requerir importe válido, motivo y usuario autorizado. Cuando proceda, exigir referencia al movimiento corregido; permitir adjuntar observación.

**MA-07.** Mostrar el saldo previo, el cambio propuesto y el saldo resultante **antes de confirmar**.

**MA-08.** Los ajustes de cuenta **no son ventas, pagos recibidos ni movimientos de inventario**. Las reglas contables y los reportes deberán distinguirlos claramente.

**MA-09.** No permitir modificaciones silenciosas del importe o motivo de un movimiento aplicado. Para corregirlo, generar un movimiento vinculado y trazable.

**MA-10.** No permitir ajustes que produzcan **saldo negativo/a favor** en esta entrega. Los cambios que reducen deuda se limitan a Q0; no generar anticipos implícitos.

### 4.3 Reversiones y auditoría

**MA-11.** Los usuarios autorizados podrán revertir movimientos compatibles mediante una operación explícita, conservando tanto el original como la reversión y su vínculo.

**MA-12.** No permitir revertir dos veces el mismo movimiento ni dejar saldos inconsistentes por solicitudes concurrentes.

**MA-13.** Registrar en bitácora: usuario, momento, acción, motivo, valor, cliente y referencia afectada. Diferenciar **fecha efectiva** de **fecha real del registro**.

**MA-14.** Los errores de permisos, validación o persistencia no deben dejar movimientos parciales ni alterar saldos de forma incompleta.

**MA-15.** Las reversiones de operaciones con efectos adicionales (por ejemplo, una venta con inventario o un abono vinculado a caja) **deben respetar los flujos existentes**. Este módulo no puede anular esos efectos simplemente con un ajuste de saldo.

### 4.4 Acceso y seguridad

**MA-16.** Solo **administradores expresamente habilitados** podrán confirmar la migración, crear ajustes y revertir movimientos financieros elegibles; los permisos de consulta podrán diferenciarse. No se exige un sistema general de roles, pero sí autorización mínima segura sobre Identity.

**MA-17.** La validación de permisos y reglas críticas ocurrirá en el backend, no solo en la interfaz.

**MA-18.** Evitar cambios duplicados ante reintentos o solicitudes concurrentes; las operaciones financieras deberán ser atómicas.

**MA-19.** **Exportar XLSX y PDF** del historial global y por cliente, con filtros aplicados, fechas, referencias, importes y totales, sin confundir pagos reales con ajustes. La exportación debe respetar permisos y no truncar registros por paginación.

## 5. Alcance C — Consumidor Final (CF) en Venta Directa

### 5.1 Selección de comprador

**CF-01.** Mostrar **solo dos opciones**: «Consumidor Final (CF)», seleccionada inicialmente, y «Seleccionar cliente existente». No introducir un tercer tipo de comprador.

**CF-02.** Bajo CF ofrecer **nombre y apellido opcionales**. Si ambos quedan vacíos, mostrar «Consumidor Final (CF)». Si se informa uno o ambos, almacenar lo escrito como datos **de esa venta**, sin registrar cliente nuevo ni exigir teléfono.

**CF-03.** La identificación ocasional no genera cuenta corriente ni vincula compras futuras por coincidencia de nombre. Cada venta conserva su ID independiente.

**CF-04.** Cambiar entre CF y cliente registrado antes de confirmar no debe borrar productos, lotes del scanner, cantidades o precios ni sobrescribir una selección existente.

**CF-05.** CF no podrá usarse en créditos, reservas personalizadas ni operaciones con saldos pendientes, incluso cuando el comprador haya escrito su nombre.

### 5.2 Cobro y consistencia

**CF-06.** CF es de **pago completo obligatorio**. Admitir un solo medio o **pagos mixtos** (efectivo, transferencia u otros medios habilitados), registrando importe aplicado por método y por venta.

**CF-07.** Los importes aplicados sumarán exactamente el total de la venta. Si se entrega efectivo excedente, calcular **cambio**; el sobrante no crea crédito ni se registra como abono.

**CF-08.** Backend rechazará cobros insuficientes, importes negativos y sumas inconsistentes. Aplicar reglas de precisión monetaria, idempotencia y concurrencia.

**CF-09.** Mantener scanner, lotes y creación automática de pedido técnico. La creación de pedido, venta, cobro e inventario se diseñará como operación **atómica e idempotente**.

**CF-10.** Cada venta conserva artículos, precios, cantidades, fecha, usuario, nombre opcional, referencias y cobros. No usar un cliente CF genérico con pagos globales que mezcle operaciones independientes.

**CF-11.** Las ventas CF cuentan normalmente en ventas y utilidad; no generan cartera anónima ni deuda a cliente CF.

### 5.3 Anulación completa y devolución

**CF-12.** Esta entrega comprende **solo anulación completa** de una venta CF, no devoluciones parciales.

**CF-13.** Un usuario autorizado a vender podrá **solicitar** anulación con motivo, fecha y usuario. Hasta su aprobación, la venta continúa vigente y no se modifica inventario ni se presume dinero devuelto.

**CF-14.** **Solo administrador** podrá aprobar o rechazar la solicitud, con registro de motivo, fecha, responsable y estado. Si se rechaza, no hay efecto financiero ni de inventario.

**CF-15.** La aprobación conserva venta y cobros originales, registra la anulación y exige tratamiento del dinero y productos, sin permitir doble aprobación, doble devolución o restitución duplicada.

**CF-16.** El dinero devuelto tendrá registro de **importe, método efectivamente utilizado, fecha y responsable**, relacionado con la venta; puede devolverse por método **distinto al del cobro original**. Una anulación no equivale automáticamente a una devolución efectuada.

**CF-17.** La recepción de productos debe registrar su condición: **aptos para reventa** podrán volver al inventario disponible; **no aptos o dañados** quedarán trazados fuera del stock vendible. La decisión comercial de aceptar/rechazar una devolución corresponde al negocio.

**CF-18.** La implementación distinguirá estados pendientes, aprobados, rechazados y confirmación de efectos efectivos. Ante errores externos (dinero no devuelto, producto no recibido), no marcar como completado lo que todavía no ocurrió; definir recuperación segura en diseño técnico.

**CF-19.** No usar ajustes de saldo como sustituto de la anulación: ésta debe preservar efectos propios de caja/cobros e inventario.

### 5.4 Informes y fiscalidad

**CF-20.** Exportar **XLSX y PDF** del reporte de ventas CF desde la primera entrega, con filtros, fecha, número de venta, comprador mostrado, medios de cobro, importes y estado de anulación.

**CF-21.** La etiqueta «CF» no implica facturación electrónica o FEL; este desarrollo no incorpora emisión fiscal automática.

**CF-22.** Permisos, reintentos, trazabilidad y validaciones críticas deben verificarse en backend y mantenerse consistentes con venta, pedido, cobro e inventario.

## 6. Integración con módulos existentes

| Módulo / área | Comportamiento esperado |
| --- | --- |
| **Clientes** | Preparación de la única migración, teléfono obligatorio, estado de cuenta coherente con saldo inicial, abonos y ajustes. |
| **Ventas y Venta Directa** | CF predeterminado con nombre y apellido opcionales, alternativa de cliente registrado, cobro completo y mixto, anulación completa con aprobación. |
| **Abonos / Pagos** | Abonos de clientes registrados disminuyen deuda sin excedente a favor; cobros CF vinculados a cada venta y diferenciados por medio. |
| **Inventario / Pedidos / Scanner** | Pedidos técnicos y lotes sin regresiones; venta/anulación mantienen integridad, productos no aptos no vuelven a stock disponible. |
| **Seguridad / Identity** | Permiso administrativo mínimo explícito, sin sistema de roles completo; solicitantes de anulación y aprobadores diferenciados. |
| **Dashboard / Reportes** | Separar ventas, utilidad, cobros, cartera inicial, ajustes, anulaciones y devoluciones; evitar dobles registros. |
| **Exportaciones** | **Excel (.xlsx) y PDF** de historial, estado de cuenta por cliente, resumen de migración y ventas CF; aplicar filtros y respetar permisos. |
| **Bitácora** | Autor, fecha real y efectiva, motivo, estado y referencias de cambios y decisiones. |
| **Portal de clientes (futuro)** | Mostrar saldos y abonos reales sin inventar ventas antiguas. |
| **WhatsApp (actual/futuro)** | Solo notificaciones, nunca fuente oficial del saldo. |

## 7. Reglas de consistencia y modelo conceptual

Estas son **necesidades lógicas**, no nombres obligatorios de entidades ni diseño de base de datos.

- Todo movimiento que afecte una cuenta identificable debe tener **importe, naturaleza, fecha, origen y trazabilidad**.
- Los saldos derivados y el historial deben concordar. Si existe un saldo almacenado por rendimiento, debe reconciliarse con el historial y actualizarse transaccionalmente.
- Una **venta** y un **ajuste** son hechos distintos y deben tener semánticas distintas en reportes.
- Un **pago real** y una **corrección de deuda** son hechos distintos.
- Un movimiento aplicado no debe borrarse de forma que desaparezca la evidencia financiera.
- Deben evitarse duplicados de importación, reintentos y reversiones.
- La migración inicial es **única, global y atómica**, con corte automático al confirmar. No hay nuevas aperturas después del cierre.
- La solicitud de anulación no cambia aún el estado económico; se diferencia de la aprobación, de la devolución real del dinero y de la recepción/condición de los productos.
- Los cobros CF permiten varios métodos sin cartera pendiente; los importes aplicados equivalen al total y el efectivo excedente es cambio.
- Las exportaciones **XLSX/PDF** deben representar el conjunto filtrado completo, totales consistentes y controles de permisos.
- Antes de añadir entidades/tablas, comprobar si existe una bitácora, libro de movimientos o infraestructura de transacciones reutilizable.

### Ejemplo de saldo inicial + actividad nueva

| Evento | Cambio en deuda | Saldo |
| --- | ---: | ---: |
| Saldo inicial al corte | +Q1,250.00 | Q1,250.00 |
| Abono posterior | -Q300.00 | Q950.00 |
| Nueva compra al crédito | +Q175.00 | Q1,125.00 |
| Segundo abono | -Q200.00 | **Q925.00** |

Un ajuste que aumente el saldo Q150.00 tampoco significa que hubo una nueva venta o entrada de caja.

## 8. Criterios de aceptación (escenarios mínimos)

1. **Migración global:** capturar o importar CSV para todos los clientes, revisar filas y totales, y confirmar una sola vez con corte automático del día y responsable.
2. **Atomicidad:** ante un error de validación, persistencia o reconexión no se aplica parte del lote; doble confirmación no genera saldos duplicados.
3. **Cierre:** una migración confirmada bloquea una segunda. Una corrección posterior solo se hace con ajuste autorizado y trazado.
4. **Saldos:** Q1,250 de deuda inicial generan Q1,250 sin ventas ficticias; un abono posterior Q300 produce Q950.
5. **Datos erróneos:** CSV con duplicados, teléfonos faltantes, coincidencias ambiguas o valores inválidos impide confirmar; Q0 se omite y los negativos se rechazan.
6. **Actividad previa:** al detectar ventas/pagos ya registrados antes de la migración, se bloquea la confirmación y no se duplica la deuda.
7. **Ajustes:** cargo +Q150 o descuento -Q100 refleja saldos correctos y auditoría, sin crear ventas ni ingresos; ajuste que produciría saldo negativo se rechaza.
8. **Reversión:** conserva original, responsable y motivo; un segundo intento no produce efectos adicionales.
9. **Permisos:** usuarios sin permiso administrativo no pueden confirmar migración ni ajustar o revertir, incluso mediante backend directo; los permisos no se asignan a todos por defecto.
10. **CF predeterminado:** sin nombre/apellido queda CF. Con nombre y/o apellido, el dato aparece en esa venta sin crear cliente o requerir teléfono.
11. **Cliente existente:** cambiar de CF a cliente registrado preserva scanner, lotes, cantidades y precios.
12. **Pagos mixtos:** venta Q500 con Q300 efectivo y Q200 transferencia registra dos importes asociados a la venta y Q0 pendiente.
13. **Cambio:** Q600 entregados en efectivo para Q500 implican Q100 de cambio, sin saldo a favor ni abono; menos de Q500 impide cerrar CF.
14. **Solicitud pendiente:** vendedor solicita anulación; hasta que el administrador decida, venta/cobro/inventario permanecen vigentes.
15. **Rechazo:** solicitud rechazada queda auditada sin efectos operativos.
16. **Aprobación y devolución:** administrador anula venta completa; se registra dinero realmente devuelto, incluso por otro medio, y se impiden dobles efectos.
17. **Inventario:** producto apto regresa a disponible y no apto queda fuera de stock vendible, ambos trazables.
18. **Exportaciones:** historial global, estado de cuenta por cliente, migración y ventas CF generan **XLSX y PDF** con filtros, importes, totales, fechas y estados correctos.
19. **Integridad:** fallos de red, concurrencia o reintentos no duplican pedido, venta, cobro, devolución ni inventario.
20. **Regresión:** dashboard y reportes no confunden apertura/ajuste con venta/cobro; scanner, Pedidos, Clientes y Ventas siguen funcionando.

## 9. Fuera del alcance de esta propuesta

- Reconstruir compras y abonos históricos anteriores a la apertura.
- **Importar XLSX** en esta primera entrega: la importación inicial será **CSV** (aunque **sí se exportará a Excel .xlsx y PDF** desde el inicio).
- Segundas migraciones de apertura, saldos a favor, crédito bajo CF y devoluciones parciales.
- Sistema general de roles (excepto autorización administrativa mínima sobre Identity).
- WhatsApp automatizado, portal público de estado de cuenta, FEL y contabilidad/caja completa.
- Rediseños fuera de las integraciones financieras indispensables.

## 10. Decisiones cerradas y pendientes de diseño técnico

### 10.1 Funcionalmente aprobadas

Quedan aprobadas las reglas descritas en **§2–§9**: una migración única con CSV y corte automático, sin saldo a favor; ajustes auditados solo por administradores; CF predeterminado con datos opcionales; cobro completo/mixto con cambio; solicitud de anulación por vendedor y aprobación administrativa; devolución completa real y clasificación apto/no apto; exportación XLSX/PDF en la primera entrega.

### 10.2 Definir durante el diseño técnico (sin cambiar silenciosamente reglas comerciales)

1. Representación de CF en persistencia, relaciones de Pedido/Venta, cobro asociado a cada venta y control transaccional del scanner.
2. Entidades/estados de lote único, movimiento de apertura, ajustes, reversiones elegibles, solicitud y anulación de venta, devolución efectiva y condición de inventario.
3. Mecanismo mínimo seguro para designar cuentas administradoras sobre Identity; permisos de consulta separados y controles de backend, sin implementar aún roles generales.
4. Plantilla CSV exacta, conciliación y manejo de coincidencias de clientes; bloqueos ante actividad financiera preexistente.
5. Reportes y archivos XLSX/PDF: campos, formato, bibliotecas, filtros, rendimiento, seguridad y modo de descarga.
6. Reglas de precisión/redondeo, fecha efectiva frente a fecha de registro y recuperación de efectos externos que no puedan ejecutarse en una sola transacción (p. ej. reembolso bancario).
7. Tipos de reversión permitidos desde Movimientos y Ajustes frente a casos que deben usar su propio proceso de negocio.
8. Ubicación definitiva de pantallas y etiquetas, sin introducir nuevas modalidades de comprador o reglas de crédito.

Si aparece un caso comercial no cubierto, **documentarlo y pedir una decisión antes de implementarlo**, sin tratar las propuestas técnicas como políticas ya aprobadas.

## 11. Orden de implementación sugerido

1. **Diseño técnico primero:** revisar AGENTS.md, código y pruebas actuales de saldos, pagos, Identity, inventario, pedidos, scanner y reportes; aprobar entidades, estados, permisos, transacciones y casos de error antes de migraciones.
2. **Base financiera + migración única:** unificar saldos, preparar carga manual/CSV, validar y confirmar atómicamente con corte único y cierre definitivo.
3. **Movimientos y Ajustes:** historial global, ajustes, reversión elegible, auditoría y permisos administrativos.
4. **CF en Venta Directa:** selección con nombre opcional, cliente existente, pagos mixtos, cambio y operación atómica de venta.
5. **Anulaciones CF:** solicitud, aprobación/rechazo, devolución real y retorno de inventario apto/no apto.
6. **Exportación XLSX y PDF:** de las cuatro vistas definidas; incluirla en la **primera entrega**, no dejarla como mejora posterior.
7. **Validación y documentación:** build, suite completa, tests de concurrencia, permisos, idempotencia, datos sintéticos, QA móvil y scanner; conciliar documentación del módulo, decisiones y pendientes cuando el código se implemente.

---

**Nota para futuros agentes:** este documento describe requisitos y decisiones de producto. No asumir que ya están implementados, no cambiar reglas de negocio no especificadas, no alterar datos reales sin respaldo ni realizar cambios operativos ajenos al alcance de una implementación autorizada.
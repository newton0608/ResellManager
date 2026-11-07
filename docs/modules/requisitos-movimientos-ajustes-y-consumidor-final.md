# ResellManager — Requerimientos: Movimientos y Ajustes, Saldos Iniciales y Consumidor Final (CF)

> **Estado:** Propuesta funcional para revisión, **no implementada** mediante este documento.  
> **Proyecto:** ResellManager  
> **Fecha del documento:** 2026-11-07  
> **Alcance de este archivo:** requisitos de negocio y criterios de aceptación; no establece una implementación técnica definitiva.  
> **Control de versiones:** especificación funcional versionada en Git; su inclusión no implica implementación.

## 1. Contexto y objetivo

El negocio ya tiene clientes con deudas y abonos históricos anteriores al uso completo de ResellManager. Reconstruir cada producto vendido y cada abono sería costoso y propenso a errores. Además, algunas ventas directas son de compradores ocasionales a quienes no conviene dar de alta como clientes registrados.

Se requiere resolver **dos capacidades principales**, relacionadas pero independientes:

1. **Gestión de movimientos y ajustes financieros**, incluyendo la **carga de saldos iniciales** por cliente.
2. **Venta Directa a Consumidor Final (CF)**, sin crear un cliente convencional por cada compra ocasional.

ResellManager debe seguir siendo la **fuente oficial de los saldos y movimientos**, conservando trazabilidad. Los mensajes por WhatsApp, si se utilizan, son notificaciones y no sustituyen los registros del sistema.

## 2. Principios y decisiones funcionales acordadas

- Los **saldos iniciales** representan la deuda pendiente a una **fecha de corte**, no la reconstrucción de compras y pagos previos.
- Registrar un saldo inicial o un ajuste de deuda **no debe crear ventas ficticias, cambiar existencias ni contabilizar dinero recibido**.
- Se creará un módulo administrativo **«Movimientos y Ajustes»** para consulta, correcciones controladas, reversiones y auditoría.
- La captura/importación de saldos iniciales se ofrecerá preferentemente desde **Clientes**, pero generará movimientos visibles en el historial financiero central.
- Los movimientos originales deben conservarse. Los cambios relevantes se registrarán mediante **movimientos compensatorios o reversiones auditables**, no mediante edición silenciosa del historial.
- **Consumidor Final (CF)** será una opción para **Venta Directa**, preferiblemente preseleccionada, que no exigirá registrar nombre, teléfono ni datos inventados.
- Las operaciones CF serán **de contado/pago completo**. No se habilitarán crédito ni abonos pendientes asociados a un CF genérico.
- Cada venta CF debe conservar sus propios productos, cantidades, importes, medio(s) de pago y fecha; todas las ventas CF **no se deben fusionar en una única operación**.
- Antes de decidir tablas, entidades o relaciones, revisar la arquitectura y los contratos existentes del repositorio, así como `AGENTS.md` y la documentación por módulos.

## 3. Alcance A — Saldos iniciales de clientes

### 3.1 Registro individual

**SI-01.** Desde la ficha de un cliente existente, un usuario autorizado podrá registrar un **saldo inicial pendiente**.

**SI-02.** Datos mínimos: cliente, importe, fecha de corte y usuario que efectúa el registro. Observación o referencia del origen: opcional, pero recomendada.

**SI-03.** El saldo inicial se mostrará en el estado de cuenta con una etiqueta inequívoca, por ejemplo **«Saldo inicial / Deuda anterior a ResellManager»**.

**SI-04.** La operación no creará productos, pedidos, ventas ni entradas/salidas de caja.

**SI-05.** El saldo inicial se tomará en cuenta en el saldo pendiente del cliente a partir de la fecha de corte definida, evitando sumar dos veces deudas representadas ya por movimientos del sistema.

**SI-06.** Si el cliente ya tiene movimientos o un saldo inicial, el sistema advertirá del riesgo de duplicidad y exigirá un tratamiento explícito; no se sumará un segundo saldo inicial automáticamente.

### 3.2 Importación masiva

**SI-07.** Permitir importar saldos mediante archivo **CSV o XLSX** (formato final sujeto a las capacidades existentes del sistema). Como mínimo: identificador de cliente o datos suficientes para localizarlo, importe y fecha de corte; observaciones opcionales.

**SI-08.** Ofrecer una **vista previa** antes de confirmar: cliente identificado, saldo, filas válidas, errores, duplicados y total agregado.

**SI-09.** Identificar posibles clientes existentes por sus identificadores disponibles; **no fusionar automáticamente** coincidencias ambiguas. Permitir resolver coincidencias y crear clientes faltantes desde el flujo si corresponde.

**SI-10.** Rechazar importes inválidos y filas ambiguas; señalar errores con número de fila. La política sobre saldos en cero o a favor del cliente deberá definirse antes de implementar.

**SI-11.** Evitar doble aplicación del mismo archivo/lote por reintentos, doble clic o importaciones repetidas (mecanismo de idempotencia/validación de duplicados).

**SI-12.** Registrar quién importó, fecha, lote y resultado. Conservar la trazabilidad entre cada saldo generado y la importación de origen.

### 3.3 Operaciones posteriores

**SI-13.** Abonos, nuevas ventas al crédito y otros movimientos válidos modificarán el saldo del cliente normalmente **después de la migración**.

**SI-14.** Los movimientos previos a la fecha de corte **no se reconstruirán** solo para justificar el saldo inicial.

**SI-15.** Una deuda mal importada se corregirá por el mecanismo autorizado de **Movimientos y Ajustes**, sin borrar su registro original.

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

**MA-10.** Definir y validar el tratamiento de ajustes que produzcan **saldo a favor** o saldo negativo antes de habilitarlos (ver decisiones pendientes).

### 4.3 Reversiones y auditoría

**MA-11.** Los usuarios autorizados podrán revertir movimientos compatibles mediante una operación explícita, conservando tanto el original como la reversión y su vínculo.

**MA-12.** No permitir revertir dos veces el mismo movimiento ni dejar saldos inconsistentes por solicitudes concurrentes.

**MA-13.** Registrar en bitácora: usuario, momento, acción, motivo, valor, cliente y referencia afectada. Diferenciar **fecha efectiva** de **fecha real del registro**.

**MA-14.** Los errores de permisos, validación o persistencia no deben dejar movimientos parciales ni alterar saldos de forma incompleta.

**MA-15.** Las reversiones de operaciones con efectos adicionales (por ejemplo, una venta con inventario o un abono vinculado a caja) **deben respetar los flujos existentes**. Este módulo no puede anular esos efectos simplemente con un ajuste de saldo.

### 4.4 Acceso y seguridad

**MA-16.** Solo roles autorizados podrán crear/importar saldos, ajustar o revertir. Los permisos de consulta podrán separarse de los de modificación.

**MA-17.** La validación de permisos y reglas críticas ocurrirá en el backend, no solo en la interfaz.

**MA-18.** Evitar cambios duplicados ante reintentos o solicitudes concurrentes; las operaciones financieras deberán ser atómicas.

## 5. Alcance C — Consumidor Final (CF) en Venta Directa

### 5.1 Flujo de venta

**CF-01.** En **Venta Directa**, el campo Cliente ofrecerá **«Consumidor Final (CF)»** además de buscar/seleccionar un cliente existente.

**CF-02.** CF será la opción predeterminada propuesta, siempre que sea compatible con el flujo actual y no sobrescriba una selección ya realizada.

**CF-03.** El sistema permitirá concluir una venta CF **sin crear antes un cliente registrado** y sin exigir nombre, teléfono o identificadores ficticios.

**CF-04.** El operador podrá cambiar de CF a cliente existente y viceversa antes de confirmar, sin perder indebidamente el detalle de los productos escaneados.

**CF-05.** La operación será de **pago completo**. El importe pagado deberá cubrir el total conforme a las reglas actuales de cobro; no se permitirá registrar deuda, venta al crédito o abono futuro bajo CF.

**CF-06.** Mantener compatibilidad con el **scanner** y el flujo real de Venta Directa, incluyendo la creación automática del pedido técnico y la venta cuando corresponda en la arquitectura actual.

**CF-07.** Registrar por cada venta CF: identificador único, productos/cantidades, precios e importes aplicados, fecha, usuario, medio(s) de pago permitidos y referencias de documentos generados.

**CF-08.** Incluir ventas CF en los reportes y totales de ventas normales, sin generar un estado de cuenta o deuda individual para un consumidor anónimo.

**CF-09.** En historial, comprobantes y reportes, identificar la operación como **«Consumidor Final»**, diferenciando cada venta por su identificador y no por un cliente ficticio compartido.

### 5.2 Restricciones y casos límite

**CF-10.** El backend rechazará intentos de guardar ventas CF con saldo pendiente, aun si se manipula la petición o falla la validación del frontend.

**CF-11.** CF **no será seleccionable** en flujos que requieran identificación del deudor, reserva personalizada, historial de abonos o compromisos de pago, salvo definición posterior explícita.

**CF-12.** Una devolución, cancelación o corrección de una venta CF conservará la referencia a la venta original y seguirá las reglas actuales de inventario/caja; no se resolverá mediante ajustes genéricos de deuda.

**CF-13.** Decidir la representación técnica más segura después de revisar el modelo existente: **cliente especial interno** frente a **venta sin `ClienteId`** (u otra alternativa compatible). No asumir todavía ninguna de ellas como decisión cerrada.

**CF-14.** Distinguir esta opción operativa de los requisitos fiscales/tributarios de facturación o FEL: el texto «CF» no debe interpretarse por sí solo como cumplimiento de emisión fiscal; integrar las reglas fiscales vigentes cuando corresponda.

## 6. Integración con módulos existentes

| Módulo / área | Comportamiento esperado |
| --- | --- |
| **Clientes** | Captura individual y masiva de saldos iniciales; estado de cuenta con origen de cada movimiento. |
| **Ventas y Venta Directa** | Crédito solo con cliente identificable; ventas CF de pago completo; preservar flujo actual del scanner. |
| **Abonos / Pagos** | Abonos reales disminuyen deuda; no se confunden con ajustes que solo modifican el saldo contable. |
| **Inventario / Pedidos** | Saldos iniciales y ajustes no alteran existencias ni crean pedidos. Ventas CF siguen las reglas normales. |
| **Dashboard / Reportes** | Distinguir ventas, cobros, cartera inicial y ajustes para evitar inflar indicadores de ventas o ingresos. |
| **Bitácora** | ResellManager conserva el registro oficial de movimientos y autoría. |
| **Portal de clientes (futuro)** | Mostrar saldo inicial, abonos y movimientos propios, sin inventar artículos de compras antiguas. |
| **WhatsApp (actual/futuro)** | Solo canal de aviso o resumen; no es el libro mayor ni origen oficial del saldo. |

## 7. Reglas de consistencia y modelo conceptual

Estas son **necesidades lógicas**, no nombres obligatorios de entidades ni diseño de base de datos.

- Todo movimiento que afecte una cuenta identificable debe tener **importe, naturaleza, fecha, origen y trazabilidad**.
- Los saldos derivados y el historial deben concordar. Si existe un saldo almacenado por rendimiento, debe reconciliarse con el historial y actualizarse transaccionalmente.
- Una **venta** y un **ajuste** son hechos distintos y deben tener semánticas distintas en reportes.
- Un **pago real** y una **corrección de deuda** son hechos distintos.
- Un movimiento aplicado no debe borrarse de forma que desaparezca la evidencia financiera.
- Deben evitarse duplicados de importación, reintentos y reversiones.
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

1. **Migración individual:** dado un cliente previo con Q1,250 de deuda real, al registrar el saldo inicial, el estado de cuenta muestra Q1,250; no aparecen ventas ni productos ficticios.
2. **Abono posterior:** sobre ese saldo, al registrar un abono real de Q300, la deuda se actualiza a Q950 y ambos movimientos conservan tipos y referencias distintos.
3. **Importación:** un archivo con clientes válidos, duplicados y filas con error presenta vista previa; no aplica importes erróneos ni repite lotes accidentalmente.
4. **Ajuste positivo:** un usuario autorizado registra un ajuste de +Q150 con motivo y ve el saldo correcto, sin alterar inventario ni ingresos por ventas.
5. **Ajuste negativo:** una corrección de -Q100 queda reflejada y auditada; no se presenta falsamente como abono recibido.
6. **Reversión:** una reversión válida conserva el original, su motivo y su vínculo, y un segundo intento no produce efecto duplicado.
7. **Permisos:** un usuario sin permiso puede ser rechazado por backend aunque invoque la API directamente.
8. **Venta CF exitosa:** seleccionar CF, escanear productos, cobrar el total y guardar registra una venta completa, sin crear un cliente nuevo ni saldo a cobrar.
9. **Venta CF con deuda:** intentar cerrar una venta CF sin pago completo es rechazado, tanto desde UI como desde API.
10. **Reportes:** las ventas CF suman como ventas normales; los saldos iniciales y ajustes no inflan ventas ni cobros.
11. **Historial:** las consultas de un cliente muestran saldo inicial, abonos y ajustes con orden, fecha, usuario y referencias comprensibles.
12. **Integridad:** ante fallos de red o doble envío, no se duplican cargos, importaciones o ventas.

## 9. Fuera del alcance de esta propuesta

- Reconstrucción de cada venta y abono histórico previo al saldo inicial.
- Automatización de WhatsApp o implementación del portal público de estados de cuenta.
- Implementación completa de facturación electrónica o nuevos medios de pago.
- Contabilidad general completa o caja/bancos desde cero.
- Rediseño integral de Ventas, Pedidos, Inventario y Clientes más allá de las integraciones necesarias.

## 10. Decisiones pendientes antes de programar

1. **Migración:** ¿qué reglas aplicar cuando el cliente ya registra actividad después de la fecha de corte? Definir validación, bloqueo o procedimiento de reconciliación para evitar duplicidad.
2. **Importación:** formato inicial (CSV, XLSX o ambos), plantilla exacta y resolución de coincidencias entre clientes.
3. **Saldos especiales:** permitir solo deuda positiva o también saldos cero/a favor; y qué hacer si un ajuste vuelve el saldo negativo.
4. **Permisos:** roles específicos que podrán importar, ajustar, revertir y consultar auditoría.
5. **Reversiones:** qué tipos de movimientos podrán revertirse desde este módulo y cuáles deberán usar su propio flujo de negocio.
6. **CF y base de datos:** elegir cliente interno especial, `ClienteId` opcional u otra opción según el esquema actual y sus restricciones.
7. **Cobros CF:** determinar si el pago completo puede dividirse entre varios métodos y cómo tratar redondeos/cambio.
8. **Fechas y periodos:** decidir si se permiten registros retroactivos y cómo tratar periodos cerrados.
9. **Presentación:** ubicación definitiva de las pantallas, etiquetas, permisos e integración en el menú existente.

## 11. Orden de implementación sugerido

1. **Auditoría del sistema actual:** leer `AGENTS.md`, arquitectura, módulos de Clientes, Ventas, Abonos, Scanner, Pedidos e Inventario; identificar entidades y reglas existentes.
2. **Diseño de movimientos:** definir contratos, integridad, tipos de movimiento, permisos, bitácora y pruebas.
3. **Saldos iniciales:** captura individual, vista en historial y después importación masiva con vista previa/idempotencia.
4. **Movimientos y Ajustes:** consulta, ajustes autorizados y reversiones compatibles.
5. **Consumidor Final en Venta Directa:** flujo UI/API, validación de pago completo, compatibilidad con scanner/pedido técnico y reportes.
6. **Pruebas integrales:** verificar consistencia financiera, inventario, reportes, permisos y no regresiones; documentar los cambios antes de desplegar.

---

**Nota para futuros agentes:** este documento describe requisitos y decisiones de producto. No asumir que ya están implementados, no cambiar reglas de negocio no especificadas, no alterar datos reales sin respaldo ni realizar cambios operativos ajenos al alcance de una implementación autorizada.
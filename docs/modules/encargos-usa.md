# Módulo futuro: Encargos de clientes en EE. UU.

> **Estado: análisis funcional y decisiones de negocio acordadas; NO implementado.**
> Documento canónico para continuar el diseño sin perder lo conversado entre el 09 y el 12/10/2026. No autoriza desarrollo, migraciones, cambios de flujos vigentes ni despliegue. Separa deliberadamente **acuerdos confirmados**, **propuestas de diseño** y **preguntas abiertas**.
>
> **Alcance:** únicamente el módulo **Encargos**. No describe ni cambia la API temporal de importación de productos, el catálogo público ni los pedidos/reservas implementados.

## 1. Problema real que queremos resolver

La familia recibe solicitudes de clientes en Guatemala que desean artículos de Estados Unidos. La madre está en EE. UU. y puede buscarlos o comprarlos; el usuario y su hermana atienden a los clientes y reciben solicitudes, habitualmente por **WhatsApp**. Una solicitud inicial no equivale a una compra: el artículo puede no estar disponible, tener otra talla/color, encontrarse a un precio distinto o requerir aprobación del cliente.

Actualmente esa coordinación depende de mensajes dispersos. Se necesita una única fuente de información, actualizada **en tiempo real**, para saber **qué pidió cada cliente**, **qué artículos hay que buscar**, **qué encontró la madre**, **cuál es el precio ofrecido**, **qué confirmó el cliente**, **qué se compró**, **qué anticipos se recibieron** y **qué falta entregar/cobrar**, sin confundir estos encargos con mercancía adquirida para stock.

**Objetivo:** transformar conversaciones y búsquedas en un proceso verificable, con decisiones explícitas y trazabilidad por cliente y por artículo.

### Personas y responsabilidades acordadas

| Actor | Responsabilidad |
| --- | --- |
| Usuario | Recibir solicitudes por WhatsApp, registrarlas, contactar al cliente, confirmar decisiones y consultar avance. |
| Hermana | Mismo papel de recepción/registro y coordinación con clientes; debe compartir la misma información, sin bandejas aisladas. |
| Madre (compradora en EE. UU.) | **Ver únicamente solicitudes ya confirmadas para búsqueda**, buscar artículos, comunicar disponibilidad/alternativas, **determinar el precio cuando encuentre el producto**, decidir si requiere anticipo según la cantidad y comprar cuando corresponda. |
| Cliente | Solicitar producto y variantes; decidir si acepta precio/alternativa antes de autorizar la compra; pagar anticipo cuando se solicite. |
| Chatbot (posible canal futuro) | Puede **registrar solicitudes** en el mismo flujo, pero **no debe eludir la confirmación humana** ni comprar o prometer precio/disponibilidad por sí mismo. Su integración no está implementada ni definida. |

**Acuerdo central:** una solicitud recién recibida no debe aparecer automáticamente como trabajo confirmado en la bandeja de la madre. El usuario, su hermana o el flujo de confirmación autorizado la revisan primero.

## 2. Principios de negocio confirmados

1. **Encargo no es inventario.** Una solicitud de cliente no crea una `UnidadInventario`, no implica existencias, no publica disponibilidad ni genera una venta por sí misma.
2. **Encargo no es compra.** Estar «confirmado para buscar» significa que la madre puede buscarlo, **no** que el cliente haya aprobado un precio ni que la madre ya lo haya comprado.
3. **Precio posterior a la búsqueda.** La madre determina/comunica el precio una vez localizado el artículo. No se debe inventar precio definitivo al capturar el mensaje inicial.
4. **Confirmación del cliente antes de comprar.** Cuando se presenta el precio o una alternativa, debe constar la aceptación del cliente antes de ejecutar la compra por encargo.
5. **Anticipo opcional y contextual.** La madre decide si se requiere anticipo **según la cantidad y las circunstancias**. No existe porcentaje, monto fijo ni obligación universal aprobada.
6. **Trabajo sincronizado en tiempo real.** Madre, usuario y hermana necesitan ver cambios sin depender de reenviar mensajes manualmente; notificaciones y conflictos de edición requieren diseño específico.
7. **Agrupación sin fusión.** Si varias personas piden el mismo artículo/variante, la madre debería verlo agrupado para buscar o comprar cantidades de manera eficiente, **pero cada cliente conserva su propia solicitud, cantidad, cotización, aceptación, anticipo y asignación**.
8. **Separación del stock.** Las compras hechas para clientes deben identificarse como tales; las compras para inventario propio siguen un flujo distinto. **Pueden compartir una misma caja/envío** sin que ello mezcle propiedad, destino, costo o saldo.
9. **Trazabilidad.** Mantener historial de quién registró, confirmó, cotizó, aceptó, compró y entregó; no convertir automáticamente un mensaje informal en un hecho financiero.
10. **No reutilizar ciegamente el módulo `Pedido` vigente.** Hoy `Pedido`, `DetallePedido`, `Compra` y `UnidadInventario` tienen reglas implementadas propias. El futuro módulo Encargos debe tener identidad y ciclo de vida diferenciados; la integración posterior se diseñará explícitamente.

## 3. Flujo funcional extremo a extremo

Las etapas de este apartado describen **la secuencia acordada**. Los nombres de estados que se muestran son **candidatos de diseño**, no enums aprobados.

### A. Recepción y preparación

1. El cliente escribe por WhatsApp solicitando un artículo.
2. El usuario o su hermana registra la solicitud; en el futuro también podría hacerlo un chatbot.
3. Se guarda lo que realmente se conoce: cliente, artículo, referencia/foto/enlace si existe, cantidad, talla, color u otra variante, notas y canal/origen.
4. Si faltan datos, el registro permanece **pendiente de aclaración**, sin inventarlos.
5. El responsable confirma que el cliente efectivamente quiere **que se busque** el artículo.
6. **Solo entonces** la solicitud entra en la bandeja de búsqueda de la madre. La confirmación para buscar **no** equivale a confirmar la compra.

### B. Búsqueda y alternativas en EE. UU.

7. La madre ve solicitudes confirmadas para búsqueda, idealmente agrupadas por producto y variante, con cantidades totales y desglose por cliente.
8. Marca qué está buscando o ha revisado, y comunica resultado:
   - Encontrado en la variante solicitada.
   - No disponible.
   - Existe alternativa (otra talla, color, modelo o presentación).
   - Aún pendiente de búsqueda.
9. Si existe alternativa, esta debe volver al responsable del cliente; **no sustituir silenciosamente** lo solicitado.
10. La madre fija el **precio cotizado** cuando ya encontró una opción concreta. Hasta ese momento no hay precio definitivo confirmado.
11. El usuario o su hermana comunica precio, variante, condiciones y eventual anticipo al cliente.

### C. Aceptación y compra

12. El cliente acepta o rechaza la cotización/alternativa. Guardar la **versión exacta de la oferta** aceptada para evitar confundirla con una cotización posterior.
13. Si la madre requiere anticipo por cantidad/caso, se solicita y se registra según las reglas financieras que se definan. Si no lo requiere, no bloquear por una política fija inexistente.
14. **Solo las solicitudes autorizadas para comprar** pasan a compra por encargo. La madre puede buscar varios artículos antes de que los clientes respondan, pero **buscar no habilita comprar**.
15. Al comprar, registrar qué se adquirió realmente y para qué cliente/cantidad se asigna. Si se compra menos que lo pedido, no marcar toda la solicitud como completada.
16. Si el artículo no se consigue, cambia el precio o la variante, vuelve a revisión/confirmación; no cambiar unilateralmente lo ya aprobado.

### D. Transporte, recepción y cierre

17. Los artículos pueden llegar a Guatemala en **equipaje de la madre** o **caja/envío**; esta logística debe poder asociarse a encargos y también a mercancía para inventario.
18. Registrar el estado de traslado/recepción sin convertir por ello automáticamente los artículos encargados en stock libre.
19. Cuando se entrega al cliente, registrar entrega, pagos restantes y cierre. La forma de enlazar con `Venta`, `Pago` y saldos existentes es una **decisión técnica/contable pendiente**; no crear saldos duplicados.
20. Cancelaciones, devoluciones y diferencias deben conservar historial y requerir reglas explícitas, especialmente si hubo anticipo o compra.

### Vista resumida de decisiones (no diagrama de entidades)

```text
WhatsApp / registro manual / futuro chatbot
                |
         Solicitud recibida
                |
    ¿Confirmada para buscar?
       no                sí
   Aclarar/esperar   Bandeja de mamá
                         |
                  Buscar artículo
                         |
        No disponible / alternativa / encontrado
                         |
                Mamá indica precio
                         |
               Cliente revisa oferta
                         |
          ¿Acepta precio y variante?
             no              sí
        Rechaza/ajusta    ¿Anticipo requerido?
                             |
                    Resolver anticipo si aplica
                             |
                     Autoriza compra
                             |
                    Compra para cliente
                             |
                  Transporte / recepción
                             |
                  Entrega / saldo / cierre
```

## 4. Dos confirmaciones que NO deben confundirse

| Confirmación | Quién/para qué | Qué habilita | Qué **no** habilita |
| --- | --- | --- | --- |
| **Confirmación de búsqueda** | Responsable de atención verifica que la solicitud es real y está suficientemente clara. | La madre la ve y puede buscarla. | Comprar, cobrar precio definitivo, generar stock o deuda. |
| **Aceptación de cotización y compra** | Cliente aprueba precio, variante y condiciones comunicadas. | Preparar/autorizar la compra; gestionar anticipo si la madre lo solicita. | Saltarse condiciones de anticipo, sustituir variante, comprar cantidades extra sin autorización. |

Esta separación es una de las invariantes principales del módulo.

## 5. Agrupar artículos iguales sin perder clientes

**Necesidad acordada:** la madre no debe buscar tres veces exactamente el mismo artículo si tres clientes lo pidieron.

**Ejemplo ilustrativo, no datos reales:**

- Cliente A solicita **2** unidades de una loción, aroma X, 250 ml.
- Cliente B solicita **1** unidad de la misma loción, aroma X, 250 ml.
- Cliente C solicita **1** unidad del aroma Y, 250 ml.

La bandeja de búsqueda podría mostrar **aroma X: 3 unidades para 2 clientes**, y **aroma Y: 1 unidad para 1 cliente**. Al abrir cada grupo, debe poder ver las solicitudes individuales. No agrupar aroma X con Y solo porque comparten marca/nombre.

**Reglas de agrupación:**

- Agrupar para **búsqueda y logística**, no para convertir varios clientes en un único encargo.
- Mantener `cliente`, `solicitud`, `cantidad`, `variante`, `estado`, `precio aceptado`, `anticipo`, `compra asignada` y `entrega` separados.
- No usar coincidencia aproximada como autorización automática para comprar un sustituto; revisar talla/color/aroma/modelo/presentación y cantidades.
- Permitir **compra parcial** y asignación de unidades reales a cada solicitud. Si la madre encuentra solo dos de las tres unidades del aroma X, debe quedar claro **cuáles clientes** recibirán esas dos; la política de prioridad/asignación está pendiente.
- La agrupación puede variar cuando entran nuevas solicitudes, se cancelan o cambian variantes. No debe alterar el historial de cada cliente.
- **Propuesta técnica, no aprobada:** una agrupación derivada de atributos normalizados y vínculos explícitos por línea de encargo; no convertir la agrupación en propietaria de anticipos ni de saldos.

## 6. Separación de compras para cliente e inventario propio

**Acuerdo confirmado:** la madre puede adquirir en un mismo viaje o caja productos **para encargos** y **para stock**. El contenedor físico compartido no significa que los registros comerciales sean iguales.

| Dimensión | Encargo de cliente | Compra para inventario |
| --- | --- | --- |
| Origen de la necesidad | Solicitud de cliente identificable. | Decisión de abastecer stock del negocio. |
| Destinatario | Cliente concreto, sujeto a confirmación. | Inventario del negocio, sin cliente asignado de origen. |
| Antes de comprar | Solicitud, búsqueda, cotización y aprobación. | Planificación de compra; no requiere aceptación de un cliente. |
| Al comprar | Vincular cantidad real con encargo(s) autorizados. | Seguir reglas vigentes de `Compra` y `UnidadInventario`. |
| Transporte | Puede compartir caja/equipaje. | Puede compartir caja/equipaje. |
| Al recibir | Mercancía asignada a cliente, no stock libre por defecto. | Recepción de unidades físicas según reglas actuales. |
| Al entregar/vender | Cierre comercial del encargo y saldo del cliente, según integración futura. | Venta/reserva ordinaria del inventario disponible. |

**Invariante propuesta:** el vínculo con una caja o viaje es **logístico**, no una reclasificación comercial. Registrar en una misma caja líneas de origen distinto sin mezclar costos, cantidades ni destinatarios.

**Pendiente:** cómo enlazar compras mixtas, comprobantes, gastos de transporte, costos de importación y landed cost con las entidades existentes. No presumir que el módulo de landed cost esté terminado.

## 7. Cotizaciones, anticipos, pagos y saldos

### Acordado

- El precio se conoce **después de encontrar** el artículo; lo decide la madre.
- El cliente debe aprobar ese precio y las condiciones antes de comprar.
- La madre puede exigir **anticipo opcional según cantidad**; no hay monto ni porcentaje estándar acordado.
- Cada cliente mantiene sus propios importes y decisiones aunque se busque un artículo agrupado.
- Un artículo aún no localizado no debe convertirse automáticamente en deuda, venta ni utilidad realizada.

### A definir antes de programar

- Si el precio comunicado es por unidad o por lote, y si incluye transporte/impuestos.
- Moneda de cotización, tipo de cambio y momento en que queda congelado; GTQ/USD ya tienen reglas en Compras, pero no se deben extrapolar automáticamente.
- Cuándo un anticipo entra al saldo global del cliente y cómo se contabiliza si todavía no existe venta.
- Cómo manejar anticipo recibido y posterior cancelación, falta de disponibilidad, cambio de precio o devolución.
- Si la madre puede aprobar excepciones y cómo quedan auditadas.
- Qué constituye una oferta vencida, cuánto dura y cómo se requiere nueva confirmación.
- Cómo vincular entrega y cobro final con el módulo de Ventas/Pagos sin doble deuda ni pagos duplicados.

**No decidido:** porcentaje mínimo, política de reembolso, plazos, penalizaciones, método de pago y reglas de prorrateo. No inventar estas políticas en la implementación.

## 8. Estados y transiciones: vocabulario de trabajo, NO contrato cerrado

En la conversación surgieron estados como `Solicitado`, `En búsqueda`, `Alternativa pendiente`, `Encontrado`, `Comprado` y `No disponible`. Para diseñar la implementación hará falta modelar **dimensiones independientes** en vez de un único enum que confunda atención, búsqueda, aprobación, compra y transporte.

| Dimensión | Estados/acciones candidatos | Observaciones |
| --- | --- | --- |
| Recepción | Borrador / pendiente de datos / recibido / confirmado para búsqueda / descartado | Solo confirmado para búsqueda entra en bandeja de mamá. |
| Búsqueda | Pendiente / en búsqueda / encontrado / alternativa ofrecida / no disponible | Una alternativa requiere revisión del cliente. |
| Cotización | Sin precio / precio propuesto / comunicado / aceptado / rechazado / requiere nueva oferta | Guardar versión de oferta aceptada. |
| Anticipo | No requerido / solicitado / recibido / pendiente de conciliación | **Opcional** y definido por mamá; reglas contables pendientes. |
| Compra | No autorizada / autorizada / parcialmente comprada / comprada / cancelación en revisión | No marcar compra por simple confirmación de búsqueda. |
| Logística | En origen / en equipaje o caja / en tránsito / recibido / entregado | Compartir caja no implica compartir propiedad. |
| Cierre | Abierto / parcialmente resuelto / completado / cancelado / incidencia | Los efectos financieros dependen del diseño final. |

**No crear estos enums tal cual** sin validar qué dimensiones son necesarias, transiciones permitidas, responsables y compatibilidad con los módulos actuales. Conservar historia de cambios y evitar saltos implícitos.

## 9. Sincronización, colaboración y bandejas

**Requisito confirmado:** información compartida **en tiempo real** entre madre, usuario y hermana, sin reenvío manual como mecanismo principal.

**Vistas funcionales propuestas:**

1. **Atención (usuario/hermana):** nuevas solicitudes, datos incompletos, pendientes de confirmar para búsqueda, cotizaciones que comunicar, alternativas por consultar, anticipos pendientes, entregas y respuestas de clientes.
2. **Búsqueda (madre):** solo solicitudes confirmadas para buscar; grupos de artículos/variantes y cantidades; detalles de clientes necesarios; botones para encontrado, alternativa, no disponible, precio y compra autorizada.
3. **Seguimiento por cliente:** todas sus solicitudes y decisiones, importes y entregas, sin mezclar otras personas del mismo grupo de búsqueda.
4. **Logística:** cajas/equipaje y contenido asignado, con trazabilidad de encargos frente a stock.

**Propuestas técnicas, todavía no decisiones:** usar actualización en vivo compatible con Blazor/SignalR, eventos de dominio o notificaciones de cambios, control de versión/concurrencia y bandejas de tareas por rol. No asumir que ya existen roles granulares: el sistema usa Identity, pero los permisos multiusuario requieren diseño.

**Casos que deben manejarse:** dos personas editan la misma solicitud; el cliente cambia de opinión mientras la madre compra; llega nueva cantidad para un grupo ya encontrado; una cotización queda obsoleta; se pierde la conexión del móvil. Evitar duplicados y sobrescrituras silenciosas.

## 10. Información mínima candidata (modelo conceptual, no esquema EF)

### Encargo / solicitud

- Identificador y referencia legible; fecha/hora y autor de registro.
- Cliente vinculado o datos mínimos para identificarlo sin duplicarlo.
- Canal de entrada (WhatsApp manual; chatbot futuro) y responsable de atención.
- Estado de confirmación para búsqueda; notas/historial de comunicaciones.
- Una o más líneas solicitadas.

### Línea de encargo

- Descripción original del artículo y referencia, enlace o imagen opcional.
- Marca/modelo/presentación, talla, color, aroma u otras variantes **cuando aplique**.
- Cantidad solicitada, confirmada, encontrada, autorizada, comprada y entregada (sin perder diferencias).
- Vínculo opcional a `Producto` existente, **sin obligar a crear Producto ni inventario por cada consulta**.
- Ofertas/alternativas y respuestas del cliente; precio aceptado por línea.
- Anticipo/pagos asociados a la operación según futura política contable.
- Asignación a compras reales y unidades destinadas al cliente; incidencias.

### Actividad y logística

- Historial de eventos con actor, fecha, estado anterior/nuevo y motivo.
- Agrupación de búsqueda por artículo y variante, conservando vínculos individuales.
- Compra o detalle de compra y comprobante si procede.
- Viaje/caja/equipaje, fechas y estado de recepción/entrega.
- Referencia de cierre comercial y pagos **solo después** de definir integración con los módulos vigentes.

**Estos son datos candidatos, no una orden de crear entidades/migraciones**. Evitar duplicar `Cliente`, `Producto`, `Pago`, `Compra` o `Venta` sin análisis de sus invariantes.

## 11. Integración con ResellManager existente

- **Clientes:** reutilizar identidad de cliente y preservar consulta de deuda total; no introducir un saldo paralelo que contradiga el actual.
- **Productos:** una referencia de producto puede ayudar a agrupar/buscar, pero un encargo nuevo puede empezar con texto/foto/enlace; no crear stock ficticio.
- **Pedidos/reservas:** `Pedido` vigente significa otra cosa y tiene reservas de inventario. No usar `TipoPedido.Catalogo` como atajo para el flujo de encargos USA sin especificación y pruebas.
- **Compras:** registrar compras reales sin perder si son para cliente o stock; la caja compartida no modifica el origen.
- **Inventario:** no exponer encargos como disponibles para venta general; decidir más adelante si se representan como asignaciones físicas separadas o mediante vínculos a compra/recepción.
- **Ventas y pagos:** diseñar momento de nacimiento de obligación, anticipo y entrega; evitar duplicidad de deuda y garantizar trazabilidad.
- **Virtuosa Store:** este módulo no agrega checkout ni crea automáticamente pedidos web. WhatsApp puede originar solicitudes, pero no equivale a una compra confirmada.
- **Chatbot:** solo un canal de captura futuro; autenticación, privacidad, confirmaciones y manejo de mensajes duplicados son trabajo pendiente.

Referencias de contratos **implementados**, que no deben modificarse por interpretar este plan:
[Pedidos](pedidos.md), [Compras](compras.md), [Inventario](inventario.md),
[Clientes](clientes.md), [Ventas](ventas.md) y [reglas de dominio](../architecture/domain-rules.md).

## 12. Escenarios de aceptación para un desarrollo futuro

Estos escenarios definen comportamiento deseado; **no son pruebas ejecutadas**.

1. **Solicitud no confirmada:** la hermana registra una solicitud recibida por WhatsApp. La madre no la ve aún en su lista de búsqueda.
2. **Confirmación de búsqueda:** el usuario confirma la solicitud y la madre la ve en tiempo real, sin que se cree compra, inventario, venta ni deuda.
3. **Artículo encontrado:** la madre registra precio y variante. El cliente todavía no ha aprobado la compra; no se habilita comprar como encargo confirmado.
4. **Alternativa:** se pidió talla M, solo hay L; se comunica L y queda pendiente hasta que el cliente la acepte o rechace.
5. **Precio rechazado:** el cliente no acepta; la búsqueda puede conservarse como historial, pero no se compra por él.
6. **Anticipo opcional:** para un encargo la madre no lo requiere; para otro sí. No existe regla fija universal.
7. **Agrupación:** dos clientes piden la misma variante; la madre ve cantidad agregada, pero se preservan dos decisiones, precios, anticipos y entregas.
8. **Compra parcial:** se consiguen dos de tres unidades; no se marcan tres compradas ni se asigna la misma unidad a dos clientes.
9. **Caja mixta:** una caja contiene dos artículos encargados y tres para stock; al recibirla los dos primeros siguen asignados a sus clientes.
10. **Cambios simultáneos:** madre y hermana modifican información relacionada; no se pierden confirmaciones ni se duplica la compra.
11. **Cancelación con anticipo:** no se borra el registro ni se devuelve/cobra automáticamente sin reglas financieras aprobadas.
12. **Chatbot:** un mensaje automatizado puede crear una solicitud en revisión, pero nunca aprobar precio, comprar o hacer pública la información de otros clientes.
13. **Desconexión móvil:** al reconectar se recupera estado autoritativo y se detectan decisiones que quedaron pendientes.

## 13. Preguntas pendientes para conversar con la madre y cerrar antes de implementar

**Operación comercial**
- ¿Quién puede marcar «confirmado para buscar» y quién puede marcar «cliente autorizó comprar»? ¿Se necesita evidencia de WhatsApp o basta el registro del responsable?
- ¿La madre siempre establece el precio final o puede proponerlo y que el usuario lo ajuste? ¿Quién autoriza descuentos?
- ¿Qué datos mínimos necesita la madre para buscar? ¿Puede devolver la solicitud por falta de datos?
- ¿Cómo se decide qué cliente recibe una unidad cuando varias personas pidieron la misma variante y hay pocas existencias?
- ¿Se permite comprar una unidad adicional para stock cuando hay una búsqueda agrupada? ¿Cómo se separa esa decisión?
- ¿Qué hacer cuando el cliente cambia talla, cantidad o modelo después de confirmar la búsqueda o después de aceptar la cotización?
- ¿Qué reglas hay para cancelar un encargo comprado y para aceptar devoluciones?

**Dinero**
- ¿Qué condiciones hacen que la madre pida anticipo y quién decide su monto?
- ¿Cómo se registra el anticipo antes de una venta? ¿Qué pasa con dinero recibido si no se encuentra el artículo?
- ¿Cuándo se considera deuda exigible, cuándo se reconoce una venta y cuándo se cierra el encargo?
- ¿Se cotiza en USD o GTQ, con qué tipo de cambio, y qué gastos incluye el precio?
- ¿Cómo se calculan y reparten gastos de caja/equipaje entre stock y encargos? (Conservar análisis de landed cost por separado.)

**Logística y personas**
- ¿Cómo identificar una caja o equipaje y quién confirma recepción parcial o total?
- ¿Se entregan artículos directamente o se guardan temporalmente? ¿Qué comprobante de entrega hace falta?
- ¿Qué cuentas/roles reales tendrán la madre, hermana, usuario y eventual chatbot? ¿Qué datos personales puede ver cada uno?
- ¿Qué avisos deben ser instantáneos y cuáles pueden quedar en una bandeja?
- ¿Se permite trabajar con conexión intermitente y qué sucede ante acciones duplicadas?

Estas preguntas son **pendientes**, no autorizaciones para inventar políticas.

## 14. Límites y definición de listo para comenzar desarrollo

**Fuera de alcance hasta nuevo acuerdo:** automatizar compras en tiendas de EE. UU., pagar a comercios externos, crear un checkout público, aceptar pagos en línea, generar inventario al recibir una solicitud, definir porcentajes de anticipo, realizar migraciones o modificar la API temporal de importación.

**Para iniciar implementación:** validar con la madre los puntos comerciales pendientes; cerrar permisos, ciclo de vida y política financiera; revisar invariantes de pedidos/compras/ventas/pagos; diseñar entidades y contratos por capas; especificar concurrencia y sincronización; y acordar pruebas de integración con base de datos aislada. La documentación antecede al código.

### Resumen de acuerdos que no deben perderse

**Usuario/hermana/chatbot futuro reciben → usuario/hermana confirman para buscar → madre ve y busca en EE. UU. → madre encuentra y fija precio/alternativa → cliente acepta → anticipo solo si la madre lo requiere → compra asignada a cliente → transporte compartible con stock, pero registros separados → entrega y cierre.**

No confundir **confirmación de búsqueda**, **aceptación de cotización**, **compra**, **recepción** y **venta**. Este documento preserva la conversación; las políticas todavía abiertas siguen siendo abiertas.

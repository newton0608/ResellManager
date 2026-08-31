# Backlog

## Backend V1 completado

- [x] Separar reserva comercial del estado físico de inventario.
- [x] Exigir correspondencia exacta entre cantidades de pedido y venta.
- [x] Registrar ventas completas sin adición posterior de artículos.
- [x] Proteger cancelaciones frente a pagos que producirían saldo negativo.
- [x] Endurecer transiciones físicas y recepción de mercancía.

## Fase 5.1: autenticación

- [x] Implementar login y logout con ASP.NET Core Identity.
- [x] Proteger globalmente las páginas de negocio.
- [x] Mantener el login público y eliminar el flujo de autorregistro.
- [x] Crear el usuario inicial mediante configuración segura opcional.
- [x] Cubrir autenticación y sesión con pruebas de integración.
- [ ] Incorporar roles y permisos si el negocio evoluciona a una tienda física con trabajadores.
- [ ] Incorporar administración de usuarios desde la aplicación cuando existan varias cuentas que deban crearse, deshabilitarse, restablecerse o asignarse a roles.

## Fase 5.2: shell visual y navegación

- [x] Implementar el layout principal con sidebar, topbar y área de contenido.
- [x] Implementar navegación base y estado activo para los módulos previstos.
- [x] Adaptar el shell para escritorio, tablet y teléfono con drawer móvil.
- [x] Integrar usuario autenticado y logout POST protegido en la topbar.
- [x] Crear inicio sin métricas ficticias y placeholders explícitos para módulos pendientes.
- [x] Ajustar visualmente login y error sin modificar su arquitectura de autenticación.
- [ ] Implementar las pantallas y operaciones completas de cada módulo en sus fases correspondientes.

## Endurecimiento de concurrencia después de completar la UI

### Saldo global por cliente

- [ ] Revisar todas las operaciones que afectan el saldo global de un cliente como sección crítica lógica: registrar venta, registrar pago/abono y cancelar venta.
- [ ] Implementar exclusión mutua para impedir que dos operaciones incompatibles sobre el saldo del mismo cliente se ejecuten simultáneamente.
- [ ] Añadir una segunda capa de seguridad a nivel de persistencia/transacción o control de concurrencia, además de la coordinación a nivel de aplicación.
- [ ] La solución no debe depender de suposiciones sobre la velocidad relativa, orden de ejecución o temporización de tareas/procesos asíncronos.
- [ ] Garantizar progreso: procesos u operaciones que no participan en la sección crítica de un cliente no deben bloquear indefinidamente a los que sí desean entrar.
- [ ] Garantizar espera acotada: ninguna operación que solicite acceso a la sección crítica debe poder ser postergada indefinidamente.
- [ ] Definir el alcance del bloqueo por cliente, evitando un bloqueo global del sistema cuando dos clientes distintos puedan procesarse de manera independiente.
- [ ] Agregar pruebas de concurrencia para pagos, ventas y cancelaciones simultáneas sobre el mismo cliente.

### Reservas de inventario — V2

- [ ] Tratar la asignación y liberación de una reserva como sección crítica por `UnidadInventarioId`, de modo que una misma unidad no pueda ser reservada simultáneamente por dos procesos/circuitos distintos.
- [ ] Implementar exclusión mutua por unidad, no un bloqueo global: dos reservas sobre unidades físicas diferentes deben poder procesarse de forma independiente.
- [ ] Mientras una operación de reserva/cancelación sobre una unidad esté en curso, una segunda operación incompatible sobre esa misma unidad debe esperar o ser rechazada de forma controlada antes de modificar la reserva.
- [ ] Mantener la validación actual del backend (`DetallePedidoReservaId`) como regla de negocio, pero complementarla con coordinación de concurrencia real; deshabilitar botones en Blazor no se considera protección suficiente.
- [ ] Añadir una segunda capa de seguridad en persistencia/transacción para evitar condiciones de carrera incluso si en el futuro existen varias instancias de la aplicación.
- [ ] La solución debe ser independiente de temporización, garantizar progreso y evitar espera indefinida/starvation.
- [ ] Agregar pruebas de concurrencia verdaderamente simultáneas: dos intentos de reservar la misma unidad para detalles distintos, reserva vs. cancelación de la misma unidad y cancelación de pedido vs. reserva concurrente.
- [ ] Revisar el contrato de `CancelarReservaAsync` para decidir si en V2 debe recibir/validar también el contexto de pedido o detalle y no únicamente `unidadInventarioId`.

- [ ] Revisar si existen otras secciones críticas además del saldo y las reservas una vez que la UI y los flujos reales estén completos.
- [ ] Considerar estas protecciones obligatorias antes de un escenario V2 multiusuario con operaciones simultáneas.

## Catálogo: información pendiente del negocio

- [ ] Confirmar porcentajes de comisión por categoría para cada proveedor de catálogo.
- [ ] Confirmar la base exacta o fórmula que utiliza cada proveedor para calcular la comisión.
- [ ] Diseñar configuración de comisiones por proveedor y categoría.
- [ ] Al automatizar comisiones, guardar snapshot del porcentaje y/o monto aplicado en cada venta para preservar historial.

## Flujos futuros

- [ ] Diseñar devoluciones y cambios para unidades entregadas.

---

## Fase 5.3: clientes

- [x] Listar clientes.
- [x] Registrar cliente.

- [x] Editar cliente.

- [ ] V2: definir eliminación/desactivación segura de clientes, preservando relaciones con ventas, pagos, pedidos e historial. Evaluar borrado lógico como alternativa preferente al borrado físico.

- [x] Buscar cliente por los criterios soportados por `IClienteService`.
- [x] Consultar detalle de cliente.

- [x] Ver historial de ventas y pagos/abonos.

- [x] Consultar saldo calculado por el backend.

---

## Fase 5.4: productos y categorías

- [x] Listar productos.
- [x] Buscar productos mediante `IProductoService.BuscarAsync`.
- [x] Registrar producto.
- [x] Editar producto.
- [x] Consultar detalle de producto.
- [ ] V2: definir eliminación/desactivación segura de productos, preservando ventas, pedidos, compras e historial. Evaluar borrado lógico como alternativa preferente al borrado físico.

---

## Fase 5.5: inventario

- [x] Listar unidades físicas mediante `IInventarioService`.
- [x] Buscar y filtrar unidades por estado físico mediante el backend.
- [x] Mostrar producto, identificación de unidad, compra/origen, ingreso y costo real de la unidad.
- [x] Presentar el estado físico y la reserva comercial como conceptos separados.
- [x] Registrar recepción de unidades compradas o en tránsito sin alterar reservas existentes.
- [x] Exponer únicamente las transiciones manuales `Comprada → EnTransito` y `Vendida → Entregada`.
- [ ] Integrar creación de unidades mediante la futura UI de compras; Inventario no crea unidades directamente.
- [x] Integrar creación y cancelación de reservas en la UI de pedidos; Inventario continúa como consulta del estado físico y la reserva.

---

## Fase 5.6: pedidos y reservas

- [x] Listar pedidos con cliente, tipo, estado, detalles y total estimado.
- [x] Crear pedidos con clientes y productos reales y múltiples detalles.
- [x] Consultar el detalle completo del pedido.
- [x] Agregar detalles a pedidos pendientes o confirmados.
- [x] Visualizar las unidades reservadas por detalle sin confundir reserva con estado físico.
- [x] Reservar unidades del mismo producto en pedidos físicos, respetando cantidad y estados terminales.
- [x] Permitir reservas de unidades compradas, en tránsito o disponibles.
- [x] Cancelar una reserva sin alterar compra, unidad ni estado físico.
- [x] Cancelar un pedido mediante IPedidoService, liberando todas sus reservas de forma atómica.
- [x] Mantener pedidos de catálogo completamente fuera del inventario físico.
- [x] Presentar listado, formulario, detalle y reservas de forma responsive.
- [ ] V2: endurecer concurrencia de reservas con exclusión mutua por `UnidadInventarioId` y protección de persistencia para impedir doble reserva simultánea.
- [ ] V2: definir edición de detalles de pedido.
- [ ] V2: definir eliminación de detalles de pedido.
- [ ] V2: definir reactivación de pedidos cancelados.
- [ ] V2: incorporar historial avanzado de cambios de reservas.

---


## Fase 5.7: ventas

- [x] Listar ventas registradas y canceladas con tabla de escritorio y cards móviles.
- [x] Consultar detalle, total y utilidad histórica desde `VentaDto`.
- [x] Registrar toda venta desde un pedido elegible mediante `IVentaService.RegistrarDesdePedidoAsync`.
- [x] Registrar ventas presenciales directas creando primero, de forma automática, un `Pedido` de tipo `VentaDirecta`.
- [x] Mantener la regla de que toda venta tiene `PedidoId`; no existen ventas libres ni ventas sin pedido.
- [x] Construir exactamente un detalle de venta por cada unidad solicitada en el pedido, sin venta parcial.
- [x] Para venta física, seleccionar únicamente unidades `Disponible`, compatibles y sin reserva ajena, priorizando reservas del mismo pedido.
- [x] Mantener la transición `Disponible → Vendida`, liberación de reserva y finalización del pedido exclusivamente en `VentaService`.
- [x] Para catálogo, capturar manualmente `CostoUnitario` y `PrecioFinal` sin usar `UnidadInventario`.
- [x] Cancelar ventas registradas mediante `IVentaService.CancelarAsync`, sin duplicar reglas de unidades, pedido o saldo en Blazor.
- [x] Proteger registro y cancelación contra doble submit en UI.
- [x] Mantener las ventas canceladas en modo de consulta.
- [ ] V2: endurecer concurrencia de saldo por cliente e inventario físico sin bloqueo global.
- [x] Excluir ventas libres: toda venta nace obligatoriamente de un pedido.
- [ ] V2: evaluar una orquestación transaccional atómica para crear `Pedido` y registrar `Venta` si el escenario multiusuario lo requiere.

- [ ] Registrar apartado

- [ ] Cancelar apartado

---

## Fase 5.7: pagos y abonos

- [x] Seleccionar un cliente real y consultar su saldo mediante `IClienteService.ObtenerSaldoAsync`.
- [x] Registrar pagos y abonos globales por cliente mediante `IPagoService.RegistrarAsync`.
- [x] Validar preventivamente monto positivo y no mayor al saldo, manteniendo al backend como autoridad.
- [x] Usar los valores reales del enum `MetodoPago`.
- [x] Consultar el historial ordenado mediante `IPagoService.ListarPorClienteAsync`.
- [x] Proteger el registro contra doble submit y recalcular saldo e historial después del pago.

- [x] Permitir cobro total usando la misma entidad `Pago`, sin asociación a una venta específica.

- [ ] V2: endurecer concurrencia de saldo por cliente para venta, pago y cancelación.

---

## Dashboard

- [ ] Total adeudado

- [ ] Inventario

- [ ] Productos apartados

- [ ] Últimos pagos

## Compras

- [ ] Registrar Comprobante de compra

- [ ] Registrar proveedor

- [ ] Adjuntar Fotografía de comprobante

- [ ] Consultar Comprobante de compra

- [ ] Ver historial de compras

## Categorías

- [x] Crear categoría.
- [x] Editar categoría.
- [x] Listar categorías.
- [ ] V2: definir eliminación/desactivación segura de categorías, preservando productos relacionados e historial.

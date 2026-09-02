# Alcance V1

## Incluye

✅ Clientes.

✅ Productos y categorías.

✅ Proveedores.

✅ Compras y comprobantes de compra.

✅ Importaciones, compras locales, catálogo y envíos del hijo según su flujo.

✅ Unidades de inventario físicas.

✅ Recepción de mercancía.

✅ Estados físicos controlados de inventario.

✅ Reservas/apartados separadas del estado físico.

✅ Pedidos.

✅ `CanalVenta` requerido en `Pedido`, separado de `TipoPedido`, visible en creación, listado y
detalle. No se duplica en `Venta`; los pedidos históricos se migran a `Otro` y Venta Directa usa
`Presencial` automáticamente.

✅ Ventas completas asociadas a pedidos.

✅ Venta presencial directa mediante creación automática de un `Pedido` de tipo `VentaDirecta`
con código `PED-VD-<GUID>` y una `Venta` con código `VEN-VD-<GUID>`. Ambos códigos técnicos se generan
sin captura de la usuaria; toda venta conserva un `PedidoId` y no existe venta sin pedido.

✅ Ventas de catálogo sin `UnidadInventario` cuando la mercancía no pasa por inventario físico.

✅ Pagos y abonos globales por cliente.

✅ Cálculo automático de saldo.

✅ Cancelación protegida de ventas antes de entrega.

✅ Consulta backend de utilidad por periodo; la pantalla Dashboard permanece pendiente.

✅ `Producto.PrecioSugerido` como referencia editable al vender.

✅ Código de barras opcional en `Producto`.

✅ Autenticación completa y protección de rutas implementadas en Fase 5.1.

✅ Aplicación privada, sin autorregistro público y con autorización basada en usuario autenticado.

✅ Shell visual responsive con sidebar, topbar, drawer móvil y área principal de contenido.

✅ Navegación base implementada para los módulos previstos, con páginas placeholder claramente identificadas.

✅ Módulo Blazor de clientes implementado en Fase 5.3: listado, búsqueda, registro, edición,
detalle, consulta del saldo calculado por el backend e historial de ventas y pagos/abonos,
con presentación responsive.

✅ Módulos Blazor de productos y categorías implementados en Fase 5.4: listado de ambos,
búsqueda de productos, registro, edición y detalle de producto, selección de categorías reales,
precio sugerido claramente identificado y presentación responsive.

✅ Módulo Blazor de inventario implementado en Fase 5.5: listado, búsqueda, filtro por estado,
recepción de mercancía y transiciones físicas manuales autorizadas, con estado y reserva
presentados por separado y diseño responsive.

✅ Módulo Blazor de pedidos y reservas implementado en Fase 5.6: listado, creación con múltiples
detalles, consulta, adición de detalles, reserva y liberación de unidades físicas, y cancelación
de pedidos con liberación atómica de reservas. Los pedidos de catálogo no usan inventario y la
presentación conserva separados el estado físico y la reserva.

✅ Módulos Blazor de ventas y pagos/abonos implementados en Fase 5.7: listado y detalle de ventas,
registro completo desde pedido físico o de catálogo y venta presencial directa con pedido automático,
códigos técnicos de pedido y venta directa generados automáticamente, cancelación delegada al backend,
selección de unidades disponibles compatibles, costo de catálogo capturado manualmente, saldo consultado
desde `IClienteService`, registro de pagos globales por cliente
e historial, con doble submit protegido y presentación responsive.

✅ Inicio y presentación del login alineados con la identidad visual de ResellManager.

## Pendiente para fases posteriores

🟡 Pantallas Blazor completas y operaciones de negocio para compras y proveedores.

🟡 Edición/eliminación de detalles, reactivación de pedidos e historial avanzado de reservas.

🟡 Endurecimiento V2 de concurrencia en reservas: exclusión mutua por `UnidadInventarioId`, sin bloqueo global, con protección adicional en persistencia/transacción para impedir que dos procesos reserven simultáneamente la misma unidad. Debe incluir pruebas concurrentes reales y garantías de progreso/espera acotada.

🟡 Endurecimiento V2 de concurrencia para saldo global por cliente en ventas, pagos/abonos y cancelaciones.

🟡 Revisión general de códigos internos que todavía se capturan manualmente en otros módulos.

🟡 Evaluar en V2 una orquestación transaccional atómica para la creación conjunta de `Pedido` y `Venta`
si el escenario multiusuario lo requiere; V1 conserva explícitamente las dos operaciones actuales.

🟡 Eliminación o desactivación segura de clientes, productos y categorías, pendiente de reglas
que preserven relaciones e historial.

🟡 Dashboard de negocio con información real, incluyendo como posibles mejoras ventas registradas
por canal y cantidad de pedidos por canal.

🟡 Adjuntar/gestionar fotografías reales de comprobantes desde la UI.

## Pendiente de información del negocio

🟡 Confirmar porcentajes de comisión de catálogo por proveedor/categoría.

🟡 Confirmar la base/fórmula utilizada por cada proveedor para calcular la comisión.

## Fuera del alcance V1 actual

❌ IA.

❌ Integraciones automáticas con Facebook o WhatsApp.

❌ Tienda Web/ecommerce; `CanalVenta.Web` solo prepara el concepto de dominio.

❌ OCR.

❌ Integración con WhatsApp.

❌ Multiusuario avanzado.

❌ Funcionamiento offline.

❌ Devoluciones y cambios de unidades ya entregadas; requieren un flujo específico futuro.

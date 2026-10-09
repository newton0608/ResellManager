# Base de Datos

Este documento describe el modelo lógico de base de datos de ResellManager V1.

Incluye contratos vigentes y sus ampliaciones enlazadas; el DER conserva el
modelo base V1 y no debe suponerse actualizado con cada campo posterior. La
fuente técnica del esquema implementado es
[EntityConfigurations](../../src/ResellManager.Infrastructure/Persistence/Configurations/EntityConfigurations.cs)
y el [snapshot/migraciones](../../src/ResellManager.Infrastructure/Persistence/Migrations/).

## Ciclo de persistencia actual

- [ResellManagerDbContext](../../src/ResellManager.Infrastructure/Persistence/ResellManagerDbContext.cs)
  integra negocio e Identity sobre SQLite mediante EF Core 10; no hay una base
  pública separada para la tienda.
- [InicializadorBaseDatos](../../src/ResellManager.Web/Inicializacion/InicializadorBaseDatos.cs)
  aplica las migraciones existentes al arrancar, antes e independientemente del
  seed de usuario. Probar un arranque contra datos reales puede modificar esquema.
- SQLite guarda referencias de archivos, no sus binarios. Comprobantes e
  [imágenes](../24_ImagenPrincipalProducto.md) se administran fuera de wwwroot;
  respaldo/restore debe cubrir datos y archivos juntos según el
  [runbook](../deployment/deployment.md).
- Hay restricciones de relación `Restrict`, `Cascade` y `SetNull` según entidad.
  Las FK no implementan por sí mismas una política de borrado lógico.
  Cliente/Producto no tienen estado activo, filtros de desactivación ni casos de
  uso de reactivación; [decisión 025](../11_DecisionesDeDiseño.md#025-preservar-clientes-y-productos-con-historial)
  sigue pendiente de implementación, sin migración definida.

El diagrama se encuentra en:
`docs/diagrams/09_BaseDeDatos.drawio`

Los contratos vigentes se describen aquí y en la configuración EF; la ampliación GTQ/USD de Compras se detalla en [monedas de compra](../28_MonedasDeCompra.md).

## Galería y categorías V1.4

`20261008144723_AddCatalogGalleryAndSubcategories` añade `ProductoImagenes`
(GUID, ProductoId, RutaRelativa, Orden) y `CategoriaPadreId` nullable con FK
restrictiva. Registra las portadas existentes sin modificar archivos; las
categorías anteriores siguen como raíces. El servicio valida 0–8 fotos, portada
única y máximo dos niveles sin ciclos. Snapshot y migración se mantienen en EF;
no cambia datos comerciales históricos. Detalle en [V1.4](../modules/catalogo-v1-4.md)
y [almacenamiento](../24_ImagenPrincipalProducto.md).

## Notas generales

- El saldo del cliente no se almacena directamente; se calcula con ventas registradas y pagos.
- `FechaVenta` no se almacena en `UnidadInventario`; se obtiene desde `Venta.Fecha`.
- `Compra.Origen` define el tipo de abastecimiento. `EnvioHermano` es el identificador persistido; la UI puede mostrar «Envío del hijo».
- `ComprobanteCompra` respalda opcionalmente una compra.
- `Producto.PrecioSugerido` es una referencia comercial y no representa costo.

## Contratos técnicos vigentes

- Las propiedades se llaman `CodigoInterno` y `Producto.CodigoBarras`; no `InternalCode` ni `Barcode`.
- EF configura capacidad de 50 caracteres e índice único para `CodigoInterno` de Producto, Compra, UnidadInventario, Pedido y Venta. `Producto.CodigoBarras` es opcional y tiene longitud configurada de 100 caracteres. Son contratos del modelo; no se propone renombrar ni migrar el esquema.
- `Pedido.CanalVenta` es obligatorio, independiente de `TipoPedido` y se persiste como entero: `Presencial = 1`, `WhatsApp = 2`, `Facebook = 3`, `Web = 4`, `Otro = 5`. Venta consulta el canal mediante su pedido; no tiene una columna propia.
- `UnidadInventario.FechaIngreso` es `DateOnly?`: las importaciones nacen sin fecha de ingreso y la recepción la asigna a las unidades existentes.

## Inventario físico y reservas

- `UnidadInventario` representa una unidad física real.
- `EstadoUnidadInventario` contiene únicamente estados físicos/logísticos: `Comprada`, `EnTransito`, `Disponible`, `Vendida`, `Entregada` y `Perdida`.
- La reserva comercial se almacena en `UnidadInventario.DetallePedidoReservaId`, una FK nullable hacia `DetallePedido`.
- Una unidad sin reserva mantiene `DetallePedidoReservaId = null`.
- La FK permite obtener indirectamente el `Pedido` y `Cliente` de la reserva.
- Cancelar una reserva no modifica el estado físico de la unidad. Marcarla `Perdida` desde `Comprada` o `EnTransito` sí cambia ese estado y deja la FK de reserva en null, conservando unidad, compra y costo. Es irreversible en V1; la unidad no puede recibirse, reservarse ni venderse, y no se cuenta como recibida.

## Pedidos y ventas

- `DetallePedido` almacena producto, cantidad y precio unitario solicitado.
- Toda `Venta` tiene `PedidoId` obligatorio y único: un pedido admite cero o una venta; cada venta pertenece exactamente a un pedido. Venta Directa crea su pedido automáticamente. Cancelar la venta no elimina esa relación ni permite una segunda venta para el mismo pedido.
- Los únicos estados de `Venta` son `Registrada` y `Cancelada`; los pagos pertenecen al cliente.
- `DetalleVenta` representa una unidad vendida y no contiene campo cantidad.
- Para inventario físico, `DetalleVenta.UnidadInventarioId` referencia la unidad vendida.
- `DetalleVenta.UnidadInventarioId` puede ser null únicamente en el flujo de catálogo sin inventario físico.
- `DetalleVenta.ProductoId`, `CostoUnitario` y `PrecioFinal` conservan la información transaccional necesaria para historial y utilidad. `UnidadInventarioId`, `ProductoId` y `CostoUnitario` son nullable en el esquema; el servicio exige producto y costo al registrar catálogo, además del precio, y guarda snapshots de producto/costo también al vender inventario físico.
- La relación de una unidad con `DetalleVenta` admite `0..N` detalles a lo largo del historial cuando ventas anteriores fueron canceladas; no puede pertenecer simultáneamente a dos ventas `Registrada`. El índice de `DetalleVenta.UnidadInventarioId` no es único; el servicio valida el uso en ventas registradas. La reventa se registra mediante otro pedido.

## Catálogo

- Las compras de catálogo nunca generan `UnidadInventario` en V1; sus ventas no utilizan unidades físicas.
- Una venta de catálogo se registra con `ProductoId`, `CostoUnitario` y `PrecioFinal`.
- Los pedidos `Catalogo` no pueden tener reservas de unidades físicas.
- Los futuros porcentajes de comisión por proveedor/categoría deberán almacenarse de forma configurable y conservarse históricamente en las ventas cuando se implemente la automatización.

## Costos y utilidad

- El costo no pertenece a `Producto`.
- En inventario físico, el costo proviene de `UnidadInventario.Costo`, generado desde el detalle de compra.
- En catálogo, el costo utilizado por la venta se conserva en `DetalleVenta.CostoUnitario`.
- La utilidad se calcula con `PrecioFinal - CostoUnitario` para ventas registradas.
## Moneda de origen en compras

GTQ es la moneda base. Compra y DetalleCompra conservan costos originales GTQ/USD y el tipo aplicado congelado; Total, CostoUnitario y UnidadInventario.Costo siguen siendo GTQ. La migración preserva todos los importes históricos. Consulta [contratos y compatibilidad](../28_MonedasDeCompra.md).

# Productos y categorías

**Estado: implementado.** Producto describe el artículo; `UnidadInventario`
representa cada unidad física. Producto no almacena el costo de adquisición.
Categoría es un maestro configurable, no un enum.

## Contratos y documentación canónica

- [Precio sugerido frente a precio/costo transaccional](../architecture/domain-rules.md#producto-precios-y-utilidad).
- [Códigos](../15_CodigosYCanalesVenta.md): PRO- automático en backend, código de
  barras externo opcional y manual, también capturable con cámara.
- [Medidas y presentación](../23_MedidasYPresentacionProducto.md): ml o gramos
  opcionales, nunca ambos informados; presentación comercial independiente.
- [Imagen principal](../24_ImagenPrincipalProducto.md): una referencia privada
  a WebP procesado, almacenamiento administrado y compensación ante fallos.
- [Scanner](../29_BarcodeScanner.md): contrato, vendor fijado, pruebas JS/ópticas
  y validación física pendiente. No extrapoles emulación a iPhone real.
- [Búsqueda asistida por código de barras](productos-lookup-codigo-barras.md):
  consulta local + proveedores externos, revisión
  antes de aplicar datos y guardado manual. **Implementado en Agregar producto.**
- No existe estado activo ni desactivación/reactivación de Producto. La política
  acordada es no eliminar físicamente productos desde la aplicación, tengan o no
  historial: siempre se prevé **Desactivar / Reactivar**. La
  [decisión 025](../11_DecisionesDeDiseño.md#025-preservar-clientes-y-productos-con-historial)
  todavía no está implementada. Quitar la imagen no equivale a borrar Producto.

## Dónde trabajar y validar

Los contratos están en [Services.cs](../../src/ResellManager.Application/Interfaces/Services.cs).
ProductoService/CategoriaService viven en
[MasterDataServices](../../src/ResellManager.Infrastructure/Services/MasterDataServices.cs);
la orquestación con archivos, en
[ProductoConImagenService](../../src/ResellManager.Infrastructure/Services/ProductoConImagenService.cs).
UI en `Components/Productos/`, `Components/Categorias/` y sus páginas de Web.

Pruebas existentes en [ResellManager.Tests](../../tests/ResellManager.Tests/):
`CatalogoModuloTests` (maestros administrativos), `ProductoPrecioTests`,
`ImagenPrincipalProductoTests` y `CreacionPedidoProductoTests`.
Para scanner sigue su guía y `npm run test:js`; para cambios de publicación lee
[Catálogo público](catalogo.md), cuyo contrato es diferente.

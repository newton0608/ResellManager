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
- [Galería e imagen principal](../24_ImagenPrincipalProducto.md): referencias privadas
  a WebP procesado, almacenamiento administrado y compensación ante fallos.
  Incluye miniaturas derivadas de 240 px para el listado administrativo, imagen
  grande en detalle, caché privada versionada y generación reanudable para fotos
  existentes. Implementado en este hotfix; todavía no desplegado.
- [V1.4 implementada](catalogo-v1-4.md): de cero a ocho imágenes totales
  por producto, portada y orden, zoom público y categorías padre/hija. La migración
  conserva la imagen existente y deja las categorías anteriores como raíces.
- [Ajustes UX implementados](catalogo-v1-4-ajustes-ux.md): selectores dependientes
  de categoría principal y subcategoría opcional en Nuevo/Editar y alta desde
  Compra. Sólo se persiste `Producto.CategoriaId`; la raíz declarada se valida
  en servidor mediante el contexto opcional `ProductoInput.CategoriaPrincipalId`.
  Progreso real por archivo al preparar/transferir mediante SignalR, con
  «Guardando fotografías…» sin contador durante el guardado atómico. No añade
  columnas ni migración. QA físico pendiente.
- [Importación temporal mediante API](importacion-temporal-productos.md): contrato de herramienta restringida para cargar productos y fotografías desde un agente, sin SQL directo ni alta de inventario. **Implementada, integrada y desplegada el 15/10/2026; acceso temporal local.**
- [Scanner](../29_BarcodeScanner.md): contrato, vendor fijado y pruebas JS/ópticas. Existe validación física posterior en **iPhone 14 Plus + Brave**; la regresión histórica específica de Safari no quedó certificada por esa prueba.
- [Búsqueda asistida por código de barras](productos-lookup-codigo-barras.md):
  consulta local + proveedores externos, revisión
  antes de aplicar datos y guardado manual. **Implementado en Agregar producto y en el alta asistida explícita
  desde el scanner de Nueva compra**; ese consumidor se documenta en el
  [contrato operativo](scanner-operativo-v2-1.md).
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
`ImagenPrincipalProductoTests`, `CreacionPedidoProductoTests`, `ProductoLookupTests`,
`ProductoLookupProvidersTests`, `ProductoLookupImagenTests`, `ProductoLookupPreviewTests`,
`ImagenExternaRedireccionesTests`, `FlujoImagenExternaTests`,
`ProductoCategoriaDependienteTests` y `CompraProductoFlujoTests`.
Para scanner sigue su guía y `npm run test:js`; para cambios de publicación lee
[Catálogo público](catalogo.md), cuyo contrato es diferente.

La [regresión integrada del scanner](../auditorias/scanner-v2-1-cierre-tecnico-20261017.md)
añade `ScannerOperativoGaleriaTests`: alta asistida desde Compra con galería,
portada/subcategoría, reintento/cancelación y código registrado durante la edición.

La [validación de miniaturas y caché del 22/10/2026](../auditorias/productos-miniaturas-20261022.md)
incluye generación desde fotografías existentes y navegación por nombre/botón.
El fallo reportado del nombre no se reprodujo en el entorno aislado ni en `main`;
su causa y corrección en el dispositivo afectado siguen pendientes de confirmar.

## Listado administrativo incremental

El hotfix incorpora scroll infinito en `/productos`, aún sin despliegue. Se
cargan 20 productos al abrir y hasta 20 adicionales al acercarse al final.
`IProductoService.ListarPaginaAsync` filtra, ordena y limita en SQLite: consulta
como máximo 21 filas para detectar si hay otra página y devuelve hasta 20.
No se obtiene todo el catálogo para recortarlo en el navegador ni se ejecuta un
conteo total. El contador de la pantalla informa productos **cargados**.

Se conserva el orden alfabético por nombre, con ID como desempate; en búsquedas,
las coincidencias exactas de código interno o barras tienen prioridad. El cursor
conserva prioridad, nombre e ID y no depende de que la fila anterior siga
existiendo. Es válido para el término y categoría aplicados; cada nueva búsqueda
o «Limpiar» descarta resultados/cursor y comienza desde la primera página.
La búsqueda sigue siendo global sobre todos los productos, combinable con la
categoría exacta seleccionada. Escribir o seleccionar opciones sin pulsar Buscar
no cambia los filtros de las cargas siguientes.

El listado serializa sus solicitudes y evita duplicar IDs. Ante error al cargar
otra página conserva lo mostrado, detiene la carga automática y ofrece reintento.
«Cargar más productos» permite avanzar por teclado o sin IntersectionObserver.
Al finalizar no hay nuevas solicitudes y se informa que están todos los resultados.
Al navegar se cancela la lectura pendiente y se desconecta el observer. El patrón
de sentinel sigue el existente del catálogo público, sin modificar ese consumidor.

No es una instantánea transaccional: altas o cambios de nombre concurrentes pueden
requerir reiniciar la búsqueda para aparecer en el orden actualizado. Los métodos
anteriores `ListarAsync` y `BuscarAsync` conservan sus contratos para otros flujos.
No se añaden migraciones ni dependencias.

Regresiones: `ProductoPaginacionTests` verifica límites SQL, nombres repetidos,
prioridad exacta, filtros y continuidad sin la fila del cursor; `ProductosListadoTests`
cubre serialización, filtros aplicados, reintento y cancelación. Las pruebas JS
de `productos-listado` cubren montaje/desmontaje y callbacks de cursores antiguos;
`npm run qa:productos` valida scroll real y fallback manual en navegador.

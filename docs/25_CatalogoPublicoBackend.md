# Catálogo público: backend de lectura

Esta guía documenta la API pública de lectura implementada. La UI también existe: consulta [Catálogo](modules/catalogo.md) para el estado integrado y los dominios. Reutiliza `Producto`, `Categoria` y el inventario físico existente. No es el flujo de `TipoPedido.Catalogo` / `OrigenCompra.Catalogo`, que sigue funcionando bajo pedido sin generar unidades físicas.

La iteración original de backend no incorporó UI; esta se añadió posteriormente. El sistema actual sigue sin cuentas públicas, autorregistro, carrito, pedidos web, checkout ni pagos online. Aquella iteración no modificó el esquema. V1.4 añade persistencia de galería y jerarquía de categorías, conservando las reglas de compras, inventario, reservas, pedidos y ventas; ver [contrato V1.4](modules/catalogo-v1-4.md).

## Disponibilidad exacta

Un producto se publica si existe **al menos una misma unidad** que cumpla simultáneamente:

```text
UnidadInventario.Estado == Disponible
UnidadInventario.DetallePedidoReservaId == null
ningún DetalleVenta de esa unidad pertenece a una Venta con Estado == Registrada
```

Esta definición combina la elegibilidad de venta general de `SeleccionOperativaService` con la validación definitiva de `VentaService`. `IInventarioService.ListarDisponiblesAsync` y el Dashboard filtran únicamente por estado físico; por ello no son una fuente suficiente para publicar disponibilidad comercial.

- `Comprada`, `EnTransito`, `Vendida`, `Entregada` y `Perdida` no cuentan.
- Una unidad recibida y reservada sigue excluida; recibirla no libera su reserva.
- Cualquier `DetallePedidoReservaId` no nulo bloquea la publicación de esa unidad, incluso ante datos inconsistentes de un pedido terminal. Los flujos existentes de cancelación/completado son quienes liberan las asociaciones; esta consulta nunca las corrige ni las ignora.
- El historial de una venta `Cancelada` no bloquea una unidad que volvió a `Disponible`.
- Una venta `Registrada` bloquea aunque el estado físico haya quedado inconsistente.
- No se agrega una condición sobre `FechaIngreso`: la validación de venta actual usa el estado físico.
- Un producto con otras unidades reservadas/vendidas se publica si aún tiene una libre. Se publica una sola vez y no se revela el número de unidades.

La disponibilidad es una lectura del momento, sin crear una reserva ni garantizar disponibilidad futura. Se deshabilita el almacenamiento HTTP en caché con `Cache-Control: no-store`.

## Capas y contratos

- **Application:** `ICatalogoPublicoService`, `ProductoCatalogoDto`, `ProductoCatalogoDetalleDto` y [DTOs de navegación](../src/ResellManager.Application/DTOs/CatalogoNavegacionDtos.cs). La abstracción permite listar/buscar/filtrar con compatibilidad o páginas, consultar escaparate/opciones/contexto, obtener detalle y abrir portada o fotografías de galería por ID.
- **Infrastructure:** `CatalogoPublicoService`, registrado scoped. Las consultas EF son `AsNoTracking`, usan `Any`/`EXISTS` y un único filtro de disponibilidad en todas las lecturas. El listado proyecta campos comerciales; el detalle carga producto, categoría e imágenes para devolver metadatos mínimos. Nunca devuelve entidades administrativas.
- **Web:** `CatalogoPublicoEndpoints`, adaptador HTTP con diez rutas GET `AllowAnonymous`. Los errores de estos endpoints no reejecutan las páginas Blazor administrativas.
- **Domain:** V1.4 añade `ProductoImagen` y `Categoria.CategoriaPadreId` opcional.

El listado expone `Id`, `Nombre`, `CategoriaId`, nombre de `Categoria`, `PrecioPublico`, `TieneImagenPrincipal`, `Disponible`, `Marca`, `CategoriaPadreId` y `CategoriaPadreNombre`. El detalle agrega `Descripcion`, `Marca`, `Modelo`, `Color`, `Talla`, `ContenidoMl`, `PesoGramos` y `Presentacion`, más `Imagenes` ordenadas (`Id`, `Orden`, `EsPortada`). `PrecioPublico` se proyecta directamente de `PrecioSugerido`; no calcula márgenes ni utiliza precios/costos transaccionales. `Disponible` es siempre true en las respuestas exitosas porque las lecturas excluyen los demás productos.

No se exponen costo, proveedor, compras, unidades, reservas, pedidos, clientes, observaciones de categoría ni rutas de archivo. Los contratos contienen únicamente campos comerciales explícitos, sin entidades Domain ni DTOs administrativos anidados.

`CodigoInterno` no se publica: [la documentación actual](15_CodigosYCanalesVenta.md) lo clasifica como identificador técnico generado `PRO-<GUID>`. Tampoco se publica `CodigoBarras`. Ambos siguen siendo criterios de búsqueda compatibles con el servicio existente, sin incluirlos en la respuesta. Si el negocio necesita una referencia comercial pública, debe revisarse esa decisión antes de diseñar la UI.

## API

| Método y ruta | Resultado |
| --- | --- |
| `GET /api/catalogo/productos?termino=...&categoriaId=...&marca=...` | 200 con array compatible de productos disponibles. Los tres filtros son opcionales y se combinan mediante AND; la UI vigente no utiliza esta lectura completa. |
| `GET /api/catalogo/productos/pagina?termino=...&categoriaId=...&marca=...&cursor=...&tamano=16` | `items`, `hasMore`, `nextCursor`; máximo 16 productos, filtros AND antes de limitar. |
| `GET /api/catalogo/productos/escaparate?cursor=...&tamano=3` | `secciones`, `hasMore`, `nextCursor`; máximo tres raíces y diez productos por carrusel. |
| `GET /api/catalogo/productos/raices?cursor=...&tamano=16` | Página de categorías raíz publicables, máximo 16; incluye raíces con publicaciones sólo en hijas. |
| `GET /api/catalogo/productos/marcas?cursor=...&tamano=16` | Página de marcas públicas normalizadas para presentación, máximo 16, sin valores vacíos ni variantes duplicadas. |
| `GET /api/catalogo/productos/categorias/{categoriaId}/contexto` | `raiz`, `seleccionada`, primera página de `subcategorias`; 404 si la categoría no tiene publicaciones directas ni indirectas. |
| `GET /api/catalogo/productos/categorias/{raizId}/hijas?cursor=...&tamano=16` | Página de hijas con productos públicos, máximo 16. No expone nietos ni hijas vacías. |
| `GET /api/catalogo/productos/{productoId}` | 200 con detalle comercial; 404 si no existe o ya no tiene unidades libres. |
| `GET /api/catalogo/productos/{productoId}/imagen` | 200 `image/webp`; portada vigente compatible, 404 si no está publicado o no puede abrirse. |
| `GET /api/catalogo/productos/{productoId}/imagenes/{imagenId}` | Fotografía identificada por GUID opaco dentro del producto; misma elegibilidad, `no-store` y 404 sin rutas privadas. `principal` conserva el fallback legado. |

La búsqueda conserva los campos y mecanismo de `IProductoService.BuscarAsync`: nombre, código interno y código de barras, con `Trim`, minúsculas y coincidencia parcial. El array compatible prioriza códigos exactos y luego ordena por nombre/ID; las páginas y carruseles ordenan por ID ascendente para mantener un cursor único. No agrega búsqueda por proveedores, compras o códigos de unidad, matching difuso ni normalización de acentos; se conservan las capacidades actuales de SQLite. Término vacío equivale al listado; categoría desconocida o búsqueda sin coincidencias devuelve un array o página vacía, según el contrato elegido.

La categoría raíz incluye productos directos y de sus hijas; una hija incluye sólo sus productos. Marca compara `Trim` y `OrdinalIgnoreCase` **dentro de SQLite, antes de limitar y proyectar**, conservando soporte Unicode sin mutar datos. Las opciones públicas ya utilizan lecturas independientes acotadas; el estado previo que las derivaba del listado completo quedó sustituido por los [ajustes UX V1.4 implementados](modules/catalogo-v1-4-ajustes-ux.md).

Solo se registran GET. No se permite escribir a través del catálogo. Los casos de uso devuelven `ServiceResult` para detalle e imagen siguiendo el patrón existente; HTTP devuelve 404 vacío sin información administrativa.

## Navegación y paginación V1.4

`CatalogoProductosPaginaDto` contiene `Items` comerciales, `HasMore` y
`NextCursor` entero nullable. Raíces e hijas usan la misma estructura con
`CategoriaCatalogoDto` (`Id`, `Nombre`, `CategoriaPadreId`), y marcas la usan
con strings y cursor textual. `CatalogoEscaparatePaginaDto` entrega `Secciones`:
cada sección contiene `Categoria` raíz y `Productos`. El contexto entrega
`Raiz`, `Seleccionada` y `Subcategorias` paginadas; permite restaurar una hija
pública aunque su ID quede fuera de las primeras dieciséis opciones.

Productos, raíces e hijas usan keyset por `Id` ascendente (`Id > cursor`).
El tamaño predeterminado/máximo es **16**, o **3** para escaparate; tamaños menores
positivos son válidos. El cursor entero, si se informa, debe ser positivo.
Un tamaño fuera de rango o cursor inválido devuelve 400 sin página administrativa.
Una fila adicional obtenida en SQL determina `HasMore`; `NextCursor` es el último
ID entregado cuando hay continuación y null al final. No se ofrece un total de
stock ni un snapshot permanente. Cada nueva consulta aplica la disponibilidad
vigente antes de limitar, igual que detalle e imágenes.

El escaparate consulta un bloque de raíces y luego **todos sus carruseles en una
sola consulta**, sin N+1. Un `NOT EXISTS` con desplazamiento de nueve filas busca
el décimo producto anterior elegible de la misma raíz; así selecciona sólo los
primeros diez por ID, sin contar ni materializar todos los anteriores. Los
productos directos y de hijas participan en el mismo orden y rango. Si una raíz
pierde disponibilidad entre ambas consultas, no aparece vacía en la respuesta;
se conserva el cursor de la última raíz examinada y `HasMore`, incluso si no
queda ninguna sección en ese bloque.

La collation SQLite `RM_CATALOGO_MARCA`, registrada por conexión en el servicio,
usa `Trim` y `OrdinalIgnoreCase`, también para letras no ASCII. El filtro de marca
forma parte del `WHERE` anterior a `LIMIT`. Las opciones se agrupan con esa
collation; `MIN` con comparación binaria selecciona una representación estable
entre variantes almacenadas. Se ordenan por la comparación normalizada y tienen
keyset textual; el recorte de presentación se aplica sólo al bloque obtenido.
No hay tabla de marcas, modificación de valores persistidos ni migración nueva
para paginación/colación. Marca vacía no crea un filtro ni una opción.

Raíces, hijas y marcas derivan únicamente de productos elegibles y no usan
servicios administrativos ni DTOs completos de producto para construir opciones.
Son independientes de la búsqueda/marca/categoría activas. Todas las nuevas
lecturas conservan `Cache-Control: no-store`; las páginas de tarjeta sólo
proyectan el indicador de portada, sin galería ni rutas privadas.

## Imágenes privadas y acceso público controlado

Se conserva el almacenamiento configurado fuera de `wwwroot` y el endpoint autenticado `GET /productos/{productoId}/imagen`.

El nuevo caso de uso recibe únicamente el ID, verifica disponibilidad y lee internamente `ImagenPrincipalRuta`. Exige que la referencia pertenezca a `productos/{productoId}/`; el almacenamiento existente valida después el formato estricto y rechaza traversal/rutas físicas. Solo abre el WebP procesado ya guardado. La respuesta transporta el stream y el MIME, sin rutas ni nombres físicos.

`TieneImagenPrincipal` indica que existe una referencia persistida no vacía; no comprueba el disco para cada fila. Un archivo ausente sigue respondiendo 404. Las imágenes tampoco se almacenan en caché HTTP y conservan `X-Content-Type-Options: nosniff` del host.

## Validación y próximos puntos a revisar

Las pruebas usan SQLite real y los servicios existentes para comprar, recibir, reservar, vender y cancelar. Incluyen todos los estados físicos, mezcla de unidades, venta activa con estado inconsistente, búsqueda, categoría, detalle inexistente, listas explícitas de campos permitidos en JSON, acceso anónimo, métodos de escritura rechazados y protección de imágenes/rutas. El host de pruebas aísla también el directorio de imágenes en una carpeta temporal.

Puntos de revisión originalmente registrados antes de la UI (ahora implementada); no son requisitos automáticamente aprobados:

1. Que nombres, descripciones y fotos existentes sean apropiados para publicación: no existe una marca editorial de publicación independiente del stock.
2. Si el negocio desea mostrar alguna referencia comercial y cómo presentar `PrecioSugerido` como precio público; la moneda/formato siguen siendo una decisión de presentación.
3. Paginación y navegación por raíces/hijas: **implementadas en los ajustes UX V1.4**, sin `ICategoriaService` administrativo; ver [contrato vigente](modules/catalogo-v1-4-ajustes-ux.md). La observación original de lectura completa se conserva como contexto histórico, no como pendiente actual.
4. La disponibilidad puede cambiar entre lecturas. Cualquier futura operación comercial debe revalidar inventario mediante los flujos existentes.

## Resultado de validación de esta iteración

Registro histórico del trabajo de backend, no resultados de la suite actual ni estado Git actual. El backend se incorporó después en `9a119bb`.

- `dotnet build ResellManager.sln --no-restore`: correcto, 0 advertencias y 0 errores (.NET 10).
- Pruebas específicas de `CatalogoPublico`: 47 correctas, 0 fallidas y 0 omitidas.
- `dotnet test ResellManager.sln --no-build --no-restore`: 545 correctas, 0 fallidas y 0 omitidas, incluida toda la suite existente.
- `git diff --check`: correcto. Trabajo realizado en `feature/tailwind-ui`, sin commit ni push.

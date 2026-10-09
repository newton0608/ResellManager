# Virtuosa Store: identidad pública

Virtuosa Store es la identidad de la tienda pública de ResellManager. Las vistas `/catalogo` y `/catalogo/{productoId}` consumen el catálogo del mismo backend y el mismo inventario. El sistema administrativo conserva su identidad visual `ui-*`; los estilos de la tienda se definen con tokens y componentes `store-*`. La tienda solo presenta datos comerciales que devuelve la API pública; no calcula disponibilidad ni modifica inventario.

La interfaz está implementada y preparada para servirse desde el dominio público confirmado. La iteración de identidad no cambió proxy, DNS ni Caddy. La ampliación V1.4 modifica el backend/esquema según [su contrato](modules/catalogo-v1-4.md). Las URL usadas por la UI son relativas al origen que sirve la página.

## Logos originales

Los cuatro JPEG aportados para Virtuosa Store se copiaron byte por byte, sin recortar, redibujar, recolorear ni eliminar fondos, a `src/ResellManager.Web/wwwroot/branding/virtuosa/`:

| Archivo | Original | Uso previsto |
| --- | --- | --- |
| `horizontal-black.jpg` | Imagen 1: logotipo horizontal completo con fondo negro | Referencia y uso únicamente sobre un fondo compatible. |
| `vertical-white.jpg` | Imagen 2: logotipo vertical completo sobre blanco | Variante completa para superficies claras donde haya espacio suficiente. |
| `icon-white.jpg` | Imagen 3: símbolo sobre blanco | Marca compacta del encabezado, incluida la vista móvil. |
| `icon-wine.jpg` | Imagen 4: símbolo en recuadro vino, con margen blanco | Icono de pestaña inicial de la tienda. |

El diseño reserva `wwwroot/branding/virtuosa/horizontal-transparent.webp` para una **exportación oficial futura** del logotipo horizontal con transparencia apta para el encabezado claro; también puede adaptarse la referencia a PNG o SVG oficial. Ese archivo aún no existe. El componente `MarcaTienda` acepta la ruta pública mediante `LogoHorizontalUrl`; hasta recibir el export oficial, el encabezado usa `icon-white.jpg` original con el nombre como texto accesible. En pantallas muy estrechas deja visible solo el icono. No se debe extraer el símbolo del JPEG horizontal ni reconstruirlo con CSS. Para un favicon definitivo conviene obtener del propietario un `.png` o `.ico` optimizado, derivado del arte original aprobado; los JPEG actuales permanecen intactos.

## Rutas y recursos públicos

La tienda usa estas lecturas existentes:

- `GET /catalogo` y `GET /catalogo/{productoId}` para las páginas, sin Identity.
- `GET /api/catalogo/productos` con `termino`, `categoriaId` y `marca` opcionales.
- `GET /api/catalogo/productos/{productoId}` y `GET /api/catalogo/productos/{productoId}/imagen`.
- `GET /api/catalogo/productos/{productoId}/imagenes/{imagenId}` para galería.

El buscador del encabezado usa el debounce de 300 ms del modelo actual en el listado; desde el detalle navega al catálogo filtrado al enviarse. La API decide qué productos siguen disponibles. El filtro de categorías usa los nombres e identificadores que recibe del listado público. Las imágenes proceden del endpoint público por ID; las rutas privadas del servidor y el endpoint administrativo de imágenes no se publican. No hay rutas de carrito, checkout, pagos ni pedidos online.

## Dominios y frontera pública

La fuente canónica de hosts, rutas y evidencia es
[Dominios](deployment/domains.md). El dominio público operativo es
`virtuosagt.com`, la administración permanece en `app.resellmanager.tech` y
`preview.newtonlab.dev` se usa para pruebas. No se migra administración a
`resellmanager.tech`.

En producción, `/` sirve el catálogo y `/producto/{id}` es la URL pública limpia.
Caddy la reescribe a la ruta Blazor interna `/catalogo/{id}`; el código no declara
`@page /producto/{id}`. `/catalogo` redirige a `/` y el host `www` redirige al
canónico. La especificación de proxy/AllowedHosts/TLS y circuito Blazor pertenece
a Deployment. Cualquier cambio futuro debe preservar preview y proteger rutas
privadas.

## Puntos para la siguiente etapa

- Incorporar una exportación horizontal transparente y un favicon optimizado proporcionados o aprobados por la marca en la carpeta indicada.
- Verificar configuración efectiva de los hosts y preview según [Dominios](deployment/domains.md); no crear otro inventario ni otra base para el catálogo.
- Revisar la calidad de nombres, descripciones y fotografías comerciales, porque provienen de los datos actuales del producto.
- La carga inicial/SEO requiere evaluación. **Portada por categorías, carruseles y paginación real ya están aprobados pero aún pendientes** en [ajustes UX V1.4](modules/catalogo-v1-4-ajustes-ux.md). La UI inicial todavía necesita conexión de Blazor para cargar productos y deriva las categorías del listado público completo.

## Experiencia V1.4

El detalle selecciona la portada, ofrece miniaturas diferidas y un visor de
pantalla completa con zoom, pan/pinch, swipe sin zoom, Escape y flechas. Mantiene
proporciones y no amplía imágenes pequeñas por defecto; bloquea scroll y restaura
foco al cerrar o desmontarse. Las tarjetas conservan contenedor 4:5 sin recorte.
El indicador Disponible utiliza verde y texto sin revelar cantidades.

El layout lleva `data-public-catalog="true"` y la rama contiene lógica de
reconexión discreta sin foco/backdrop, con Reintentar/Recargar y modal
administrativo conservado. **La captura de Preview del 08/10 mostró un modal
grande**, por lo que el [ajuste UX pendiente](modules/catalogo-v1-4-ajustes-ux.md)
exige comprobar versión desplegada/caché/detección antes de darlo por validado
físicamente; la captura no demuestra por sí sola un fallo de la rama.
El contacto WhatsApp usa configuración de servidor y URL canónica, descritas en
[V1.4](modules/catalogo-v1-4.md#implementación-y-configuración-v14).

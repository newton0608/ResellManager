# Virtuosa Store: identidad pública

Virtuosa Store es la identidad de la tienda pública de ResellManager. Las vistas `/catalogo` y `/catalogo/{productoId}` consumen el catálogo del mismo backend y el mismo inventario. El sistema administrativo conserva su identidad visual `ui-*`; los estilos de la tienda se definen con tokens y componentes `store-*`. La tienda solo presenta datos comerciales que devuelve la API pública; no calcula disponibilidad ni modifica inventario.

La interfaz está implementada y preparada para servirse desde el dominio público confirmado. La iteración de identidad no cambió proxy, DNS ni Caddy. V1.4 incorpora galería/jerarquía y [ajustes UX](modules/catalogo-v1-4-ajustes-ux.md) ya implementados, sin release ni despliegue de esa ampliación. Las URL usadas por la UI son relativas al origen que sirve la página; este documento no certifica qué versión ejecuta cada host.

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

Las páginas siguen siendo `GET /catalogo` y `GET /catalogo/{productoId}`, sin
Identity. La UI vigente consume escaparate, productos paginados, opciones y
contexto de categoría mediante las [lecturas públicas acotadas](25_CatalogoPublicoBackend.md#api).
El endpoint de array `/api/catalogo/productos` se conserva para compatibilidad;
no se usa para precargar productos ni construir filtros. Detalle, portada y
fotografías de galería mantienen sus rutas públicas por ID.

El buscador del encabezado conserva debounce de 300 ms; desde el detalle navega
al catálogo filtrado al enviarse. Busca sobre todos los productos elegibles,
combinado con los filtros activos, aunque no se hayan cargado sus carruseles.
La API decide qué productos siguen disponibles. Opciones de raíces, hijas y
marcas son lecturas públicas independientes. Las imágenes proceden del endpoint
público por ID; no se publican rutas privadas ni el endpoint administrativo.
No hay rutas de carrito, checkout, pagos ni pedidos online.

## Portada y navegación incremental V1.4

Sin filtros, la portada presenta bloques de tres raíces, cada una con carrusel
táctil de hasta diez productos directos o de sus hijas. «Ver todos» queda fuera
del área desplazable y abre el listado de la raíz. Las tarjetas mantienen 4:5,
`object-contain`, imágenes lazy y scroll horizontal con snap y controles
accesibles. Raíces, hijas y productos tienen orden determinista por ID ascendente.

El listado de raíz muestra su nombre, chips «Todos»/hijas públicas y regreso a
la portada. Raíz, hija, búsqueda y marca cargan dieciséis productos por página;
no se recorta un catálogo descargado completo. El sentinel usa
`IntersectionObserver`, con «Cargar más» como alternativa. La siguiente carga
muestra estado discreto, conserva resultados y permite reintentar el mismo
cursor ante error. Hay mensaje de cero resultados e indicador de fin.

Categoría principal y marca ofrecen opciones acotadas ampliables; las hijas
pertenecen a la raíz abierta. Los enlaces con `termino`, `categoriaId` y `marca`
restauran contexto, incluso con una hija fuera del primer bloque de opciones.
Cambiar filtros reinicia la paginación y descarta respuestas obsoletas; «Limpiar»
restablece la portada. La navegación conserva historial atrás/adelante.

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
- La carga inicial/SEO requiere evaluación. Portada por categorías, carruseles y paginación real ya están implementados en [ajustes UX V1.4](modules/catalogo-v1-4-ajustes-ux.md). La carga de datos todavía necesita JavaScript y el circuito Blazor; las opciones ya no dependen del listado público completo. QA físico de esta navegación sigue pendiente.

## Experiencia V1.4

El detalle selecciona la portada, ofrece miniaturas diferidas y un visor de
pantalla completa con zoom, pan/pinch, swipe sin zoom, Escape y flechas. Mantiene
proporciones y no amplía imágenes pequeñas por defecto; bloquea scroll y restaura
foco al cerrar o desmontarse. Las tarjetas conservan contenedor 4:5 sin recorte.
El indicador Disponible utiliza verde y texto sin revelar cantidades.

El layout lleva `data-public-catalog="true"`. La reconexión transitoria muestra
aviso mínimo con spinner, sin contador, foco ni backdrop; fallo/rechazo permiten
Reintentar/Recargar y administración conserva su modal. El mecanismo ahora
resincroniza al cambiar el layout, durante navegación mejorada de Blazor y al
restaurar historial. El [diagnóstico del ajuste UX](modules/catalogo-v1-4-ajustes-ux.md#8-diagnóstico-de-reconexión-y-límites-de-evidencia)
distingue el defecto local corregido de la captura de Preview del 08/10: su modal
grande no se reprodujo en Edge a 390 px y la causa exacta en el iPhone sigue sin
confirmarse. La prueba física de desconexión y caché sigue pendiente.
El contacto WhatsApp usa configuración de servidor y URL canónica, descritas en
[V1.4](modules/catalogo-v1-4.md#implementación-y-configuración-v14).

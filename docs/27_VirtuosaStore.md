# Virtuosa Store: identidad pública y preparación de dominio

Virtuosa Store es la identidad de la tienda pública de ResellManager. Las vistas `/catalogo` y `/catalogo/{productoId}` consumen el catálogo del mismo backend y el mismo inventario. El sistema administrativo conserva su identidad visual `ui-*`; los estilos de la tienda se definen con tokens y componentes `store-*`. La tienda solo presenta datos comerciales que devuelve la API pública; no calcula disponibilidad ni modifica inventario.

Esta etapa prepara la interfaz para funcionar en un dominio propio. No cambia el proxy, DNS, Caddy, el backend ni la base de datos. Las URL usadas por la UI son relativas al origen que sirve la página.

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
- `GET /api/catalogo/productos` con `termino` y `categoriaId` opcionales.
- `GET /api/catalogo/productos/{productoId}` y `GET /api/catalogo/productos/{productoId}/imagen`.

El buscador del encabezado usa el debounce de 300 ms del modelo actual en el listado; desde el detalle navega al catálogo filtrado al enviarse. La API decide qué productos siguen disponibles. El filtro de categorías usa los nombres e identificadores que recibe del listado público. Las imágenes proceden del endpoint público por ID; las rutas privadas del servidor y el endpoint administrativo de imágenes no se publican. No hay rutas de carrito, checkout, pagos ni pedidos online.

## Topología futura de dominios

| Host externo | Destino previsto | Rutas visibles |
| --- | --- | --- |
| `resellmanager.tech` | Administración de ResellManager | Aplicación administrativa y sus endpoints actuales, protegidos por Identity según corresponda. |
| Dominio futuro de Virtuosa Store | **La misma instancia** ASP.NET Core y la misma base de datos/almacenamiento de imágenes | Catálogo, API pública de catálogo y recursos indispensables para renderizarlo. |

El proxy deberá terminar TLS para ambos hosts y reenviar cada uno a la **misma aplicación interna**, manteniendo `Host` y el esquema externo correctos. La configuración de `AllowedHosts` del entorno productivo tendrá que aceptar ambos dominios, y las cabeceras reenviadas seguirán confiándose solo al proxy conocido. El host administrativo puede abrir la tienda en `/catalogo`; en el dominio de Virtuosa, la raíz `/` podrá redirigir a `/catalogo` en el proxy cuando se haga el despliegue, sin codificar nombres de host en los componentes.

Para limitar la superficie del dominio público, el proxy deberá enrutar las páginas `/catalogo` y sus detalles, las tres lecturas de `/api/catalogo/productos` y los recursos estáticos que consume la aplicación: `branding/virtuosa/*`, `app.css`, `css/tailwind.css`, `ResellManager.Web.styles.css`, `app.js`, `form-feedback.js`, `reconnect.js`, `catalogo-publico.js`, `_framework/*` y, si aparecen dependencias de componentes, `_content/*`. El renderizado `InteractiveServer` necesita también el circuito de Blazor (`/_blazor`, incluida negociación, WebSocket y reconexión); una regla que acepte solo GET a HTML e imágenes rompería la búsqueda y los filtros. El documento HTML actual carga algunos recursos compartidos con la administración; son archivos estáticos, no endpoints de datos administrativos.

Las rutas de administración, autenticación, comprobantes y sus API no deben enrutarse en el dominio público. La autorización de ASP.NET Core sigue protegiéndolas incluso detrás del proxy: la configuración por host añade una frontera de routing, no reemplaza Identity. Comprobar en un entorno de preparación los enlaces directos, los fallos 404 de producto/imagen, la conexión interactiva y la denegación de las rutas privadas desde el dominio público. Mantener las cookies de autenticación restringidas a su host y revisar cualquier política CSP de conexiones WebSocket al añadir el segundo dominio.

La configuración versionada actual de despliegue documenta `app.resellmanager.tech`; el destino administrativo solicitado aquí es `resellmanager.tech`. Antes de cambiar el enrutamiento real habrá que decidir la transición de host y actualizar la configuración productiva, certificados, `AllowedHosts` y pruebas operativas. **Este documento no cambia esa configuración.**

## Puntos para la siguiente etapa

- Incorporar una exportación horizontal transparente y un favicon optimizado proporcionados o aprobados por la marca en la carpeta indicada.
- Configurar y probar ambos hosts en el proxy, DNS y TLS con el mismo backend y los mismos volúmenes persistentes; no crear otro servicio de catálogo ni otra base de datos.
- Revisar la calidad de nombres, descripciones y fotografías comerciales, porque provienen de los datos actuales del producto.
- Evaluar carga inicial/SEO y paginación si el catálogo crece. La UI actual necesita conexión de Blazor para cargar productos y el filtro de categorías deriva del listado público completo.
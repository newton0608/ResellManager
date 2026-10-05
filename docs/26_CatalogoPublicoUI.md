# Catálogo público: primera UI

La primera vista pública permite explorar productos disponibles en `/catalogo` y consultar su detalle en `/catalogo/{productoId}` sin iniciar sesión. Usa exclusivamente la API de lectura descrita en [25_CatalogoPublicoBackend.md](25_CatalogoPublicoBackend.md).

El backend se incorporó en `9a119bb` y esta primera UI en `c345ad6`, sobre `feature/tailwind-ui`.

> Registro de la primera iteración visual, no especificación del diseño final. La identidad pública vigente se describe en [Virtuosa Store](27_VirtuosaStore.md), y los dominios/ruta canónica pendiente en [Dominios](deployment/domains.md). El estado integrado está en [Catálogo](modules/catalogo.md).

## Componentes y responsabilidades

Todos los componentes públicos están fuera de `Components/Pages`, cuyo `_Imports.razor` exige autenticación. Las dos páginas declaran `AllowAnonymous`, `InteractiveServer` y `CatalogoLayout`.

| Archivo | Responsabilidad |
| --- | --- |
| `Components/Catalogo/_Imports.razor` | Usings específicos de los componentes públicos. |
| `Components/Layout/CatalogoLayout.razor` | Encabezado, marca, navegación pública, enlace para saltar al contenido y pie. |
| `Components/Catalogo/Catalogo.razor` | Ruta del listado, parámetros de URL, ciclo de carga y callbacks. |
| `Components/Catalogo/CatalogoVista.razor` | Encabezado comercial, búsqueda, categoría, grid y estados. |
| `Components/Catalogo/ProductoCatalogoTarjeta.razor` | Enlace al detalle, nombre, categoría, precio e indicador recibido del backend. |
| `Components/Catalogo/ImagenCatalogo.razor` | Imagen pública y placeholder compartido. |
| `Components/Catalogo/CatalogoEstado.razor` | Mensajes de vacío/error, reintento y regreso. |
| `Components/Catalogo/CatalogoDetalle.razor` | Ruta de detalle, consulta y tratamiento de 404/error. |
| `Components/Catalogo/CatalogoDetalleVista.razor` | Presentación de descripción y atributos comerciales opcionales. |
| `Catalogo/ICatalogoPublicoClient.cs` y `CatalogoPublicoClient.cs` | Adaptador de lecturas HTTP del mismo origen mediante JS interop. |
| `Catalogo/CatalogoListadoModelo.cs` | Estado visual, debounce, cancelación y opciones públicas del filtro. |
| `Catalogo/CatalogoPresentacion.cs` | Formato de quetzales y atributos comerciales presentes. |
| `wwwroot/catalogo-publico.js` | GET a la API pública, parámetros, abort de consultas sustituidas y traducción del 404 de detalle a null. |

`Program.cs` registra el cliente scoped. La UI usa los DTOs de Application existentes; no introduce entidades persistentes ni modifica las capas Domain, Application o Infrastructure.

## Lecturas y disponibilidad

- Listado: `GET /api/catalogo/productos`, con `termino` y `categoriaId` cuando corresponda.
- Detalle: `GET /api/catalogo/productos/{productoId}`.
- Imagen: `GET /api/catalogo/productos/{productoId}/imagen`.

Las solicitudes JSON omiten credenciales y usan `cache: no-store`. La disponibilidad y los resultados de búsqueda/filtro vienen del backend. La UI no consulta unidades, no calcula stock y no reconstruye las reglas de reserva o venta. El precio mostrado es `PrecioPublico`, formateado como `Q 249.00`, con dos decimales y cultura `es-GT`.

Un 404 de detalle muestra un mensaje amigable y un enlace para volver al catálogo. Un error de red/servidor muestra un mensaje público con reintento; sus detalles técnicos solo se registran en el servidor.

## Búsqueda y categorías

La búsqueda espera 300 ms desde el último cambio, siguiendo el patrón de los buscadores existentes. El botón Buscar y Enter ejecutan la consulta inmediatamente. Cambiar categoría también consulta inmediatamente y conserva el término. Limpiar restablece ambos filtros y vuelve a leer el listado completo.

Se cancelan los trabajos anteriores y se ignoran sus respuestas o errores tardíos. El módulo HTTP aborta una lectura anterior cuando empieza otra del mismo tipo.

No existe un endpoint público independiente de categorías. La primera lectura completa ya proporciona `CategoriaId` y `Categoria`: de ella se obtienen opciones únicas, ordenadas por nombre, para categorías con productos publicados. Las opciones se conservan durante las búsquedas/filtros y se actualizan al volver a consultar sin filtros. No se usa `ICategoriaService` ni un endpoint administrativo.

Las URL `/catalogo?termino=...&categoriaId=...` admiten filtros iniciales. Un enlace de categoría desde el detalle usa ese mecanismo. Si la categoría seleccionada ya no está en el listado público, se muestra “Categoría no disponible”. El selector se recrea al cambiar su colección de opciones para conservar la selección tras la carga interactiva.

Los cambios hechos en el formulario no sincronizan la URL en esta primera versión; al volver desde el detalle se abre `/catalogo`.

## Diseño y responsive

Se reutilizan los tokens `ui-*`, el contenedor `.rm-ui` y los controles compartidos del sistema Tailwind actual, junto con el spinner existente. No se agregan hojas de estilo paralelas. `wwwroot/css/tailwind.css` se regenera mediante `npm run css:build`. Durante la revisión final aparecieron cambios concurrentes en `App.razor`, `Styles/tailwind.css` y la fuente tipográfica de `app.css`; también hubo ediciones concurrentes de los layouts administrativos. Se conservaron y el catálogo se adaptó a los tokens y controles compartidos, sin reescribir esos archivos.

El contenedor tiene ancho fluido, máximo `max-w-7xl` y espaciado adaptable. El grid muestra una columna por debajo de 360 px, dos desde 360 px, tres desde 768 px y cuatro desde 1280 px. Los filtros se apilan en móvil y se distribuyen en una fila desde 768 px. El detalle usa una columna en móvil y dos desde 768 px.

Se incluyen etiquetas de campos, foco de los controles existentes, estados accesibles de carga/error, imágenes decorativas en tarjetas y texto alternativo en detalle. Los esqueletos respetan la preferencia de movimiento reducido.

## Imágenes faltantes

La imagen tiene un marco estable con proporción 4:5 y `object-contain`. Si `TieneImagenPrincipal` es false, se presenta directamente el placeholder, sin solicitar imagen. Si la descarga falla, incluido 404, el evento `onerror` retira el elemento img y deja el mismo placeholder. Las tarjetas cargan imágenes de forma diferida; el detalle prioriza su imagen.

La URL solo contiene el ID público. El endpoint administrativo y las rutas físicas no se utilizan ni se muestran.

## Datos y próximos puntos a revisar

Los DTOs proporcionan todos los datos necesarios para esta vista: nombre, categoría, precio, indicador de imagen, disponibilidad, descripción y atributos opcionales. Los atributos vacíos se omiten. No se muestran identificadores internos, códigos de barras, costo, proveedor, compras ni unidades.

Antes de crecer el catálogo, conviene revisar:

1. Un contrato público de categorías y paginación si el volumen deja de permitir una lectura completa para las opciones del filtro. La solución actual depende del listado sin paginar existente.
2. La calidad comercial de nombres, descripciones y fotos: la publicación sigue dependiendo del inventario libre definido por el backend.
3. Renderizado inicial de productos y metadatos para SEO si se requiere. Esta versión prerenderiza estructura y carga; consulta datos después de activar Blazor y requiere JavaScript/conexión del circuito, como el frontend interactivo actual.
4. Conservación de filtros al navegar entre listado y detalle, si se desea en una iteración posterior.

Esta iteración se limita a lecturas de catálogo. No incorpora carrito, checkout, pedidos web, pagos, clientes públicos, favoritos, promociones, envíos ni cambios al inventario, esquema o migraciones.

## Archivos de esta implementación

Se crearon los archivos de `Components/Catalogo/` y `Catalogo/` indicados en la tabla, `Components/Layout/CatalogoLayout.razor`, `wwwroot/catalogo-publico.js`, `tests/ResellManager.Tests/CatalogoUiTests.cs`, `tests/ResellManager.Tests/HtmlPrueba.cs` y este documento. Se modificaron `Program.cs`, `README.md` y el CSS compilado `wwwroot/css/tailwind.css`.

Se ajustaron también assertions de títulos/enlaces en `DashboardTests.cs`, `AutenticacionIntegracionTests.cs` y `FormularioUxTests.cs` para permitir atributos CSS e iconos internos sin perder las comprobaciones de texto, destino y estructura responsive. El helper `HtmlPrueba` normaliza el texto de los elementos para esas comprobaciones. Los cambios concurrentes de App, estilos compartidos, Dashboard y layouts administrativos no se atribuyen a esta implementación. Las compilaciones y tests finales incluyen el estado compartido disponible al ejecutarlos.

## Verificación

Las pruebas en `CatalogoUiTests.cs` siguen la estrategia existente de xUnit, `HtmlRenderer`, dobles del cliente y `WebApplicationFactory` con SQLite aislado. Cubren tarjetas y campos comerciales, formato Q, debounce, filtros combinados e iniciales, opciones conservadas, respuestas tardías, carga, vacío, error/reintento, ausencia de imagen, detalle comercial, detalle 404, reutilización del módulo y acceso anónimo a ambas rutas.

Se comprobó además la UI con Chromium headless y seis productos ficticios en una base SQLite separada dentro de `.artifacts/catalogo-preview`, usando los servicios y el almacenamiento existentes. La revisión incluyó 320, 390, 820 y 1440 px, sin desbordamiento horizontal; búsqueda con una petición tras el debounce, filtros, enlaces al detalle, 404, archivo de imagen ausente y recuperación de error 503. No hubo errores JavaScript. Los datos y capturas de prueba están excluidos por `.gitignore`.

Resultado final:

- `npm run css:build`: correcto, Tailwind v4.3.3.
- `dotnet build ResellManager.sln --no-restore`: correcto, 0 advertencias y 0 errores.
- Suite completa: `dotnet test ResellManager.sln --no-build --no-restore`, 565 correctas, 0 fallidas y 0 omitidas; incluye 20 pruebas nuevas de UI y las 545 previas.
- Informe de tests: `.artifacts/test-results/catalogo-ui/suite-completa.trx`.
- `git diff --check`: correcto.

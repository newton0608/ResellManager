# V1.4 — Galería, exploración y UX del catálogo público

**Estado: decisión aprobada; implementación pendiente.** Alcance acordado el 08/10/2026.
**Base obligatoria de esta línea de trabajo:** tag `v1.3.0`, commit
`b9ab4eeabc2dd04b587e437243d6e27cb4b5e1e2`; rama
`feature/catalogo-v1-4`. Esta especificación es la fuente de verdad para Codex.
No interpretar estos requisitos como funcionalidades ya desplegadas.

## Objetivo y fronteras

Mejorar cómo se cargan, observan y encuentran los productos de Virtuosa Store
sin cambiar la operación comercial. La administración sigue siendo la única que
edita productos y categorías. El catálogo público sigue siendo **solo lectura**,
anónimo y sujeto a la misma regla actual de disponibilidad comercial: una unidad
`Disponible`, sin reserva y sin venta `Registrada`. No añadir carrito,
checkout, pedidos, pagos, WhatsApp, stock público, precios automáticos,
cambios de costos, ventas, inventario, códigos de barras ni servicios externos.
No alterar infraestructura, Caddy, DNS, Docker ni DeployManager.

Los cambios administrativos **necesarios** para fotografías y subcategorías
sí forman parte de V1.4; no rediseñar el resto de la administración. Conservar
el lenguaje visual de Virtuosa y el diseño `store-*`, separado de `ui-*`.

## 1. Galería de fotografías del producto

### Contrato funcional

- Cada `Producto` admite **0 a 8 fotografías en total**, no 8 adicionales.
  Una sola foto es válida; **no** exigir un mínimo de dos. Cuando hay fotos,
  hay exactamente **una portada** y un orden estable para las demás.
- La portada se muestra en tarjetas y detalle del catálogo. El detalle permite
  recorrer todas las imágenes ordenadas mediante miniaturas, botones y gestos
  táctiles apropiados. El administrador puede subir varias fotos, añadir hasta
  el límite, eliminar, cambiar portada y reordenar. Las fotos se asocian a
  `Producto`, **no** a `UnidadInventario`.
- Un producto con una foto existente antes de V1.4 conserva esa imagen como
  portada tras migrar y actualizar. El campo actual
  `Producto.ImagenPrincipalRuta` y el endpoint público existente
  `GET /api/catalogo/productos/{id}/imagen` deben seguir funcionando para
  compatibilidad. No duplicar archivos existentes ni perder referencias.
- Elegir una nueva portada actualiza el resultado del endpoint antiguo.
  Las imágenes adicionales se consultan mediante **identificadores opacos**
  o índices validados dentro del producto, nunca rutas físicas suministradas
  por el cliente. La API pública entrega metadatos mínimos ordenados e imágenes
  **solo si el producto sigue siendo comercialmente publicable**. Endpoints
  administrativos autenticados para edición; no permitir subida pública.
- La implementación puede incorporar una entidad/tabla de imágenes adicionales
  con `ProductoId`, ruta y orden; elegir el modelo que menos riesgo suponga
  para compatibilidad con `ImagenPrincipalRuta`, EF y servicios actuales.
  Se requiere **migración EF Core real** y snapshot coherente; no crear SQL
  manual como sustituto. Migración hacia delante sin pérdida de imágenes,
  ventas, compras, categorías ni datos históricos. No modificar migraciones
  históricas. Eliminar/renombrar archivos solo tras persistir la referencia
  correspondiente y compensar errores, siguiendo el contrato vigente.
- Reutilizar `IAlmacenamientoImagenesProducto`, validación de firma,
  decodificación, orientación EXIF, límites de entrada, procesamiento WebP,
  rutas privadas y mitigaciones de traversal. No guardar binarios en SQLite,
  no aceptar SVG ejecutable ni confiar en extensiones/nombres del usuario.
  Evitar archivos huérfanos y borrado de archivos aún referenciados.
  No descargar imágenes externas adicionales automáticamente.
- Priorizar lectura de etiquetas pequeñas: no estirar fotos de baja resolución;
  evaluar tamaño de salida del pipeline actual (máximo 1200 px de lado mayor)
  y, si hace falta, permitir para **nuevas** imágenes de galería una variante
  acotada de hasta 2000 px con WebP de calidad adecuada, sin degradar ni
  reprocesar fotos existentes. Mantener límites de memoria, tamaño de subida
  y almacenamiento; cargar miniaturas de manera diferida y evitar descargar
  ocho imágenes grandes al abrir el listado.
- Mantener contenedor **4:5** y `object-contain` en tarjetas para no recortar
  envases/etiquetas. Fotografías 4:5 (p. ej. 1200 × 1500) son recomendación
  editorial, **no** requisito ni recorte automático obligatorio.
- En el alta asistida por código de barras, la imagen externa aceptada sigue
  siendo como máximo **una portada** y solo se persiste al guardar, según
  `docs/modules/productos-lookup-codigo-barras.md`. El flujo manual prevalece.
  No romper edición, altas desde Compra ni la creación sin imágenes.

### Visor ampliado

- Al tocar/clicar la imagen principal del **detalle**, abrir un visor de
  pantalla completa (lightbox) con navegación por todas las fotos y zoom.
- Móvil: pellizcar para ampliar, desplazarse por la imagen ampliada y deslizar
  entre fotos cuando no se está haciendo pan/zoom; escritorio: clic o controles
  de zoom, anterior/siguiente, cerrar; teclado: Escape y flechas.
- Mantener proporción, sin `object-cover` que corte la tabla nutricional.
  Evitar que el gesto de deslizar cambie de imagen mientras se hace zoom.
- Accesibilidad: foco inicial y restauración al cerrar, nombres accesibles,
  controles táctiles suficientes, texto alternativo útil, navegación de teclado,
  bloqueo del scroll de fondo mientras está abierto. Placeholder si falla una
  imagen, sin romper el resto de la galería.

## 2. Subcategorías — máximo dos niveles

- Extender `Categoria` con `CategoriaPadreId` **opcional**. Categorías
  existentes quedan como raíces (`NULL`), sin cambiar sus productos.
  Un hijo pertenece a una raíz; **no** permitir nietos, ciclos, autorreferencia
  ni convertir en hija una categoría que ya tenga hijas.
- Administración: crear/editar categoría seleccionando opcionalmente una
  categoría padre válida; mostrar jerarquía en listas/selectores de Producto,
  sin impedir que un Producto pertenezca a una categoría raíz.
  Conservar contratos y consumidores existentes donde sea posible.
- Ejemplo: Salud y bienestar → Vitaminas y suplementos / Cuidado personal.
  Son ejemplos, **no** crear categorías de prueba en datos reales.
- Catálogo: filtro de categoría raíz incluye productos de la raíz **y** sus
  subcategorías; filtro de hija incluye solo sus productos. Buscar + categoría
  + marca se combinan mediante AND.
- Mostrar opciones jerárquicas claras en móvil y escritorio. Las opciones
  públicas solo deben revelar categorías que tengan al menos un producto
  comercialmente publicable, directa o indirectamente; nunca categorías
  administrativas vacías/sin publicaciones. No introducir profundidad > 2.
- Validar jerarquía en servicio/backend (no solo en Razor) y cubrir casos de
  categorías inexistentes, cambios de padre, eliminación/cambios incompatibles
  con relaciones existentes. Conservar política actual de borrado restrictivo.

## 3. Filtro público por marca

- `Producto.Marca` **ya existe**. Reutilizarlo: **no** crear una tabla de
  marcas ni modificar su captura administrativa solo para filtrar.
- Incluir la marca en el DTO del listado público y aceptar parámetro opcional
  `marca` en la API de listado, combinado con `termino` y `categoriaId`.
  Normalizar para comparación `Trim` e ignorar mayúsculas/minúsculas sin
  cambiar el valor almacenado ni introducir matching difuso.
- Solo ofrecer marcas no vacías de productos actualmente publicables.
  Consolidar variaciones de espacios/caso para evitar duplicados visuales.
  Opciones disponibles estables al aplicar otros filtros, sin consultar
  productos no publicados ni usar servicios administrativos.
- Mantener búsqueda con debounce, estados de carga/error, cancelar respuestas
  tardías y URLs compartibles (`?termino=...&categoriaId=...&marca=...`).
  Limpiar elimina los tres filtros; conservar filtros al cambiar entre ellos.

## 4. Reconexión discreta **solo** en catálogo público

- El diálogo de reconexión actual es **global**, se monta en
  `Components/App.razor`, usa `ReconnectModal.razor` y
  `wwwroot/reconnect.js`; su `showModal()` bloquea la pantalla.
- En las páginas públicas de Virtuosa, el estado transitorio
  **«Reconectando…»** debe presentarse como aviso pequeño, no modal,
  sin backdrop ni captura de foco, sin desplazar contenido ni ocultar el
  producto; accesible mediante `role=status` / `aria-live=polite`.
  Al reconectar desaparece automáticamente. No simular conexión restaurada.
- Ante fallo persistente/rechazo, mostrar un aviso suficientemente visible
  con **Reintentar** o **Recargar** según el estado, funcional aun sin circuito
  Blazor. Evitar avisos que desaparezcan sin alternativa cuando falla.
- La detección de catálogo debe ser robusta en Preview
  (`/catalogo`, `/catalogo/{id}`) **y** en el dominio público con rutas
  reescritas (`/`, `/producto/{id}`); no basarse únicamente en prefijos
  `/catalogo` ni afectar accidentalmente páginas administrativas.
  Preferir una marca de layout público verificable en DOM o mecanismo
  equivalente, sin acoplar la lógica a hosts hardcodeados.
- **No modificar apariencia, accesibilidad ni comportamiento de reconexión
  de las pantallas administrativas.** No sustituir el mecanismo de reconexión
  de Blazor ni crear polling permanente.

## 5. Indicador de disponibilidad

- Cambiar el punto/acento rojo por **verde** en el indicador `Disponible`
  de Virtuosa. Mantener el texto legible, contraste accesible y la misma
  regla de elegibilidad que devuelve el backend.
- No deducir stock a partir del color ni publicar cantidad de unidades.
  No modificar los colores semánticos de administración.

## Contratos técnicos y compatibilidad

- Seguir capas `Domain/Application/Infrastructure/Web` y `AGENTS.md`.
  Consultas y validaciones en servicios, no en componentes Razor.
- Revisar y extender `ICatalogoPublicoService`, DTOs, endpoints,
  `CatalogoPublicoClient`, `CatalogoListadoModelo`, componentes `store-*`,
  gestión de categorías/productos y almacenamiento según lo necesario.
- No romper los endpoints públicos existentes ni los enlaces canónicos
  `/catalogo` y `/catalogo/{id}` dentro de la app; respetar el
  rewrite/redirect existente del dominio de tienda sin tocar Caddy.
- Seguridad: `AllowAnonymous` solo en lecturas públicas elegibles;
  escritura autenticada y protegida. No exponer rutas, archivos privados,
  IDs de unidades, precios de costo ni categorías/marcas sin publicaciones.
- Usar SQLite de pruebas aislada, migración EF verificable, rollback de
  operaciones fallidas con archivos y límites de upload; nunca tocar datos
  de producción ni despliegues reales durante implementación.

## Criterios de aceptación y QA

1. Producto existente con 1 imagen: tras migración mantiene portada,
   endpoint anterior y miniatura; producto sin imágenes conserva fallback.
2. Crear producto con 0, 1, 2 y 8 fotos; intentar la novena se rechaza
   **sin persistencia parcial**. Agregar, quitar, reordenar y cambiar portada;
   mantener consistencia en UI, API, DB y archivos incluso ante fallos.
3. Portada de catálogo y galería del detalle correctas; leer foto del reverso
   de un envase ampliada; zoom, pinch/pan, swipe, teclado, Escape y foco.
4. Imágenes no accesibles si el producto no es publicable; sin traversal,
   referencias ajenas, bypass de auth ni filtración de rutas privadas.
5. Categorías existentes siguen como raíces. Crear hija válida; impedir
   nietos/ciclos; filtrar raíz/hija con inventario publicable.
6. Marca sin dato no aparece como filtro; marcas duplicadas por casing
   se consolidan; búsqueda + categoría + marca funciona y URL restaura estado.
7. En Preview y dominio público simulado, reconexión breve muestra aviso
   discreto; fallo/rechazo permite acción. Administración conserva el modal.
8. Indicador `Disponible` verde con texto y contraste; sin cambios en
   precio, reservas, venta, compra ni inventario.
9. QA responsive al menos 320, 390, 768 y 1440 px; Safari/iPhone real
   pendiente hasta validación física documentada. Emulación no la sustituye.
10. `dotnet build ResellManager.sln`, tests focalizados y suite completa
    por migraciones/contratos compartidos, `npm run test:js`,
    `npm run css:build`, `git diff --check`; reportar resultados reales.

## Estrategia de trabajo y cierre para Codex

1. Confirmar rama `feature/catalogo-v1-4` basada en `v1.3.0`; revisar
   `git status` y `git fetch`. **No** hacer merge/rebase/cherry-pick de
   `develop` ni de `feature/scanner-operativo-v2-1` para resolver la V1.4.
   Si hay cambios locales ajenos, conservarlos y reportar bloqueo.
2. Implementar por bloques verificables: persistencia/servicios de galería,
   administración, API/UI pública y visor, subcategorías, marca, reconexión,
   indicador y QA. Commits descriptivos; sin cambios no relacionados.
3. Actualizar documentación **después de implementar**: este contrato y
   `docs/modules/catalogo.md`, `docs/modules/productos.md`,
   `docs/24_ImagenPrincipalProducto.md`, `docs/25_CatalogoPublicoBackend.md`,
   `docs/27_VirtuosaStore.md`, `docs/19_V2_Pendientes.md`,
   `docs/09_Backlog.md`, `ROADMAP.md`, `CHANGELOG.md` cuando aplique.
   Conservar resultados históricos y distinguir pruebas automáticas de QA real.
4. Ejecutar validaciones, registrar limitaciones y hacer push de **esta rama**.
   No crear tag, no publicar V1.4, no desplegar, no fusionar con `main`
   o `develop` sin aprobación posterior. Resolver eventuales conflictos
   con V2.1 **en una tarea separada**, no aquí.

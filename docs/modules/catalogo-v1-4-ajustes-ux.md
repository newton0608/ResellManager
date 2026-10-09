# V1.4 — Ajustes de UX tras revisión en iPhone (iteración pendiente)

**Estado: requisitos aprobados el 08/10/2026; NO implementados todavía.**
Se aplicarán exclusivamente a `feature/catalogo-v1-4`, creada desde
`v1.3.0` (`b9ab4ee`), **después** de la implementación inicial de V1.4.
Este documento describe decisiones aprobadas, no evidencia de despliegue, release ni pruebas.

**Fuentes:** [contrato y validación de V1.4](catalogo-v1-4.md),
[catálogo público](catalogo.md), [productos/categorías](productos.md) y
[guía de agentes](../../AGENTS.md). Este documento es el contrato **aprobado
para la siguiente iteración de V1.4**; prevalece en los cuatro comportamientos
que amplía o sustituye de la implementación inicial. El resto de V1.4 se conserva.

## Motivo y estado actual verificado

La revisión visual del catálogo en `preview.newtonlab.dev` mostró un diálogo
grande de «Reconectando…», con fondo oscurecido, que bloquea la exploración.
El usuario confirmó que las tarjetas con «Imagen no disponible» corresponden
a productos **sin fotografías registradas**: no abrir una incidencia de imágenes
rotas ni generar imágenes ficticias por esa captura.

Inspección de la rama antes de esta iteración:

- `CatalogoListadoModelo` obtiene el **listado público completo** para
  construir categorías/marcas y, sin filtros, mostrar todas las tarjetas.
  `CatalogoVista` representa todos los resultados; las imágenes de tarjeta
  usan `loading="lazy"`, pero **los datos no están paginados**.
- El selector público combina raíces e hijas en un único desplegable.
- `ProductoForm` usa un selector único de categorías jerárquicas y muestra
  «Preparando fotografías...» o «Guardando...» sin progreso por archivo.
- `ReconnectModal` ya incluye aviso público pequeño y `reconnect.js`
  intenta alternarlo según `data-public-catalog="true"`; la captura de Preview
  no permite concluir si hay un despliegue antiguo, caché o fallo de detección.
  **Diagnosticar antes de cambiar el mecanismo.**

## 1. Inicio del catálogo: escaparate por categorías raíz

### Experiencia aprobada

- Sin búsqueda ni filtros, `/catalogo` es una **portada por categorías**,
  no una cuadrícula única con todos los productos.
- Mostrar únicamente **categorías raíz** con al menos un producto
  comercialmente publicable, directo o en cualquiera de sus hijas.
  No listar subcategorías como secciones independientes en la portada.
- Cada sección tiene título de categoría raíz, carrusel horizontal táctil de
  **hasta 10 productos** y acción clara «Ver todos» que abre la vista de esa raíz.
  Sus diez artículos se seleccionan entre los de la raíz **y sus hijas**.
- Orden de categorías y productos estable, documentado y determinista
  (no inventar fechas «más recientes» si el modelo no las posee).
  Evitar duplicados dentro de un carrusel; si un producto pertenece a una
  única categoría, se presenta bajo su raíz correspondiente.
- Con muchas raíces, **no precargar todos los carruseles ni todos sus
  productos**: cargar un grupo acotado de secciones inicialmente y el resto
  conforme se acerquen a la pantalla, con consultas acotadas al servidor.
  Una raíz vacía por disponibilidad no se muestra.
- Mantener diseño minimalista de Virtuosa, tarjetas **4:5 con
  `object-contain`**, imágenes lazy, swipe/scroll horizontal,
  `scroll-snap` y controles de navegación accesibles cuando correspondan.
  No causar scroll horizontal de toda la página; en móvil no exigir
  arrastrar con precisión para llegar a «Ver todos».
- Enlace a producto conserva su comportamiento y la galería de V1.4.
  El estado sin productos, carga y error deben seguir siendo útiles.

### Navegación

- Pulsar «Ver todos» o una categoría raíz muestra un **listado de esa raíz**
  (incluye productos asignados directamente y a sus hijas).
- Sobre el listado aparecen chips/pestañas de subcategorías publicables:
  **«Todos»** (seleccionado inicialmente) y solo las hijas de esa raíz con
  productos públicos. Al pulsar una hija, mostrar solo sus artículos.
- Mostrar nombre de raíz, estado seleccionado y una acción comprensible para
  regresar a todas las categorías/portada; no exigir volver mediante navegador.
- No repetir el selector plano raíz+hijas. Si existe un selector para
  accesibilidad o escritorio, debe ser dependiente/jerárquico y no una lista
  gigantesca.
- Búsqueda global desde el encabezado **busca en todo el catálogo público**,
  no solo en los primeros diez productos ni en secciones ya cargadas.
  La búsqueda conduce a resultados paginados. El filtro de marca sigue
  disponible y combinable con búsqueda/categoría/hija, sin cambiar
  `Producto.Marca` ni crear una tabla de marcas.
- Preservar enlaces compartibles con `termino`, `categoriaId` y `marca`;
  el `categoriaId` raíz representa raíz + hijas y el `categoriaId` hijo
  representa solo esa hija. Al abrir una URL con hija, reconstruir la raíz,
  sus chips y el seleccionado; `Limpiar` restablece la portada sin filtros.
  No romper `/catalogo/{id}` ni las rutas canónicas/rewrite de la tienda.

## 2. Listados: paginación real e incremento al desplazarse

- Para raíz, hija, búsqueda y marca, mostrar **16 productos inicialmente**
  y cargar el siguiente bloque de **16** al acercarse al final. No descargar
  toda la tabla y recortarla en Blazor/JS.
- La paginación debe ejecutarse **en la consulta del backend/SQLite** con
  filtro de elegibilidad comercial aplicado **antes** de limitar. No
  filtrar una página incompleta en memoria ni exponer productos no elegibles.
- Orden estable y desempate único; elegir cursor/keyset cuando resulte
  apropiado o paginación equivalente documentada. Evitar duplicados o
  saltos entre páginas en condiciones normales. Si cambia el inventario
  durante la navegación, no mostrar datos prohibidos ni asumir un snapshot
  permanente.
- Respuesta paginada con indicador fiable de continuación (`hasMore`,
  `nextCursor` o equivalente). Limitar y validar el tamaño solicitado en
  servidor. **Conservar compatibilidad** del endpoint antiguo
  `GET /api/catalogo/productos` para sus consumidores actuales; se puede
  introducir un endpoint nuevo o parámetros opt-in sin cambiar silenciosamente
  el formato de respuesta previo. La UI nueva **no** debe llamar al endpoint
  sin paginación para obtener el universo completo.
- Opciones públicas de categorías y marcas mediante consultas/contratos
  **acotados e independientes** del listado completo, derivadas únicamente
  de productos publicables. No construir filtros solicitando todos los DTO
  de producto. Cargar el resumen/índice de raíces de forma incremental si
  el número de categorías lo amerita; evitar N+1 por sección.
- Preferir `IntersectionObserver` para el sentinel inferior y para diferir
  carruseles; ofrecer también botón «Cargar más» como alternativa funcional
  cuando el observer no se active o para navegación por teclado.
- Skeletons discretos, estados de carga por sección/página, retry que
  **conserva lo ya cargado**, indicador de fin y mensaje de cero resultados.
  No bloquear la pantalla con un spinner general al pedir otra página.
- Al cambiar filtros o categoría, cancelar/ignorar solicitudes obsoletas,
  reiniciar cursor y resultados, evitar duplicados y no mezclar páginas de
  filtros anteriores. Evitar ráfagas simultáneas del observer.
- Reducir datos e imágenes transferidos: listado solo necesita portada,
  nunca las ocho fotos. Conservar límites de imagen, cache/privacy y
  seguridad de V1.4.

## 3. Administración: categorías dependientes y progreso real de fotos

### Selección de categoría en formularios de Producto

- Sustituir el selector administrativo único por **Categoría principal**
  (solo raíces) y **Subcategoría** (opcional; solo hijas de la raíz elegida).
- Un producto puede pertenecer a la raíz directamente: sin subcategoría,
  persistir el ID de la raíz. Con hija seleccionada, persistir el ID de la hija
  en el mismo `Producto.CategoriaId`; **no** agregar columnas redundantes.
- En edición de producto existente con hija, preseleccionar correctamente
  la raíz y la hija; con raíz, dejar la hija vacía. Al cambiar la raíz,
  limpiar cualquier hija anterior y actualizar sus opciones.
- Validar en servidor la pertenencia real hija→raíz y la categoría final;
  mantener el máximo de dos niveles y los contratos existentes.
- Aplicar al formulario reutilizado en **Nuevo/Editar producto y alta desde
  Compra**, sin rediseñar el módulo de Categorías ni cambiar las reglas de
  eliminación de categorías.

### Preparación, subida y guardado de fotografías

- Mostrar indicador circular animado y texto visible, por ejemplo:
  **«Preparando foto 1 de 5»** al leer/validar las seleccionadas y
  **«Subiendo foto 1 de 5»** cuando efectivamente se transfieren.
  Durante procesamiento/persistencia, **«Guardando foto 1 de 5»** solo si
  el servicio puede notificar el avance real por archivo.
- El contador `n/total` **debe reflejar progreso verificable** de la etapa
  indicada, no temporizadores, porcentajes simulados ni «5/5» antes de
  confirmar éxito. Si una etapa es atómica o no permite progreso por foto,
  mostrar spinner con **«Guardando fotografías…»** sin contador inventado.
- Indicar finalización real o error claro; permitir reintentar sin duplicar
  fotos ni perder las ya seleccionadas. Bloquear solo acciones que podrían
  corromper la operación en curso; no crear un modal de pantalla completa.
- Mantener límite **0–8 fotos totales**, una portada cuando hay fotos,
  validación de tamaño/tipo, cancelación de lotes inválidos, compensación
  de archivos y persistencia consistente. La mejora es de observabilidad,
  no un permiso para guardar parcialmente.
- Accesibilidad: `role=status` / `aria-live=polite` sin anunciar cada
  actualización excesivamente; spinner no es el único indicador, respetar
  movimiento reducido, 320 px sin overflow.
- Distinguir explícitamente la preparación local/SignalR del guardado real:
  no etiquetar como «subida completada» lo que aún no llegó al servidor.

## 4. Reconexión pública: corregir causa y reducir presencia visual

- La captura real de Preview (08/10/2026, navegador móvil) muestra
  **modal grande**, backdrop y «Intento 11 de 30» sobre la tienda.
  Esto incumple la experiencia acordada aunque el código de la rama ya
  incluya un aviso `#store-reconnect`. **Primero verificar qué commit está
  desplegado en Preview, assets/cache y la detección de
  `data-public-catalog="true"` durante pérdida de conexión.**
  No afirmar un bug de la rama sin reproducirlo en su build.
- Para cualquier ruta pública de Virtuosa, durante reconexión transitoria:
  **solo aviso muy pequeño y no modal** («Reconectando…» + spinner discreto),
  en una esquina segura respetando safe areas; **sin** oscurecimiento,
  foco capturado, contador de intentos, texto técnico o bloqueo de lectura.
  Al restablecer, desaparecer.
- Tras fallo definitivo/rechazo, aviso compacto pero identificable, con
  «Reintentar»/«Recargar» funcional incluso con circuito caído. No ocultar
  permanentemente la recuperación por hacerla discreta.
- No tocar el comportamiento visual/funcional de reconexión de **administración**.
  Mantener los estados oficiales de Blazor y evitar polling artificial.
- Verificar rutas Preview `/catalogo` y `/catalogo/{id}`, y rutas públicas
  reescritas `/` y `/producto/{id}`. Diagnosticar también navegación
  mejorada, back/forward, caché y reconexión en Safari/iPhone.

## 5. Límites y compatibilidad

- El catálogo sigue siendo **anónimo y solo lectura**; ninguna escritura,
  checkout, carrito, pedidos, reservas o confirmación automática por WhatsApp.
- La elegibilidad no cambia: unidad `Disponible`, sin reserva y sin venta
  `Registrada`. No publicar existencias, IDs de unidades, costos, rutas
  privadas ni categorías sin publicaciones.
- No tocar infraestructura, Caddy, Docker, DNS, DeployManager, secretos,
  producción ni la rama `feature/scanner-operativo-v2-1`.
- Preservar galería/zoom, WhatsApp configurable, disponibilidad verde,
  rutas públicas, estilos `store-*` separados de `ui-*`, pruebas
  históricas y compatibilidad de DTO/consumidores actuales.
- Implementar cambios de contratos y consultas en Application/Infrastructure,
  endpoints/JS/UI en Web; no poner paginación simulada o reglas de
  disponibilidad en componentes Razor. Evitar nuevas dependencias salvo
  justificación técnica.

## 6. Criterios de aceptación y pruebas para la iteración

1. Con 0, 1, 10 y más de 10 artículos por raíz, la portada muestra
   **máximo 10** por carrusel y «Ver todos» lleva al conjunto completo
   paginado; las hijas no aparecen como secciones independientes.
2. Raíz con productos directos e hijas: «Todos» incluye ambos; una hija
   muestra solo los suyos; raíz con publicaciones solo en hijas aparece;
   categorías vacías/no publicables nunca se exponen.
3. Listado con más de 32 productos: primeras 16 tarjetas y siguientes
   16 al avanzar; backend limita consulta y payload; sin llamadas al
   listado total para construir filtros o carruseles.
4. Scroll infinito y botón «Cargar más»: sin duplicados, carrera de
   solicitudes, bloqueo de pantalla ni pérdida de resultados al fallar
   la siguiente página. Estado de fin correcto.
5. Búsqueda global, marca y categoría/hija se combinan; URL compartida,
   recarga y navegación atrás restauran contexto; cambiar filtros
   reinicia paginación. Productos que dejan de ser publicables no se filtran
   solo en el cliente.
6. Producto existente con categoría hija: edición preselecciona raíz/hija;
   cambiar raíz limpia hija; guardar raíz sin hija y alta desde Compra
   funcionan; no se aceptan jerarquías inválidas.
7. Selección de 5 fotos muestra avance **real** `1/5 … 5/5` en etapa
   instrumentada; subida/guardado sin avance disponible se rotula
   honestamente. Casos 0, 1, 8, novena foto, archivo inválido, error de
   transferencia y guardado fallido preservan consistencia.
8. Reconexión simulada y física en Preview y rutas públicas: nunca
   aparece modal/backdrop del admin sobre Virtuosa; estado persistente
   permite recuperar; admin sigue mostrando su modal. Registrar la
   causa del modal observado (build antiguo, detección o defecto real).
9. UI responsive 320/390/768/1440 px: carruseles táctiles, chips
   legibles, keyboard/lectores de pantalla, movimiento reducido y
   ausencia de overflow horizontal; verificar Safari/iPhone real y
   Android cuando estén disponibles.
10. `dotnet build ResellManager.sln`, pruebas focalizadas y suite
    completa (cambios API/servicios), `npm run test:js`,
    `npm run css:build`, QA responsive, enlaces y `git diff --check`.
    **No reutilizar 894/123/76 como evidencia de esta iteración**;
    conservarlos como resultados históricos de V1.4 inicial.

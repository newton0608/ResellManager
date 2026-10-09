# V1.4 — Ajustes de UX tras revisión en iPhone

**Estado: implementación y validación automática completadas; QA exploratorio parcial en iPhone satisfactorio y verificaciones físicas específicas pendientes.**
Requisitos aprobados el 08/10/2026 y completados sobre la implementación inicial
V1.4, cuya base histórica es `v1.3.0` (`b9ab4ee`). Sin release ni despliegue.
Este documento registra el contrato aprobado y su funcionamiento final; los
resultados históricos de V1.4 inicial no certifican esta iteración.

**Fuentes:** [contrato y validación de V1.4](catalogo-v1-4.md),
[catálogo público](catalogo.md), [productos/categorías](productos.md) y
[guía de agentes](../../AGENTS.md). Este documento es el contrato **aprobado
para los ajustes UX de V1.4, ya implementados**; prevalece en los cuatro comportamientos
que amplía o sustituye de la implementación inicial. El resto de V1.4 se conserva.

## Motivo y estado previo de la iteración

La revisión visual del catálogo en `preview.newtonlab.dev` mostró un diálogo
grande de «Reconectando…», con fondo oscurecido, que bloquea la exploración.
El usuario confirmó que las tarjetas con «Imagen no disponible» corresponden
a productos **sin fotografías registradas**: no abrir una incidencia de imágenes
rotas ni generar imágenes ficticias por esa captura.

Registro histórico anterior a los ajustes; ya no describe el comportamiento vigente:

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
- El alcance excluye cambios de infraestructura, Caddy, Docker, DNS,
  DeployManager, secretos y producción; V2.1 sigue siendo una evolución separada.
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
10. Compilación de la solución, pruebas focalizadas y suite .NET completa
    por cambios de API/servicios; regresiones JS, generación de CSS,
    QA responsive, enlaces y revisión del diff sin errores de whitespace.
    **894/123/76 son resultados históricos de V1.4 inicial**, no evidencia
    de esta iteración.

## 7. Funcionamiento implementado

La portada obtiene **tres raíces por bloque**, cada una con hasta diez tarjetas.
Raíces, hijas y productos se ordenan por `Id` ascendente; ese identificador es
el cursor único para continuar, sin atribuir al modelo una fecha de publicación.
Una consulta obtiene las raíces y otra resuelve conjuntamente los carruseles
mediante existencia del décimo producto anterior elegible; no hay una consulta
por sección ni materialización del catálogo completo. Una raíz que pierde sus
publicaciones entre ambas lecturas se omite, conservando el cursor y la
continuación aunque todo ese bloque quede vacío.

Raíz, hija, búsqueda y marca utilizan páginas de **16** productos. SQLite aplica
la elegibilidad y todos los filtros, incluida marca, antes del límite; una fila
extra determina `hasMore`, y `nextCursor` permite avanzar. Cada lectura revalida
la disponibilidad vigente: el cursor no establece un snapshot ni una reserva.
Las tarjetas reciben únicamente la portada, sin metadatos ni fotos de galería.
El endpoint antiguo de array conserva formato, firma y orden para consumidores
anteriores; la UI vigente consume exclusivamente las nuevas lecturas acotadas.
Contrato de rutas, DTOs y normalización: [backend público](../25_CatalogoPublicoBackend.md#navegación-y-paginación-v14).

Raíces, marcas e hijas tienen opciones independientes, también paginadas en
bloques de hasta dieciséis. La marca se agrupa y filtra mediante comparación
Unicode `Trim` + `OrdinalIgnoreCase` dentro de SQLite, sin alterar su valor
persistido. El contexto de una categoría reconstruye raíz e hija seleccionada
al abrir una URL, incluso si esa hija queda fuera del primer bloque de opciones.
Las opciones no se restringen al resultado de la búsqueda o marca seleccionada.

`IntersectionObserver` solicita el siguiente bloque al acercarse a su sentinel;
«Cargar más» conserva una alternativa de teclado y para navegadores donde no se
active. Una petición adicional mantiene las tarjetas previas; su error permite
reintentar el mismo cursor. El modelo evita solicitudes simultáneas, deduplica
por ID e ignora respuestas obsoletas al cambiar filtros. El listado de raíz
muestra «Todos», chips de hijas públicas y regreso a todas las categorías.
Las URL conservan los tres filtros y el historial de navegación.

El formulario reutilizado de Producto muestra categoría principal y subcategoría
opcional, precarga ambas desde el ID existente y limpia la hija al cambiar raíz.
`ProductoInput.CategoriaPrincipalId` es un contexto opcional de validación,
compatible con los consumidores anteriores; sólo `Producto.CategoriaId` se
persiste. El servicio comprueba categoría final, raíz real y pertenencia hija→raíz,
además del límite de dos niveles. No hay columnas, migraciones ni dependencias
nuevas para estos ajustes.

La preparación muestra `Preparando foto n de total`; la lectura real de cada
`IBrowserFile` por SignalR muestra `Subiendo foto n de total`. Tras recibirla,
vuelve a preparación para comprobar firma y conservar la selección en memoria.
Esto aún no significa persistencia. El guardado transaccional sigue mostrando
**«Guardando fotografías…»**, sin contador por foto porque el servicio no publica
ese avance. Un fallo de transferencia permite reintentar el lote conservado,
y un guardado fallido conserva las fotos preparadas. Lotes inválidos no añaden
fotografías parcialmente; portada, límite y compensación de archivos se mantienen.
Contrato detallado: [gestión de fotografías](../24_ImagenPrincipalProducto.md#progreso-y-reintento-en-el-formulario).

La reconexión pública transitoria muestra sólo aviso pequeño en esquina segura,
spinner y «Reconectando…», sin contador, foco ni backdrop. Fallo y rechazo
conservan las acciones nativas de recuperación, aun con el circuito caído.
El modal administrativo y los estados oficiales de Blazor se mantienen.

## 8. Diagnóstico de reconexión y límites de evidencia

La inspección anónima de Preview encontró `data-public-catalog="true"`, el aviso
público y assets de reconexión coincidentes con la implementación inicial V1.4.
No permitió identificar de forma verificable el SHA desplegado ni la caché del
iPhone de la captura. En Edge a 390 px, con pérdida y restauración real del
circuito provocada desde el navegador, el modal grande **no se reprodujo**.
Por ello no se atribuye aquella captura a un build antiguo ni a una causa exacta
que no haya sido demostrada; la comprobación física del navegador afectado sigue
pendiente. Esta inspección no modificó el despliegue.

Sí se reprodujo un defecto local: al cambiar entre layout administrativo y público
manteniendo la misma clase de reconexión, el observador original de clases no
recalculaba la presentación. El diálogo podía seguir abierto aunque la marca
pública ya estuviera presente. La evidencia local
`.artifacts/reconnect-ux/layout-diagnostic.json` registra ese estado y su corrección.
Ahora se observa la marca DOM del layout, se escucha `enhancedload` en **Blazor**
y se sincroniza al restaurar historial mediante `pageshow`/`popstate`. El nodo de
reconexión se conserva durante navegación mejorada. No se añadieron hosts
hardcodeados, polling ni un mecanismo de reconexión paralelo.

Las regresiones de [reconexión](../../tests/ResellManager.Tests/reconnect.test.cjs)
incluyen cambios de layout con clase idéntica, eventos de navegación/historial,
estados transitorios y persistentes y conservación del modal administrativo.

## 9. Validación final y QA físico

Validación consolidada el **09/10/2026**, con .NET SDK 10.0.302, Node 24.19.0
y Edge mediante Playwright. Datos ficticios, SQLite, archivos y claves aislados;
la inspección de Preview fue sólo lectura. Las cifras 894/.NET, 123/JS y
76/responsive de V1.4 inicial se conservan exclusivamente como evidencia histórica.

| Validación | Resultado |
| --- | --- |
| Compilación de la solución | Correcta; 0 errores y 0 advertencias. |
| Suite .NET completa | **942/942**; 0 fallos y 0 omitidas. |
| Foco backend/API de catálogo | **80/80**; elegibilidad, SQL acotado, compatibilidad y privacidad. |
| Foco UI incremental/ciclo de vida | **30/30**; carreras, reintentos y desmontaje durante importación JS. |
| Foco Producto/lookup/alta desde Compra | **139/139** tras corregir las regresiones encontradas. |
| Suite JavaScript | **134/134**; 0 fallos. |
| Dependencias y generación CSS | Instalación desde lockfile y generación correctas; 0 vulnerabilidades informadas. Asset generado conservado. |
| QA de navegador a 320/390/768/1440 px | **193 comprobaciones** correctas; 0 errores JavaScript. |
| Documentación y diff | 153 enlaces locales y 15 anclas verificadas; sin errores de whitespace. |

El [runner de navegador](../../tests/catalogo.browser.mjs) conserva el fallback
manual y además comprueba un `IntersectionObserver` real. Cubre carruseles de
0/1/10/más de 10 artículos, grupos acotados, páginas 16/32/48/57, error y reintento
sin perder tarjetas, opciones ampliables, hija fuera del primer bloque, filtros
combinados, URL/recarga/historial y respuestas tardías descartadas. Mantiene las
regresiones de galería, zoom, disponibilidad y enlace WhatsApp.

El flujo administrativo utiliza servicios y archivos reales de prueba: selección
raíz/hija, progreso observado durante cinco transferencias, portada/orden,
0/1/8 fotos, rechazo de la novena y firma inválida, guardado fallido y reintento,
y alta reutilizada desde Compra. Las pruebas instrumentadas cubren además fallo
de transferencia y compensación. Se revisaron capturas móviles y de escritorio
sin overflow, con tarjetas legibles y aviso de reconexión pequeño. Evidencia local:
`.artifacts/catalogo-qa/1791561303063/report.json`, capturas y `photo-progress.json`.

La validación encontró y corrigió regresiones: disposición repetida del
formulario tras introducir cancelación de fotos y una instantánea de prueba
anterior a la nueva preselección raíz/hija. También se limpió el lote de fotos
fallido al deshacer una importación, evitando referencias obsoletas y recuperando
el guardado; una regresión comprueba conservación de fotos y nueva selección.
El QA detectó columnas de carrusel comprimidas y texto accesible posicionado
fuera de su área desplazable; se
corrigieron las dimensiones y su contención. Estos casos quedaron cubiertos,
igual que la importación JS tardía al desmontar y los callbacks de sentinel
obsoletos o rechazados por un circuito caído.

**QA físico exploratorio comunicado por el usuario el 09/10/2026:** probó
la navegación del catálogo en un iPhone y seleccionó/subió fotografías desde
la galería y directamente desde la cámara; reportó funcionamiento satisfactorio.
El navegador, modelo exacto de esa sesión y un protocolo detallado no quedaron
confirmados; esta evidencia es manual y declarativa, distinta de Playwright.
El botón WhatsApp no apareció: su ocultación es esperada sin número válido,
pero no se comprobó la configuración del servidor ni la apertura del enlace.

**Pendiente de QA específico:** carruseles/scroll infinito, chips e historial
bajo casos de estrés; pinch/pan/swipe y accesibilidad en dispositivos reales;
desconexión/caché y causa de la captura original; apertura de WhatsApp una vez
configurado; comprobación explícita de Safari y Android, y escritorio.
Los ajustes están implementados; este QA parcial no certifica todos los
recorridos ni implica release o despliegue.

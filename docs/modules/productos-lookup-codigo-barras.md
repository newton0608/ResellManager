# Búsqueda asistida de productos por código de barras

**Estado: implementado en Agregar producto y validado físicamente con Brave en un iPhone 14 Plus. Esa prueba no certifica la regresión histórica específica de Safari; ver la guía del scanner.**

Esta función reduce la captura manual al registrar productos nuevos. Reutiliza el
scanner existente para consultar fuentes externas por código de barras, permite
revisar el resultado antes de tocar el formulario y deja el guardado final bajo
control explícito de la usuaria.

No convierte ResellManager en una base de datos global de productos y no sustituye
el alta manual.

## Alcance

La función aplica al flujo **Agregar producto** del módulo Productos.

El formulario actual conserva:

- captura manual de `CodigoBarras`;
- `BarcodeScanner` con su contrato actual;
- edición manual de todos los campos;
- el botón normal **Guardar producto** como único punto que crea el producto.

La edición de un producto existente no activa automáticamente consultas externas.
El scanner puede seguir utilizándose allí conforme al comportamiento existente,
pero esta especificación no añade importación externa al flujo de edición.

## Principio principal

Escanear o buscar un código **nunca guarda el producto**.

El flujo es:

1. la usuaria escanea o escribe un código;
2. ResellManager comprueba primero si ya existe localmente;
3. si no existe, consulta proveedores externos en orden;
4. un resultado se presenta en un diálogo de revisión;
5. solo **Usar estos datos** copia información al formulario;
6. la usuaria puede corregir o completar los campos;
7. únicamente **Guardar producto** persiste el alta.

Cancelar, cerrar, buscar otra fuente o agotar proveedores no persiste nada.

## Inicio de la búsqueda

La consulta puede iniciarse de dos formas:

- al confirmar un código desde el `BarcodeScanner`;
- mediante una acción visible **Buscar producto** para un código escrito
  manualmente.

No se elimina la captura manual.

El código debe validarse como una entrada razonable para los formatos que ya
soporta el scanner. La búsqueda externa puede intentar representaciones
equivalentes internamente cuando un proveedor lo requiera, pero no debe alterar
silenciosamente el valor aprobado por la usuaria. Si un proveedor devuelve una
representación distinta, debe mostrarse como parte del candidato.

## Comprobación local obligatoria

Antes de cualquier petición externa se consulta ResellManager por coincidencia
exacta de `Producto.CodigoBarras`.

Si existe un producto:

- no se consulta ningún proveedor externo;
- se informa claramente qué producto utiliza el código;
- se ofrece abrir o consultar ese producto cuando encaje con la navegación actual;
- no se crea un duplicado desde este flujo.

La consulta local pertenece a Application/Infrastructure. Un componente Razor no
debe consultar `DbContext` directamente.

## Proveedores externos

La implementación debe usar una abstracción común, equivalente conceptualmente a:

`IProductoLookupProvider`

Cada adaptador encapsula su HTTP, autenticación/configuración, DTOs y peculiaridades.
La UI y el orquestador trabajan con un resultado normalizado.

Proveedores iniciales y prioridad:

1. **Open Facts / Open Food Facts**, usando la API oficial apropiada para
   consulta por código y, cuando sea posible, la cobertura transversal disponible;
2. **UPCitemdb**, como fallback.

Agregar un proveedor futuro no debe requerir cambiar el formulario ni duplicar la
orquestación.

Antes de implementar cada adaptador se deben respetar la documentación oficial,
identificación/User-Agent, límites de uso y configuración vigente de su API.

No se guardan secretos en código o Git. Cualquier clave futura se obtiene mediante
configuración externa siguiendo los patrones existentes del proyecto.

## Resultado normalizado

El contrato interno debe representar, cuando existan:

- código de barras;
- nombre;
- descripción;
- marca;
- modelo;
- color;
- talla;
- presentación;
- peso en gramos;
- contenido/volumen compatible con el modelo actual;
- categoría externa como texto informativo;
- URL de imagen;
- fuente/proveedor.

Los DTOs concretos de Open Facts y UPCitemdb no deben escapar de sus adaptadores.

El nombre de la fuente sirve para revisión/diagnóstico; no requiere una nueva
columna en `Producto`.

## Estrategia de fallback

Una ronda de búsqueda mantiene el orden de proveedores y sabe cuáles ya fueron
consultados.

Estados diferentes:

- encontrado;
- no encontrado;
- timeout;
- límite de peticiones;
- proveedor no disponible;
- respuesta inválida.

Si un proveedor no encuentra el producto o falla técnicamente, el flujo continúa
con el siguiente proveedor siempre que quede alguno.

Cuando aparece un candidato, se detiene temporalmente la ronda y se muestra al
usuario.

Si la usuaria elige **Buscar en otra fuente**, el candidato se descarta y la ronda
continúa con el siguiente proveedor. No se vuelve a consultar un proveedor ya
descartado dentro de la misma ronda.

Si se agotan todas las fuentes:

- se informa que no hubo coincidencias útiles;
- se conserva el código introducido/escaneado;
- se permite seguir manualmente;
- puede ofrecerse **Reintentar búsqueda**, iniciando una ronda nueva.

Los fallos externos nunca bloquean el alta manual.

## Diálogo de revisión

Cuando exista un candidato, se abre un diálogo delante del formulario, siguiendo
el patrón visual existente de confirmación/revisión usado por el sistema.

Muestra, cuando estén disponibles:

- imagen;
- nombre;
- marca;
- código consultado y código devuelto si difieren;
- descripción;
- modelo;
- color;
- talla;
- presentación;
- peso o volumen;
- categoría externa;
- fuente.

Acciones mínimas:

- **Usar estos datos**
- **Buscar en otra fuente**
- **Continuar manualmente**

Cerrar o cancelar equivale a no aplicar el candidato.

Hasta que se pulse **Usar estos datos**, el resultado vive solo en memoria y no
modifica `ProductoFormModel`.

## Aplicación al formulario

Al pulsar **Usar estos datos**:

- se crea una instantánea del estado actual del formulario;
- se copian únicamente campos presentes y utilizables;
- valores ausentes no vacían campos existentes;
- el resultado sigue siendo editable;
- aparece una acción **Deshacer datos importados** mientras esa importación siga
  siendo reversible en la sesión.

**Deshacer datos importados** restaura exactamente la instantánea anterior a esa
aceptación, incluida la información escrita manualmente antes de buscar.

Una búsqueda posterior puede iniciar una nueva importación y una nueva instantánea
según el diseño más simple que mantenga el comportamiento predecible.

## Reglas por campo

Se pueden proponer/importar:

- `CodigoBarras`;
- `Nombre`;
- `Descripcion`;
- `Marca`;
- `Modelo`;
- `Color`;
- `Talla`;
- `Presentacion`;
- peso;
- volumen;
- imagen principal externa pendiente.

### Código de barras

El valor escaneado o escrito por la usuaria es la referencia principal de la
operación. No se normalizan ceros o dígitos de forma oculta.

Si el proveedor responde con otra representación equivalente, se muestra para
revisión. Aceptar el candidato no debe reemplazar silenciosamente el código
original sin una regla explícita y visible.

### Precio sugerido

`PrecioSugerido` **nunca se importa** de fuentes externas.

Precios de terceros pueden usar otra moneda, contexto o antigüedad y no representan
la política comercial de Virtuosa/ResellManager.

### Categoría

No se crean categorías automáticamente.

`CategoriaExterna` se muestra como referencia. Solo puede seleccionarse una
`CategoriaId` local si existe una coincidencia inequívoca y segura con una
categoría ya registrada. Si no, la usuaria debe elegirla manualmente.

### Medidas

Se respetan las reglas actuales de Producto: no dejar peso y volumen informados
simultáneamente cuando el modelo vigente los considera excluyentes.

Cuando una fuente entrega una medida ambigua o no convertible con seguridad, no se
importa automáticamente.

### Texto externo

Descripción y demás textos se tratan como datos no confiables:

- no se renderiza HTML externo;
- se normalizan/recortan según las restricciones vigentes del formulario/dominio;
- contenido vacío o claramente inválido se ignora.

## Imagen externa pendiente

Una URL de imagen encontrada se muestra en el diálogo sin guardarse.

Al aceptar el candidato queda como **imagen externa pendiente** para el alta.

Reglas:

- una imagen elegida manualmente por la usuaria tiene prioridad;
- no se persiste una dependencia permanente de la URL remota;
- al guardar el producto, la imagen externa aceptada se descarga y pasa por las
  mismas validaciones y almacenamiento administrado de imágenes del proyecto;
- tipo real, tamaño y contenido deben validarse;
- la descarga usa timeout y límites;
- no se permiten esquemas ni destinos inseguros; la implementación debe mitigar
  SSRF y no acceder a direcciones locales/privadas por una URL arbitraria;
- un fallo de la imagen externa no debe dejar archivos huérfanos ni corromper el
  alta del producto.

La imagen externa es una ayuda. Si no puede obtenerse de forma segura, el producto
debe poder registrarse sin ella mediante un mensaje controlado, salvo que una
regla existente más restrictiva lo impida explícitamente.

Reutilizar `ProductoConImagenService`, `IAlmacenamientoImagenesProducto` o la
abstracción vigente en lugar de crear almacenamiento paralelo.

## Orquestación y capas

Responsabilidades esperadas:

- **Domain:** sin conocimiento de APIs externas;
- **Application:** contratos/resultados normalizados y orquestación agnóstica de
  HTTP cuando corresponda;
- **Infrastructure:** adaptadores HTTP de proveedores y consultas/persistencia
  concretas;
- **Web:** iniciar búsqueda, presentar estados, diálogo y aplicar datos temporales
  al formulario.

No introducir llamadas HTTP de proveedores, JSON externo, reglas de fallback ni
consultas EF directamente en Razor.

Usar `HttpClient`/`IHttpClientFactory` y configuración tipada según los patrones
del proyecto.

## Estados de UX

La interfaz debe representar al menos:

- listo/manual;
- buscando;
- producto local existente;
- candidato encontrado;
- buscando siguiente fuente;
- proveedores agotados;
- error externo recuperable;
- datos importados;
- datos importados deshechos.

Evitar consultas simultáneas del mismo código y bloquear solo las acciones
necesarias mientras una búsqueda está en curso.

El flujo debe conservar usabilidad móvil y escritorio y no romper el diálogo ni
la privacidad/cleanup del scanner existente.

## Logging y privacidad

Registrar información técnica suficiente para diagnosticar proveedor, timeout,
rate limit o respuesta inválida sin incluir imágenes completas, secretos ni
payloads innecesarios.

No enviar a proveedores más datos del negocio que el código necesario para la
consulta.

## Sin cambios de esquema por defecto

Esta funcionalidad es una asistencia de captura y no necesita, por sí sola:

- columnas de fuente externa;
- cache persistente;
- historial de consultas;
- nuevas entidades;
- migraciones.

Si durante la implementación aparece una necesidad real de persistencia, debe
justificarse antes de introducir una migración.

## Pruebas mínimas

Las pruebas automatizadas no deben depender de Internet real. Usar fakes,
handlers HTTP controlados o dobles de providers.

Cubrir al menos:

1. coincidencia local evita llamadas externas;
2. primer proveedor encuentra el producto;
3. primer proveedor no encuentra y el segundo sí;
4. primer proveedor falla y el segundo todavía responde;
5. timeout/rate limit no bloquean el flujo manual;
6. todos los proveedores se agotan sin resultado;
7. **Buscar en otra fuente** no repite el proveedor descartado en la ronda;
8. cerrar/rechazar candidato no modifica el formulario;
9. aceptar candidato copia solo campos disponibles;
10. valores ausentes no destruyen datos escritos;
11. **Deshacer datos importados** restaura el estado anterior;
12. `PrecioSugerido` nunca se importa;
13. categoría externa no crea categorías;
14. medidas inválidas/ambiguas no rompen la regla peso-volumen;
15. imagen manual tiene prioridad sobre imagen externa;
16. fallo al obtener imagen externa no deja archivos huérfanos;
17. creación y edición existentes de Producto continúan funcionando;
18. el scanner conserva su contrato y comportamiento existente.

Si se modifica JavaScript del scanner, ejecutar además las pruebas específicas
indicadas en [Scanner](../29_BarcodeScanner.md).

## Criterios de aceptación

La función está terminada cuando una usuaria puede:

1. abrir **Agregar producto**;
2. escanear o escribir un código;
3. comprobar primero si ya existe en ResellManager;
4. consultar proveedores externos en fallback;
5. revisar un candidato sin alterar todavía el formulario;
6. rechazarlo y probar otra fuente;
7. aceptar datos y editarlos;
8. deshacer la importación;
9. continuar manualmente si no hay coincidencias;
10. conservar el código aunque nadie encuentre el producto;
11. elegir manualmente precio y, cuando corresponda, categoría;
12. guardar solo mediante **Guardar producto**.

No se considera completa si una consulta externa persiste un producto, crea
categorías, importa precios de terceros o hace depender permanentemente la imagen
de una URL remota.

## Fuera de alcance

- carrito, checkout, pedidos web o reservas;
- WhatsApp;
- cambios al catálogo público;
- generación de códigos de barras;
- OCR/IA;
- creación automática de categorías;
- importación de precios;
- cache persistente de catálogos externos;
- cambios de Docker, Caddy, DNS o despliegue;
- refactors generales no necesarios para este flujo.

## Implementación y configuración

- `ProductoLookupService` en Application conserva la ronda y sus intentos.
  `IConsultaProductoCodigoBarras`, implementado por `ProductoService`, realiza la
  coincidencia local exacta. Se comprueba nuevamente al continuar la ronda y al
  guardar el alta asistida. La comprobación final y la creación comparten una
  transacción de escritura, incluyendo un registro paralelo durante la descarga.
- Los adaptadores y la descarga están en `Infrastructure/Lookup/`. El orden de
  registro de `IProductoLookupProvider` determina el fallback; cada candidato
  útil puede contener datos parciales, sin exigir que todos los campos existan.
- Open Facts consulta `GET /api/v3/product/{code}?product_type=all`. Sus
  redirecciones se limitan a las cuatro instancias oficiales y a rutas de consulta
  de productos. UPCitemdb usa `GET /prod/trial/lookup?upc={code}` por defecto;
  una clave configurada selecciona `/prod/v1/lookup` y se envía solo en headers.
  Estas APIs reciben códigos numéricos de 8, 12, 13 o 14 dígitos. Un CODE-128 de
  texto conserva la consulta local y la captura manual, sin peticiones HTTP
  incompatibles. No se modifica el string original, incluidos espacios.
- `ProductoForm` habilita la asistencia solo desde `/productos/nuevo`; la edición
  y el alta contextual de compras conservan su flujo. El diálogo reutiliza
  `ConfirmacionOperacion`; Escape descarta el candidato. La última aceptación
  conserva una instantánea completa del formulario, unidades de medida, imagen
  manual y URL pendiente. La categoría local siempre se elige manualmente.
- Se importan medidas con una sola unidad inequívoca (`ml`, `cl`, `l`, `g`, `kg`,
  `lb` u `oz` de masa). No se interpreta `fl oz`, separadores de miles, cantidades
  compuestas ni medidas contradictorias. Al importar una medida válida se libera
  la medida opuesta; deshacer restaura ambas selecciones anteriores.
- `CrearAsistidoAsync` de `ProductoConImagenService` reutiliza la preparación,
  transacción, confirmación y compensación actuales. La imagen manual prevalece;
  una descarga, contenido o confirmación externa fallidos permiten guardar sin
  imagen con un aviso. La URL nunca se guarda en Producto.
- Las respuestas JSON se limitan a 1 MiB y a un timeout total por proveedor
  (incluida la lectura del cuerpo). La imagen usa 10 segundos y 8 MiB como máximo,
  y pasa por el decoder real y conversión a WebP existentes. Solo se admite HTTPS
  en puerto 443, sin credenciales de URL ni proxies. Las
  [redirecciones de imágenes](../24_ImagenPrincipalProducto.md#imágenes-externas-importadas-desde-la-búsqueda)
  se siguen manualmente con límite y validación SSRF por salto.
  El socket valida las direcciones DNS públicas y conecta a esa IP validada,
  evitando una segunda resolución; rechaza redes privadas, locales y reservadas.

La sección tipada `ProductoLookup` permite configurar externamente:

| Clave | Valor predeterminado |
| --- | --- |
| `UserAgent` | `ResellManager/1.0 (+https://github.com/newton0608/ResellManager)` |
| `TimeoutSegundos` | `6` (acotado a 1–30) |
| `OpenFactsHabilitado` / `UpcitemdbHabilitado` | `true` |
| `OpenFactsPeticionesPorMinuto` | `15`, máximo 15 |
| `UpcitemdbPeticionesPorMinuto` / `UpcitemdbPeticionesPorDia` | `6` / `100` |
| `UpcitemdbUserKey` | sin clave; plan trial |

Para una clave futura puede usarse `ProductoLookup__UpcitemdbUserKey` mediante
configuración externa. No se versiona su valor. Los límites se comparten en
memoria entre sesiones del proceso, respetan `Retry-After` y los headers de
cuota/reset del proveedor y no realizan reintentos automáticos ni esperas.
En trial se acotan a las cuotas oficiales; un plan de pago requiere configurar
sus límites acordados. Otras aplicaciones o instancias pueden consumir la misma
cuota por IP: una respuesta 429 sigue siendo recuperable.

Referencias oficiales revisadas al implementar:

- [Consulta transversal de Open Facts](https://openfoodfacts.github.io/documentation/docs/Product-Opener/api/tutorials/scanning-cosmetics-pet-food-and-other-products/).
- [API v3 y campos](https://openfoodfacts.github.io/documentation/docs/Product-Opener/v3/products/get-api-v3-product-code/).
- [Identificación y límites vigentes](https://openfoodfacts.github.io/openfoodfacts-server/api/).
- [UPCitemdb: configuración](https://www.upcitemdb.com/wp/docs/main/development/getting-started/),
  [respuestas](https://www.upcitemdb.com/wp/docs/main/development/responses/) y
  [límites](https://www.upcitemdb.com/wp/docs/main/development/api-rate-limits/).

Pruebas automatizadas: `ProductoLookupTests`, `ProductoLookupProvidersTests` y
`ProductoLookupImagenTests`, junto con las regresiones existentes de productos,
imágenes y formularios. Los proveedores y handlers son dobles locales; no requieren
Internet ni datos reales. Se mantiene el QA óptico y de privacidad del scanner
documentado en su [guía](../29_BarcodeScanner.md).

## Validación de implementación (2026-10-05)

- Build de la solución en Debug y Release: cero errores y advertencias.
- Suite completa Release: 771 pruebas correctas, sin omisiones; incluye 94 nuevas
  pruebas de lookup, adaptadores e imágenes. Las pruebas existentes conservan sus
  expectativas de negocio.
- npm ci, npm run css:build y npm run test:js: correctos; 115 pruebas JS.
  Se mantienen los avisos de dependencias documentados en la guía del scanner.
- QA de Blazor con SQLite aislado y proveedores ficticios en Edge headless a
  320/390/768/1440 px: revisión, Escape, otra fuente, aceptación, edición,
  deshacer, agotamiento, consulta local, precio/categoría manuales y alta con
  aviso ante fallo de imagen. Se comprobó ausencia de overflow y controles
  del diálogo de al menos 44×44 px.
- El consumidor real del scanner se probó con fixtures EAN-13 y CODE-128:
  únicamente Usar código inicia lookup en el alta; la edición solo captura.
  La cámara y documentos del decoder quedan liberados antes de la revisión.
- npm run qa:scanner: 80 comprobaciones y 28 lecturas ópticas correctas,
  sin errores, en los cuatro tamaños. JS, contrato y vendor del scanner
  permanecen intactos. Evidencia local en .artifacts/scanner-qa/ y
  .artifacts/producto-lookup-qa/; no se versionan datos de pruebas.
- git diff --check: correcto.

No se usó cámara física ni se hicieron consultas de producto a Internet en **esta validación automatizada del 05/10**. Ese era el estado de la evidencia en ese momento; la validación operativa real del 06/10 se registra más abajo. No se añadieron migraciones en esta funcionalidad.

## Corrección de la preview externa (2026-10-05)

Tras «Usar estos datos», el formulario muestra la URL externa pendiente como
preview solamente si pasa `DestinoImagenProductoSeguro.UrlPermitida`, igual que
el diálogo de revisión. La preview usa `referrerpolicy="no-referrer"`. Una imagen
manual conserva prioridad y una URL no permitida nunca se incorpora al `src`
de la preview. «Quitar imagen externa» elimina la URL y la preview; «Deshacer datos
importados» recupera la imagen, los bytes manuales y los indicadores anteriores.
Las imágenes ya guardadas siguen usando `/productos/{id}/imagen` y su eliminación
habitual. Mostrar la preview no invoca el alta ni el descargador del servidor:
la importación y persistencia definitiva siguen ocurriendo al guardar.

`ProductoLookupPreviewTests` renderiza el componente Razor real y prueba
aceptación, URLs rechazadas por la política existente, quitar, deshacer,
prioridad manual antes y después de importar y la ruta de imágenes ya guardadas.
La investigación y corrección de las cuatro alertas npm se detalla en
[Auditoría npm del scanner](../29_BarcodeScanner.md#auditoría-npm-2026-10-05).

Validación del seguimiento: build Debug y Release sin errores ni advertencias;
136 pruebas .NET del área de lookup/imágenes/formulario y 789 de la suite Release,
incluidas 18 regresiones nuevas de preview, todas correctas; 115 pruebas JS;
`npm ci`, `npm run css:build` y `npm audit` correctos (0 vulnerabilidades).
QA de Blazor en 320/390/768/1440 px con SQLite temporal, proveedores simulados e
imágenes interceptadas localmente: carga de preview, quitar, deshacer, prioridad
manual y ausencia de descarga del servidor hasta guardar. QA del scanner:
80 comprobaciones y 28 decodificaciones ópticas, sin fallos. `git diff --check`
correcto. En **este seguimiento automatizado del 05/10** todavía no se hicieron
peticiones a proveedores reales, migraciones, despliegues ni merge; la evidencia
operativa posterior se registra más abajo.

## Guardado externo y redirecciones (2026-10-05)

Se reprodujo un defecto real e independiente en la descarga HTTP: el descargador rechazaba cualquier respuesta 3xx antes de preparar y confirmar el WebP. Un navegador podía seguir esa redirección para mostrar revisión/preview mientras `CrearAsistidoAsync` recibía una descarga fallida y guardaba sin imagen. Se corrigió con seguimiento manual y validado de redirecciones. **Sin embargo, esta no fue la causa del fallo persistente observado después en Preview**: ese entorno continuó fallando incluso con URLs de imagen que respondían 200, y su causa operativa se documenta en la sección siguiente.

El diagnóstico manual, separado de las pruebas automatizadas, confirmó un
`301` legítimo de `https://world.openfoodfacts.org/images/products/...` hacia
`https://images.openfoodfacts.org/images/products/...`, para la misma imagen
pública de referencia. Las URL actuales de las muestras consultadas de Open
Food Facts, Open Beauty Facts y el primer CDN de UPCitemdb respondieron
`200 image/jpeg` y el descargador real pudo leerlas. Otra imagen devuelta por
UPCitemdb respondió `403` con un challenge de Cloudflare: una preview de navegador
no garantiza que el servidor pueda descargar cualquier recurso. No se recibió
el código/URL del producto reportado para atribuirle una de esas respuestas
específicas; el rechazo de redirecciones sí quedó reproducido y corregido.

El seguimiento manual, las restricciones SSRF por salto y los límites se definen
en [Imagen principal: importación externa](../24_ImagenPrincipalProducto.md#imágenes-externas-importadas-desde-la-búsqueda).
El alta, la prioridad manual, la compensación de archivos, los endpoints y las
reglas de catálogo reutilizan los servicios existentes.

Validación de esta corrección: `dotnet build ResellManager.sln` en Debug y Release,
sin errores ni advertencias; 210 pruebas del área y 828 pruebas .NET de la suite
Release correctas, incluidas 39 regresiones nuevas; `npm run test:js` con 115
pruebas correctas; `git diff --check` correcto. QA de Blazor en Edge a 320 y
1440 px, con SQLite temporal y HTTP simulado: aceptación sin descargar en el
servidor, cadena 301→302→200 al guardar, ruta persistida, imágenes cargadas en
listado/detalle administrativos e `ImagenCatalogo` y mismo WebP en ambos
endpoints. Se verificaron 404 públicos y ausencia en listado antes de registrar
stock elegible. No se modificaron modelos, migraciones, reglas de publicación,
scanner, dependencias ni infraestructura de despliegue; en ese punto todavía no se había hecho despliegue ni merge.

## Validación operativa y persistencia en Preview — 06/10/2026

Después del merge de la funcionalidad se validó el flujo contra Preview con proveedores y cámara reales:

- el scanner en vivo detectó un código inmediatamente con **Brave en un iPhone 14 Plus**;
- la búsqueda externa devolvió datos e imagen y la revisión/preview funcionó;
- imágenes de muestra de Open Food Facts pudieron descargarse desde el VPS con respuesta `200 image/jpeg`;
- el fix de redirecciones era correcto, pero no resolvía por sí solo el fallo de persistencia observado en Preview.

La causa operativa restante estaba en la configuración de almacenamiento de Preview. DeployManager montaba su persistencia en `/data`, pero el proyecto no configuraba `AlmacenamientoImagenesProducto__DirectorioBase`. La aplicación caía entonces en su valor por defecto relativo a `App_Data/productos` bajo el directorio de la aplicación, que no era el volumen persistente/escribible previsto para ese contenedor.

Se configuró Preview con `AlmacenamientoImagenesProducto__DirectorioBase=/data/productos` y se recreó el contenedor conservando el bind persistente de `/data`. El health check respondió 200 y una nueva alta asistida guardó correctamente la imagen externa; el detalle administrativo dejó de mostrar el fallback sin imagen.

Esta ruta es específica del despliegue de Preview. Producción conserva el contrato de Compose `/app/data/productos` con su bind de host; no copiar `/data/productos` a producción. Ver [Imagen principal](../24_ImagenPrincipalProducto.md) y [Dominios/Preview](../deployment/domains.md).

La prueba física confirma funcionamiento en **iPhone 14 Plus + Brave**. No quedó registrado que se usara exactamente el código histórico que fallaba en v1.2.0 y no se repitió esa comprobación en Safari; por tanto no debe presentarse como certificación específica de Safari.

## Compatibilidad posterior V1.4

La galería manual (0–8 fotos) prevalece sobre la imagen externa aceptada.
El lookup sigue descargando como máximo una portada solo al guardar; no
obtiene fotografías externas adicionales. Deshacer la importación conserva
la galería manual y el alta desde Compra sigue funcionando. Ver [V1.4](catalogo-v1-4.md).

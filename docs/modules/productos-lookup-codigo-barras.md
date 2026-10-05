# Búsqueda asistida de productos por código de barras

**Estado: especificación aprobada; implementación pendiente.**

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

# html5-qrcode

Versión: **2.3.8**. Archivo `html5-qrcode.min.js` de
[la distribución publicada](https://unpkg.com/html5-qrcode@2.3.8/html5-qrcode.min.js).
Licencia Apache-2.0 incluida en `LICENSE`.

SHA-256 del archivo: `660B12437B1D747E3E68B8BE0685C08CB728140110AD213F167B14B66F8B1D8E`.

`barcode-scanner.js` lo carga bajo demanda desde este directorio; la captura
no requiere un CDN en producción.

El flujo principal permite tomar o seleccionar una foto mediante un input
HTML `type="file"`, `accept="image/*"` y `capture="environment"`. Capture es
una sugerencia de cámara trasera: el navegador puede ofrecer la fototeca o
un selector de archivos. No se utiliza `InputFile` ni se transmite el File
a .NET, HTTP, almacenamiento o base de datos. La fotografía permanece sólo
en memoria del navegador durante el análisis local.

La instancia de Html5Qrcode analiza el File con `scanFileV2(file, false)`.
El segundo argumento evita mostrar una preview grande. En 2.3.8, el canvas
oculto conserva la resolución nativa disponible y añade 100 px de padding
por lado; el ancho compacto del diálogo no reduce los píxeles que recibe
el decoder. El resultado se presenta para revisión y sólo «Usar código»
entrega `decodedText`, sin trim ni normalización, a `OnDetected`. La misma
confirmación se aplica a los resultados de la alternativa en vivo.

Un fallo conserva abierto el diálogo y ofrece otra foto o entrada manual.
El input se vacía para permitir seleccionar incluso el mismo archivo. La
opción manual devuelve el foco al campo hermano de código de barras,
cuando está disponible, sin modificar su valor.

El adaptador de archivos depende de la implementación incluida de 2.3.8:
esta versión crea dos object URLs por File y su `clear()` no las revoca.
Durante la llamada síncrona a `scanFileV2`, el adaptador intercepta
`URL.createObjectURL` sólo para ese File y captura la instancia creada con
`window.Image`; restaura ambas APIs inmediatamente después de la llamada.
Al finalizar, fallar, cancelar o disponer el componente revoca las dos
URLs, retira handlers de imagen, vacía src e input y limpia el canvas con
`clear()`. Revisar este adaptador si se actualiza la versión de la librería.

La carga de la imagen puede cancelarse. El decoder síncrono ya iniciado no
tiene API de abort: su resultado tardío se ignora y se completa la limpieza,
sin confirmar códigos ni reabrir la interfaz. Los modos foto y en vivo son
mutuamente excluyentes; antes de analizar una foto se espera la detención de
la cámara, se liberan sus MediaStreamTracks y se limpia el scanner.

El componente compartido mantiene únicamente EAN-13, EAN-8, UPC-A, UPC-E y
CODE128, con ZXing (useBarCodeDetectorIfSupported: false). En vivo usa 10 FPS.
Solicita cámara trasera y 1920×1080 mediante constraints ideal, sin exigir
una resolución exacta. El qrbox responsive usa hasta el 92% del ancho,
una proporción cercana a 2.8:1 y queda limitado por el tamaño del video.

La ventana visible recorta verticalmente el video proporcional. Al cambiar
el ancho se adapta el conjunto video/marco, manteniendo la geometría interna
del crop que esta versión calcula una sola vez. Esta adaptación no es zoom.
Para códigos pequeños, el backing canvas del decoder aprovecha hasta 2×
detalle nativo por eje. En 2.3.8, drawImage usa el destino CSS de qrRegion
y ZXing lee todo el backing canvas; revisar esta integración al actualizar
la librería. Requiere disableFlip: true; si no hay contexto compatible o
resolución nativa suficiente, conserva el muestreo original.

El zoom mostrado aplica constraints reales del track, sólo con capacidades
min/max/step válidas. Inicia cerca de 1.8×, limitado y ajustado al paso
soportado. Si no existe zoom o falla su aplicación inicial, no muestra
slider y continúa escaneando. El enfoque continuo se solicita únicamente
cuando está anunciado y su rechazo no bloquea el lector. Los ajustes
conservan las preferencias flexibles de resolución y enfoque aceptado.

Las pruebas con stream de canvas y capacidades simuladas no validan
hardware de Safari/iPhone. Comprobar en dispositivo físico permisos,
enfoque, zoom disponible, códigos pequeños y rendimiento. Validar también
capture/cámara/fototeca, formatos de imagen aceptados realmente por Safari
y consumo de memoria/tiempo de análisis con fotografías de muchos megapíxeles.
Las imágenes no se reducen preventivamente; si el navegador no puede
analizarlas, siempre queda disponible la entrada manual.

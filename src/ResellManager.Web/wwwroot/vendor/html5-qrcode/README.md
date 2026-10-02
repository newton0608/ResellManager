# html5-qrcode

Versión: **2.3.8**. Archivo `html5-qrcode.min.js` de
[la distribución publicada](https://unpkg.com/html5-qrcode@2.3.8/html5-qrcode.min.js).
Licencia Apache-2.0 incluida en `LICENSE`.

SHA-256 del archivo: `660B12437B1D747E3E68B8BE0685C08CB728140110AD213F167B14B66F8B1D8E`.

`barcode-scanner.js` lo carga bajo demanda desde este directorio; la captura
no requiere un CDN en producción.

El componente compartido mantiene únicamente EAN-13, EAN-8, UPC-A, UPC-E y
CODE128, con ZXing (useBarCodeDetectorIfSupported: false) y 10 FPS.
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
enfoque, zoom disponible, códigos pequeños y rendimiento.
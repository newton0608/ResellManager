# Scanner de códigos de producto: hotfix de v1.2.0

## Alcance y motivo

Las pruebas físicas de v1.2.0 en iPhone/Safari no lograron leer el mismo código
1D ni por fotografía (aproximadamente 20 intentos) ni en vivo. Se reemplaza
html5-qrcode/ZXing 2.3.8 por **@ericblade/quagga2 1.11.0**, orientado a localizar
y decodificar barras 1D. Los mocks anteriores no demostraban funcionamiento
óptico real en iPhone.

El contrato de BarcodeScanner.razor conserva exactamente Disabled y
`EventCallback<string> OnDetected`. Productos, negocio, base de datos y
migraciones no cambian. El diálogo mantiene fotografía, vivo, manual, cancelar,
reintentar y «Usar código». Detectar sólo presenta un resultado pendiente;
únicamente «Usar código» entrega el string a Blazor.

## Versión y distribución reproducible

Se comprobó el registro npm antes de instalar: 1.11.0 está disponible y el
registro también anuncia 1.12.1. Este hotfix fija **1.11.0** sin rangos en
package.json y conserva la integridad del paquete en package-lock.json.

wwwroot/vendor/quagga2/quagga.min.js y LICENSE están versionados. Por ello
dotnet run/build/publish funcionan sin Node ni node_modules. El decoder se
carga bajo demanda desde el mismo origen y respetando document.baseURI; no
usa CDN.

scripts/sync-scanner-vendor.mjs verifica la versión instalada y copia
dist/quagga.min.js y la licencia MIT. Se ejecuta con npm run scanner:vendor
y también al principio de npm run css:build. Docker copia ese script en su
etapa Node, ejecuta npm ci --include=dev y el build, y transfiere el vendor
generado a la etapa SDK antes del publish. El runtime recibe únicamente los
archivos publicados, sin Node ni dependencias npm.

La búsqueda global confirmó que el scanner era el único consumidor del
decoder anterior. Se retiraron su vendor/documentación, el host oculto de
archivos, el adaptador que interceptaba APIs globales y el muestreo/crop
dependiente de aquella implementación.

## Readers y exactitud de los códigos

Se habilitan solamente, en este orden:

    ["ean_8_reader", "ean_reader", "upc_reader", "upc_e_reader", "code_128_reader"]

Corresponden a EAN-8, EAN-13, UPC-A, UPC-E y CODE-128. No se incorporan lectores
QR ni formatos ajenos. La prioridad EAN-8 tiene una regresión óptica concreta:
la foto vertical oficial ean_8/image-001.jpg, cuyo código impreso es
42191605, produjo 54086883 al probar EAN antes de EAN-8. EAN-8 primero devuelve
el valor correcto y mantiene los resultados de los otros cuatro fixtures.
El QA de navegador conserva esos valores esperados; no los sustituye por
cualquier resultado del decoder.

No se recortan espacios de CODE-128 ni se añaden/eliminan ceros o dígitos.
Quagga puede presentar UPC-A como EAN-13 con cero inicial cuando lo reconoce el
reader EAN. Se muestra y entrega esa representación exacta para revisión;
no existe normalización oculta.

## En vivo

- LiveStream, cámara trasera mediante facingMode: { ideal: "environment" }.
- Resolución ideal 1920×1080, sin dimensiones exactas ni mínimos obligatorios.
- Muestreo de Quagga limitado a 1280 px de lado mayor y máximo 10 lecturas/s.
- locate: true, patchSize: "medium", halfSample: false, multiple: false.
- Área central con márgenes 36% arriba/abajo y 4% a izquierda/derecha.
  Es una franja horizontal; la ventana visible conserva su proporción 2:1.
- Foco continuo y zoom sólo si el track anuncia capacidades válidas. El zoom
  conserva preferencias de resolución, límites/paso y foco aceptado.
- Timeout de 45 s que incluye carga del bundle y solicitud de permiso.

Se requieren **dos detecciones consecutivas del mismo string en 1500 ms**.
Un código distinto o un resultado malformado reinicia la confirmación. Un
fotograma sin lectura no cuenta como detección; si la siguiente lectura está
fuera de la ventana, inicia una confirmación nueva. onProcessed no suma una
segunda lectura del mismo fotograma. Al confirmar se detienen decoder y
tracks, se vuelve al resultado y se espera «Usar código».

No se define un umbral universal de confianza: decodedCodes[].error mide
ajuste de patrones con escalas distintas por reader, no probabilidad. Se
rechazan medidas presentes negativas o no finitas, formatos ajenos y códigos
numéricos de longitud inválida. Los readers hacen sus comprobaciones internas
de checksum; la doble lectura y la revisión humana completan la protección.
Ni checksum ni doble lectura garantizan ausencia absoluta de falsos positivos.

## Fotografía local y orientación

Se mantiene el input type="file" accept="image/*" capture="environment".
capture es una sugerencia: Safari puede ofrecer cámara/fototeca. El input
se vacía para aceptar la misma foto nuevamente.

1. Leer como máximo 128 KiB de cabecera local para dimensiones JPEG y
   orientación EXIF válida (TIFF little/big endian).
2. Para un tag EXIF localizado, crear un Blob por slices con orientación 1.
   Aplicar su orientación original exactamente una vez con Canvas, incluidas
   rotaciones y espejos EXIF 1–8. No se rota dos veces según el navegador.
3. Preferir createImageBitmap con imageOrientation: "from-image" y resize
   de JPEG a **2560 px de lado mayor** cuando se conocen dimensiones. Si la API
   o sus opciones/formato fallan, cargar un Image mediante ObjectURL local.
   El fallback recibe el mismo JPEG neutralizado.
4. Preparar un único canvas maestro de hasta 2560 px por lado mayor, respetando
   proporción y sin ampliar fotos pequeñas. Liberar bitmap/Image al prepararlo.
5. Decodificar secuencialmente, deteniéndose en el primer resultado válido:

| Candidato | Región del maestro | Lado mayor máximo |
| --- | --- | --- |
| Completo | 100% × 100% | 1600 px |
| Central amplio | 90% del ancho × 50% del alto | 1920 px |
| Central cercano | 65% del ancho × 30% del alto | 1920 px |

Los recortes aprovechan más detalle del código central que una reducción de
toda la foto. Se usa halfSample: false e inputStream.size: 0 para impedir
que decodeSingle vuelva a reducir los candidatos a su default de 800 px.
Se entregan PNG locales sin EXIF al decoder, sin añadir rotaciones
combinatorias. Quagga ya localiza barras giradas. Se libera cada candidato
antes del siguiente y se cede al event loop entre intentos. Timeout total: 30 s.

Un maestro cuadrado de 2560 px tiene como máximo 6,55 MP; una foto 4:3 reducida
tiene 4,92 MP. Ninguna foto de 12/24/48 MP se entrega directamente a Quagga.
Image o un formato sin dimensiones JPEG disponibles pueden necesitar
decodificación nativa temporal a resolución original antes de reducirlo.
Esto depende del navegador y debe medirse físicamente en Safari.

Para otros formatos o EXIF situado fuera de la cabecera acotada se usa
orientación from-image del bitmap, o la orientación visual del Image
nativo en el fallback. No se prometen compatibilidad universal de HEIC/HEIF
ni equivalencia de orientación en navegadores antiguos: fallos de carga o
memoria muestran el error técnico y dejan foto/manual/reintento disponibles.

Estados separados: idle, analyzing, success, not-found y technical-error.
Si las tres decodificaciones finalizan sin código se muestra «No encontramos
un código de barras en la foto». Un fallo técnico muestra «No pudimos analizar
la foto. Inténtalo de nuevo». El log sólo incluye una etiqueta y nombre del
error, sin imagen, data URL, código ni stack para el usuario.

## Privacidad y cleanup

Los bytes de foto no cruzan la interop de .NET, HTTP, almacenamiento ni base
de datos. Cabecera, Blob, bitmap, canvas y PNG/data URL existen sólo durante
el análisis en memoria del navegador.

Quagga2 1.11.0 no tiene abort público para decodeSingle y conserva recursos
internos de imagen/eventos. Cada operación usa un documento about:blank
del mismo origen, descartable; ahí se carga el bundle local. La sesión en
vivo tiene también su propio documento visible dentro del host de cámara.
No se embebe ninguna ruta de aplicación ni se cambian X-Frame-Options,
frame-ancestors o las políticas de seguridad. El atributo allow="camera"
mantiene el permiso del mismo origen. Los assets siguen siendo locales.

Cerrar, cancelar, cambiar modo, confirmar, fallar, timeout, desmontaje o
pagehide retiran callbacks onDetected/onProcessed, detienen tracks, paran
Quagga, vacían src/srcObject/canvas y eliminan el documento del decoder.
También se revocan ObjectURLs propios, se cierra cada bitmap, se retiran
handlers de imágenes y controles, se eliminan timers/ResizeObservers y se
desconecta el MutationObserver del componente. Resultados tardíos se
descartan, incluso si llega un permiso tras cancelar.

visibilitychange detiene el modo vivo al ocultar la página; no cancela foto,
porque el selector nativo iOS puede ocultarla temporalmente. pagehide
termina ambos modos. Foto y vivo son excluyentes. El componente mantiene
IAsyncDisposable y su interop Blazor existente.

## Validación y límites

Pruebas unitarias conductuales:

    npm ci
    npm run css:build
    node --test tests/barcode-scanner.test.mjs tests/ResellManager.Tests/*.test.cjs
    dotnet build -c Release
    dotnet test -c Release --no-build
    git diff --check

tests/barcode-scanner.browser.mjs usa el markup real del diálogo, ambos CSS
reales, el bundle local y fixtures oficiales fijados a su commit, con cabeceras
de seguridad equivalentes. Requiere Playwright disponible; ejecutar
npm run qa:scanner. Se puede indicar un módulo instalado mediante
SCANNER_PLAYWRIGHT_MODULE (ruta absoluta a playwright/index.mjs) y elegir
SCANNER_BROWSER_CHANNEL (predeterminado msedge). No se agrega Playwright
al runtime ni al lockfile de producción. Evidencia local en
.artifacts/scanner-qa/: screenshots y report.json.

El QA compara códigos ópticos esperados, estados, apertura/cierre, ausencia
de overflow y controles de al menos 44×44 px a 320/390/768/1440 px. El stream
de cámara es un canvas con barras sintéticas, no hardware. Las pruebas
unitarias cubren cancelación/errores/permisos/timeout, orientación 1–8,
recursos tardíos, zoom, resize, confirmación y privacidad.

### Resultado automatizado de este hotfix

- npm ci y npm run css:build: correctos.
- 115 tests JS: 98 del scanner y los 17 existentes de otros módulos; sin fallos.
- dotnet build -c Release: 0 errores y 0 warnings.
- 677 tests .NET: sin fallos ni omisiones; no se cambiaron sus expectativas.
- dotnet publish local: correcto; bundle y licencia presentes, con SHA-256
  idéntico al vendor versionado.
- Edge/Chromium headless: 80 comprobaciones y 28 casos ópticos documentados.
  Los cinco readers se ejercitan con fixtures fotográficos reales; el vivo
  emplea un stream de canvas. Incluye EXIF 6/8 real con bitmap y fallback,
  los cinco estados, controles táctiles y ausencia de overflow en los cuatro anchos.
- git diff --check: correcto. Sólo queda la referencia histórica al motor
  retirado al explicar el motivo de este cambio.

npm informa cuatro vulnerabilidades altas en la cadena existente
Tailwind CLI → parcel/watcher → micromatch → braces, y un aviso de scripts de
parcel/watcher sin aprobación npm registrada. No se ejecutó audit fix --force
ni se actualizó Tailwind fuera del alcance. El build CSS terminó correctamente.
Docker no está instalado en este entorno: se revisó su distribución por etapas
y se comprobó publish local; no se construyó ni desplegó una imagen Docker.

Fuentes de API:
[Quagga2](https://github.com/ericblade/quagga2),
[createImageBitmap/orientación](https://developer.mozilla.org/en-US/docs/Web/API/Window/createImageBitmap),
[seguridad de captura en subframes WebKit](https://webkit.org/blog/7763/a-closer-look-into-webrtc/).

Pendiente validación física en iPhone/Safari con el código real que fallaba en v1.2.0.

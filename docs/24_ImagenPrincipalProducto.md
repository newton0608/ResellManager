# Galería e imagen principal opcional de producto

Cada producto admite de cero a ocho fotografías totales. `ProductoImagenes` guarda identificadores GUID, referencias privadas y orden; `Producto.ImagenPrincipalRuta` mantiene la única portada y la compatibilidad de los endpoints anteriores. No se guardan binarios en SQLite. La migración V1.4 registra cada referencia histórica sin copiar ni reprocesar archivos; categorías e historial permanecen intactos.

`IAlmacenamientoImagenesProducto` recibe el flujo sin usar el nombre original. Admite JPEG, PNG y WebP validados por firma y decodificación con SkiaSharp. El límite de entrada es 8 MB y 25 millones de píxeles. Se aplica la orientación codificada y se conserva la proporción sin ampliar imágenes pequeñas. `PrepararAsync` conserva el máximo histórico de 1200 píxeles y WebP calidad 82; `PrepararGaleriaAsync` permite nuevas fotografías hasta 2000 píxeles, calidad 88, con los mismos límites de entrada y memoria. No se reprocesan imágenes existentes. HEIC/HEIF no está soportado en esta versión: requeriría dependencias o soporte nativo adicional en Linux.

El archivo procesado pasa por `.temporales/` y se confirma como `productos/{id}/imagen-principal-{guid}.webp`. La base solo conserva esa ruta relativa. El directorio físico se configura mediante `AlmacenamientoImagenesProducto:DirectorioBase` (en Docker, `/app/data/productos`), fuera de `wwwroot`. La lectura usa `GET /productos/{id}/imagen` con autenticación y valida la ruta antes de abrir el archivo. No se publican rutas del servidor.

El servicio de producto con imagen prepara el archivo, guarda el producto y la nueva referencia dentro de una transacción SQLite, y compensa eliminando temporales o archivos confirmados cuando el guardado falla. En el reemplazo conserva la imagen anterior hasta confirmar la nueva referencia y después la elimina. Al quitarla, limpia la referencia y después elimina el archivo. Como SQLite y el sistema de archivos no comparten transacción, un error físico al borrar después del commit se registra como crítico para limpieza operativa; no se elimina antes un archivo que aún pueda estar referenciado.

Producción requiere el bind mount `/opt/resellmanager/data/productos:/app/data/productos`. Staging requiere `/opt/resellmanager-staging/data/productos:/app/data/productos`. El directorio debe ser persistente y escribible por el usuario del contenedor. El backup debe incluir conjuntamente `database/`, `comprobantes/`, `dataprotection/` y `productos/`; el script versionado ya enumera los cuatro, pero la cobertura de la copia instalada debe verificarse operativamente. Preview usa un despliegue distinto administrado por DeployManager: su bind persistente es `/data` y, desde la corrección operativa del 06/10/2026, configura `AlmacenamientoImagenesProducto__DirectorioBase=/data/productos`. No reutilizar esa ruta interna para producción.

## Imágenes externas importadas desde la búsqueda

La búsqueda conserva la URL aceptada en `ProductoFormModel.ImagenExternaUrl`
hasta «Guardar producto». Una imagen manual tiene prioridad. El alta asistida
descarga la imagen, reutiliza la validación y conversión a WebP de este
almacenamiento y persiste una sola portada en `ImagenPrincipalRuta` y su referencia en la galería. La ruta lógica
`productos/{id}/imagen-principal-{guid}.webp` se resuelve físicamente como
`{DirectorioBase}/{id}/imagen-principal-{guid}.webp`.

`ImagenProductoExternaService` sigue manualmente **301, 302, 303, 307 y 308**,
con un máximo de **tres redirecciones** (cuatro peticiones contando la inicial).
Resuelve `Location` relativa contra la URL de la petición actual y detecta ciclos,
incluidos cambios que solo afectan al fragmento. Un `Location` ausente o inválido,
un ciclo, un salto adicional o un destino inseguro producen el fallo controlado.

Antes de cada petición aplica `DestinoImagenProductoSeguro.UrlPermitida`:
HTTPS/443, sin credenciales, sin nombres locales ni IP privadas/reservadas.
El handler conserva `AllowAutoRedirect = false`, sin proxy ni cookies.
Su `ConnectCallback` valida todas las IP resueltas por DNS y fija la conexión a
una IP pública ya validada; también se conserva esa protección en cada salto.
El presupuesto de **10 segundos** cubre toda la cadena y el cuerpo final,
sin reiniciarse por redirección. Se mantiene el máximo de **8 MiB**, MIME
JPEG/PNG/WebP y la posterior comprobación de firma, decodificación, dimensiones
y conversión por Skia. No se consumen los cuerpos de las redirecciones.

Los logs de descarga incluyen motivo y estado HTTP cuando está disponible,
sin URL, query, credenciales ni cuerpo remoto. Un 403/404, contenido inválido,
destino bloqueado o fallo de archivo conserva el alta sin imagen con el aviso
existente y sin huérfanos; no se eluden restricciones del CDN.

El detalle y listado administrativos consumen `/productos/{id}/imagen`.
Para un producto comercialmente disponible, la API pública proyecta
`TieneImagenPrincipal` y `ImagenCatalogo` carga
`/api/catalogo/productos/{id}/imagen`. Tener imagen no publica por sí solo un
producto: las [reglas de disponibilidad](25_CatalogoPublicoBackend.md#disponibilidad-exacta)
y la autenticación de la imagen administrativa se conservan.

Regresiones: `ImagenExternaRedireccionesTests` prueba HTTP simulado, destinos,
límites, ciclos, cancelación, timeout total y cuerpos acotados;
`FlujoImagenExternaTests` recorre adaptadores HTTP → candidato → formulario →
Guardar → SQLite/WebP → endpoints administrativos/públicos y `ImagenCatalogo`,
incluyendo la exclusión pública antes de registrar una unidad disponible.
Las pruebas automatizadas no consultan Internet ni usan imágenes externas reales.

## Gestión V1.4

`CrearGaleriaAsync` y `EditarGaleriaAsync` validan el límite, pertenencia de IDs,
portada y referencias de nuevas imágenes exactamente una vez. Preparan cada foto
secuencialmente y persisten el conjunto en una transacción SQLite; una novena o
un archivo inválido rechaza el lote completo. La edición permite eliminar, elegir
portada y reordenar sin perder las restantes. Solo se borran archivos después
de verificar que ninguna referencia persistida los usa; los fallos de limpieza
se registran para operación. Las páginas administrativas autenticadas realizan
las escrituras mediante el servicio; los nuevos GET autenticados son
`/productos/{id}/imagenes` y `/productos/{id}/imagenes/{guid}`.

El lookup sigue importando a lo sumo una portada al guardar, con prioridad de
la galería manual. El detalle público usa IDs, nunca rutas físicas. Contrato,
migración y QA: [V1.4](modules/catalogo-v1-4.md).

## Progreso y reintento en el formulario

Los [ajustes UX V1.4](modules/catalogo-v1-4-ajustes-ux.md) instrumentan el formulario
reutilizado en Nuevo/Editar y alta desde Compra. Un spinner con texto accesible
anuncia `Preparando foto n de total` al comprobar el archivo y su firma, y
`Subiendo foto n de total` durante la lectura efectiva de `IBrowserFile` por
SignalR. Después de recibir los bytes, la preparación conserva contenido/vista
previa en memoria para confirmar con el producto. La transferencia no significa
que el archivo ni su referencia ya estén persistidos; la decodificación completa
y el procesamiento WebP siguen siendo responsabilidad del servicio de guardado.

La operación transaccional muestra **«Guardando fotografías…»**, sin contador
por archivo porque el servicio no informa ese avance. La finalización de la
preparación se anuncia sólo después de recibir y validar todo el lote. Los
lotes inválidos conservan la galería anterior sin añadir fotos parcialmente;
la novena fotografía sigue rechazándose antes de transferir.

Ante fallo de transferencia se conserva la selección y existe «Reintentar
preparación». Deshacer una importación descarta el lote de transferencia fallido
antes de renovar el selector, evitando referencias obsoletas a archivos. Ante
fallo de guardado se conservan los bytes ya preparados para
reintentar sin duplicar fotos; las garantías de compensación del servicio se
mantienen. Se bloquean guardado, cambios de fotos y acciones incompatibles
durante la operación, sin modal de pantalla completa. `role=status` y
`aria-live=polite` acompañan al texto; el spinner respeta movimiento reducido.
QA físico de selección desde cámara/galería sigue pendiente.
